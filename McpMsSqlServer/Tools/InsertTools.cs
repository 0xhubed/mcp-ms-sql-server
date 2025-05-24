using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class InsertTools
{
    [McpServerTool]
    [Description("Insert new records into a table in the configured schema")]
    public static async Task<string> InsertRecords(
        [Description("The table name to insert into")] string tableName,
        [Description("JSON object or array of objects with column-value pairs")] string recordsJson,
        [Description("Whether to use a transaction (default: true)")] bool useTransaction,
        DatabaseService databaseService,
        ConfigService configService,
        SecurityService securityService,
        TransactionService transactionService)
    {
        try
        {
            var config = configService.CurrentConfig;
            if (!config.Permissions.AllowWrite)
            {
                return JsonSerializer.Serialize(new { error = "Write operations are not allowed for this configuration" });
            }

            var schemaName = config.AllowedSchema;

            // Validate table access
            var validationResult = await securityService.ValidateTableAccessAsync(schemaName, tableName);
            if (!validationResult.IsValid)
            {
                return JsonSerializer.Serialize(new { error = validationResult.ErrorMessage });
            }

            // Parse JSON records
            List<Dictionary<string, object?>> records;
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(recordsJson);
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    records = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(recordsJson) ?? new();
                }
                else
                {
                    var singleRecord = JsonSerializer.Deserialize<Dictionary<string, object?>>(recordsJson) ?? new();
                    records = new List<Dictionary<string, object?>> { singleRecord };
                }
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(new { error = $"Invalid JSON format: {ex.Message}" });
            }

            if (records.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "No records provided to insert" });
            }

            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            SqlTransaction? transaction = null;
            if (useTransaction)
            {
                transaction = connection.BeginTransaction();
            }

            try
            {
                var insertedCount = 0;
                var errors = new List<string>();

                foreach (var record in records)
                {
                    if (record.Count == 0)
                    {
                        errors.Add("Empty record found");
                        continue;
                    }

                    // Build INSERT statement
                    var columns = record.Keys.ToList();
                    var columnNames = string.Join(", ", columns.Select(c => $"[{c}]"));
                    var paramNames = string.Join(", ", columns.Select((c, i) => $"@p{i}"));

                    var insertQuery = $"INSERT INTO [{schemaName}].[{tableName}] ({columnNames}) VALUES ({paramNames})";

                    using var command = new SqlCommand(insertQuery, connection, transaction);
                    command.CommandTimeout = config.QuerySettings.TimeoutSeconds;

                    // Add parameters
                    for (int i = 0; i < columns.Count; i++)
                    {
                        var value = record[columns[i]];
                        if (value is JsonElement jsonValue)
                        {
                            // Convert JsonElement to appropriate type
                            value = ConvertJsonElement(jsonValue);
                        }
                        command.Parameters.AddWithValue($"@p{i}", value ?? DBNull.Value);
                    }

                    try
                    {
                        await command.ExecuteNonQueryAsync();
                        insertedCount++;
                    }
                    catch (SqlException ex)
                    {
                        errors.Add($"Row {insertedCount + 1}: {ex.Message}");
                        if (useTransaction)
                        {
                            throw; // Rollback entire transaction
                        }
                    }
                }

                if (useTransaction && insertedCount > 0)
                {
                    await transaction!.CommitAsync();
                }

                // Audit logging
                if (config.Security.AuditOperations && insertedCount > 0)
                {
                    await transactionService.LogOperationAsync(
                        "INSERT",
                        $"{schemaName}.{tableName}",
                        $"Inserted {insertedCount} records");
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    insertedCount = insertedCount,
                    errors = errors.Count > 0 ? errors : null
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                if (useTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal :
                                   element.TryGetInt64(out var longVal) ? longVal :
                                   element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }
}