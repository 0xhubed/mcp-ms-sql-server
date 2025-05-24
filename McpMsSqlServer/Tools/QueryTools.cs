using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Data;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class QueryTools
{
    [McpServerTool]
    [Description("Execute a SQL SELECT query against the configured schema")]
    public static async Task<string> ExecuteQuery(
        [Description("The SQL SELECT query to execute")] string sqlQuery,
        [Description("Maximum number of rows to return (default: 100)")] int maxRows,
        DatabaseService databaseService,
        SecurityService securityService,
        ConfigService configService)
    {
        try
        {
            // Validate query
            var validationResult = await securityService.ValidateQueryAsync(sqlQuery);
            if (!validationResult.IsValid)
            {
                return JsonSerializer.Serialize(new { error = validationResult.ErrorMessage });
            }

            // Get current configuration
            var config = configService.CurrentConfig;
            if (!config.Permissions.AllowRead)
            {
                return JsonSerializer.Serialize(new { error = "Read operations are not allowed for this configuration" });
            }

            // Apply max rows limit
            maxRows = maxRows > 0 ? Math.Min(maxRows, config.Security.MaxRowsPerQuery) : config.Security.MaxRowsPerQuery;

            // Execute query
            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(sqlQuery, connection);
            command.CommandTimeout = config.QuerySettings.TimeoutSeconds;

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<Dictionary<string, object?>>();
            var columnNames = new List<string>();

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            // Read data
            int rowCount = 0;
            while (await reader.ReadAsync() && rowCount < maxRows)
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnNames[i]] = value;
                }
                results.Add(row);
                rowCount++;
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                rowCount = results.Count,
                columns = columnNames,
                data = results,
                truncated = rowCount >= maxRows
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}