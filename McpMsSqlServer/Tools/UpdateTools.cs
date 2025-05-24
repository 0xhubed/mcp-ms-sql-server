using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class UpdateTools
{
    [McpServerTool]
    [Description("Update existing records in a table within the configured schema")]
    public static async Task<string> UpdateRecords(
        [Description("The table name to update")] string tableName,
        [Description("JSON object with column-value pairs to update")] string updateData,
        [Description("WHERE clause conditions (required for safety)")] string whereClause,
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

            // Validate WHERE clause requirement
            if (config.Security.RequireWhereClause && string.IsNullOrWhiteSpace(whereClause))
            {
                return JsonSerializer.Serialize(new { error = "WHERE clause is required for UPDATE operations in this configuration" });
            }

            var schemaName = config.AllowedSchema;

            // Validate table access
            var validationResult = await securityService.ValidateTableAccessAsync(schemaName, tableName);
            if (!validationResult.IsValid)
            {
                return JsonSerializer.Serialize(new { error = validationResult.ErrorMessage });
            }

            // Parse update data
            Dictionary<string, object?> updateValues;
            try
            {
                updateValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(updateData) ?? new();
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(new { error = $"Invalid JSON format for update data: {ex.Message}" });
            }

            if (updateValues.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "No update values provided" });
            }

            // Validate WHERE clause
            var whereValidation = await securityService.ValidateWhereClauseAsync(whereClause);
            if (!whereValidation.IsValid)
            {
                return JsonSerializer.Serialize(new { error = whereValidation.ErrorMessage });
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
                // First, count affected rows
                var countQuery = $"SELECT COUNT(*) FROM [{schemaName}].[{tableName}] WHERE {whereClause}";
                using var countCommand = new SqlCommand(countQuery, connection, transaction);
                var affectedRowsCount = (int)await countCommand.ExecuteScalarAsync();

                // Check max rows limit
                if (affectedRowsCount > config.Security.MaxRowsPerUpdate)
                {
                    return JsonSerializer.Serialize(new { 
                        error = $"Update would affect {affectedRowsCount} rows, which exceeds the maximum allowed ({config.Security.MaxRowsPerUpdate})" 
                    });
                }

                // Build UPDATE statement
                var setClause = new StringBuilder();
                var parameters = new List<SqlParameter>();
                var index = 0;

                foreach (var kvp in updateValues)
                {
                    if (setClause.Length > 0)
                        setClause.Append(", ");
                    
                    setClause.Append($"[{kvp.Key}] = @p{index}");
                    
                    var value = kvp.Value;
                    if (value is JsonElement jsonValue)
                    {
                        value = ConvertJsonElement(jsonValue);
                    }
                    
                    parameters.Add(new SqlParameter($"@p{index}", value ?? DBNull.Value));
                    index++;
                }

                var updateQuery = $"UPDATE [{schemaName}].[{tableName}] SET {setClause} WHERE {whereClause}";

                using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                updateCommand.CommandTimeout = config.QuerySettings.TimeoutSeconds;
                updateCommand.Parameters.AddRange(parameters.ToArray());

                var updatedRows = await updateCommand.ExecuteNonQueryAsync();

                if (useTransaction)
                {
                    await transaction!.CommitAsync();
                }

                // Audit logging
                if (config.Security.AuditOperations && updatedRows > 0)
                {
                    await transactionService.LogOperationAsync(
                        "UPDATE",
                        $"{schemaName}.{tableName}",
                        $"Updated {updatedRows} records. WHERE: {whereClause}");
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    updatedRows = updatedRows,
                    whereClause = whereClause
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