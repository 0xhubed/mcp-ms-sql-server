using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Data;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class DeleteTools
{
    [McpServerTool]
    [Description("Delete records from a table within the configured schema")]
    public static async Task<string> DeleteRecords(
        [Description("The table name to delete from")] string tableName,
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
            if (!config.Permissions.AllowDelete)
            {
                return JsonSerializer.Serialize(new { error = "Delete operations are not allowed for this configuration" });
            }

            // Validate WHERE clause requirement
            if (string.IsNullOrWhiteSpace(whereClause))
            {
                return JsonSerializer.Serialize(new { error = "WHERE clause is required for DELETE operations to prevent accidental mass deletion" });
            }

            var schemaName = config.AllowedSchema;

            // Validate table access
            var validationResult = await securityService.ValidateTableAccessAsync(schemaName, tableName);
            if (!validationResult.IsValid)
            {
                return JsonSerializer.Serialize(new { error = validationResult.ErrorMessage });
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
                if (affectedRowsCount > config.Security.MaxRowsPerDelete)
                {
                    return JsonSerializer.Serialize(new { 
                        error = $"Delete would affect {affectedRowsCount} rows, which exceeds the maximum allowed ({config.Security.MaxRowsPerDelete})" 
                    });
                }

                // Confirmation for large deletions
                if (affectedRowsCount > 10)
                {
                    // In a real implementation, this might require user confirmation
                    // For now, we'll just add a warning to the response
                }

                // Get sample of rows to be deleted for audit trail
                var sampleQuery = $"SELECT TOP 5 * FROM [{schemaName}].[{tableName}] WHERE {whereClause}";
                var deletedSample = new List<Dictionary<string, object?>>();
                
                using (var sampleCommand = new SqlCommand(sampleQuery, connection, transaction))
                {
                    using var reader = await sampleCommand.ExecuteReaderAsync();
                    var columns = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }

                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        deletedSample.Add(row);
                    }
                }

                // Execute delete
                var deleteQuery = $"DELETE FROM [{schemaName}].[{tableName}] WHERE {whereClause}";
                using var deleteCommand = new SqlCommand(deleteQuery, connection, transaction);
                deleteCommand.CommandTimeout = config.QuerySettings.TimeoutSeconds;

                var deletedRows = await deleteCommand.ExecuteNonQueryAsync();

                if (useTransaction)
                {
                    await transaction!.CommitAsync();
                }

                // Audit logging
                if (config.Security.AuditOperations && deletedRows > 0)
                {
                    await transactionService.LogOperationAsync(
                        "DELETE",
                        $"{schemaName}.{tableName}",
                        $"Deleted {deletedRows} records. WHERE: {whereClause}",
                        JsonSerializer.Serialize(deletedSample));
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    deletedRows = deletedRows,
                    whereClause = whereClause,
                    warning = affectedRowsCount > 10 ? $"Deleted {deletedRows} rows. Consider backing up data before large deletions." : null,
                    sampleDeleted = deletedRows > 0 && deletedRows <= 5 ? deletedSample : null
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
}