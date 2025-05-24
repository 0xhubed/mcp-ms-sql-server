using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Data;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class TableInfoTools
{
    [McpServerTool]
    [Description("Get detailed table metadata and sample data")]
    public static async Task<string> GetTableInfo(
        [Description("The table name to inspect")] string tableName,
        [Description("Number of sample rows to return (default: 10)")] int sampleRows,
        DatabaseService databaseService,
        ConfigService configService,
        SecurityService securityService)
    {
        try
        {
            var config = configService.CurrentConfig;
            var schemaName = config.AllowedSchema;

            // Validate table access
            var validationResult = await securityService.ValidateTableAccessAsync(schemaName, tableName);
            if (!validationResult.IsValid)
            {
                return JsonSerializer.Serialize(new { error = validationResult.ErrorMessage });
            }

            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            // Get row count
            var countQuery = @"
                SELECT COUNT(*) 
                FROM [{0}].[{1}]";
            
            using var countCommand = new SqlCommand(string.Format(countQuery, schemaName, tableName), connection);
            var totalRows = (int)await countCommand.ExecuteScalarAsync();

            // Get table metadata
            var metadataQuery = @"
                SELECT 
                    t.create_date,
                    t.modify_date,
                    CAST(SUM(p.rows) AS BIGINT) as row_count,
                    CAST(SUM(a.total_pages) * 8 AS BIGINT) as size_kb
                FROM sys.tables t
                INNER JOIN sys.partitions p ON t.object_id = p.object_id
                INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
                WHERE SCHEMA_NAME(t.schema_id) = @schemaName AND t.name = @tableName
                GROUP BY t.create_date, t.modify_date";

            using var metadataCommand = new SqlCommand(metadataQuery, connection);
            metadataCommand.Parameters.AddWithValue("@schemaName", schemaName);
            metadataCommand.Parameters.AddWithValue("@tableName", tableName);

            var metadata = new Dictionary<string, object>();
            using (var reader = await metadataCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    metadata["createDate"] = reader.GetDateTime(0);
                    metadata["modifyDate"] = reader.GetDateTime(1);
                    metadata["rowCount"] = reader.GetInt64(2);
                    metadata["sizeKB"] = reader.GetInt64(3);
                }
            }

            // Get column statistics
            var statsQuery = @"
                SELECT 
                    c.name AS column_name,
                    t.name AS data_type,
                    c.max_length,
                    c.precision,
                    c.scale,
                    c.is_nullable,
                    c.is_identity
                FROM sys.columns c
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID(@fullTableName)
                ORDER BY c.column_id";

            using var statsCommand = new SqlCommand(statsQuery, connection);
            statsCommand.Parameters.AddWithValue("@fullTableName", $"{schemaName}.{tableName}");

            var columnStats = new List<object>();
            using (var reader = await statsCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columnStats.Add(new
                    {
                        columnName = reader.GetString(0),
                        dataType = reader.GetString(1),
                        maxLength = reader.GetInt16(2),
                        precision = reader.GetByte(3),
                        scale = reader.GetByte(4),
                        isNullable = reader.GetBoolean(5),
                        isIdentity = reader.GetBoolean(6)
                    });
                }
            }

            // Get sample data
            sampleRows = sampleRows > 0 ? Math.Min(sampleRows, 100) : 10;
            var sampleQuery = $"SELECT TOP {sampleRows} * FROM [{schemaName}].[{tableName}]";
            
            using var sampleCommand = new SqlCommand(sampleQuery, connection);
            using var sampleReader = await sampleCommand.ExecuteReaderAsync();

            var sampleData = new List<Dictionary<string, object?>>();
            var columns = new List<string>();

            // Get column names
            for (int i = 0; i < sampleReader.FieldCount; i++)
            {
                columns.Add(sampleReader.GetName(i));
            }

            // Read sample data
            while (await sampleReader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < sampleReader.FieldCount; i++)
                {
                    row[columns[i]] = sampleReader.IsDBNull(i) ? null : sampleReader.GetValue(i);
                }
                sampleData.Add(row);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                schema = schemaName,
                table = tableName,
                metadata = metadata,
                totalRows = totalRows,
                columnStats = columnStats,
                sampleDataCount = sampleData.Count,
                sampleData = sampleData
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}