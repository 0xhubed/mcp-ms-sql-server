using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Data;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class SchemaTools
{
    [McpServerTool]
    [Description("Get database schema information for the configured schema")]
    public static async Task<string> GetSchemaInfo(
        [Description("Optional table name to get specific table info")] string? tableName,
        DatabaseService databaseService,
        ConfigService configService)
    {
        try
        {
            var config = configService.CurrentConfig;
            var schemaName = config.AllowedSchema;

            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            if (!string.IsNullOrEmpty(tableName))
            {
                // Get specific table info
                return await GetTableSchema(connection, schemaName, tableName);
            }
            else
            {
                // Get all tables and views in schema
                return await GetAllTablesInSchema(connection, schemaName);
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static async Task<string> GetAllTablesInSchema(SqlConnection connection, string schemaName)
    {
        var query = @"
            SELECT 
                t.TABLE_NAME,
                t.TABLE_TYPE,
                CAST(p.rows AS BIGINT) as ROW_COUNT
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.tables st ON st.name = t.TABLE_NAME AND SCHEMA_NAME(st.schema_id) = t.TABLE_SCHEMA
            LEFT JOIN sys.partitions p ON p.object_id = st.object_id AND p.index_id IN (0, 1)
            WHERE t.TABLE_SCHEMA = @schemaName
            ORDER BY t.TABLE_TYPE, t.TABLE_NAME";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@schemaName", schemaName);

        using var reader = await command.ExecuteReaderAsync();
        var tables = new List<object>();

        while (await reader.ReadAsync())
        {
            tables.Add(new
            {
                name = reader.GetString(0),
                type = reader.GetString(1),
                rowCount = reader.IsDBNull(2) ? null : (long?)reader.GetInt64(2)
            });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            schema = schemaName,
            tableCount = tables.Count,
            tables = tables
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static async Task<string> GetTableSchema(SqlConnection connection, string schemaName, string tableName)
    {
        var columnsQuery = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END AS IS_PRIMARY_KEY
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                    AND tc.TABLE_NAME = ku.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    AND tc.TABLE_SCHEMA = @schemaName
                    AND tc.TABLE_NAME = @tableName
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schemaName AND c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION";

        using var command = new SqlCommand(columnsQuery, connection);
        command.Parameters.AddWithValue("@schemaName", schemaName);
        command.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await command.ExecuteReaderAsync();
        var columns = new List<object>();

        while (await reader.ReadAsync())
        {
            columns.Add(new
            {
                name = reader.GetString(0),
                dataType = reader.GetString(1),
                maxLength = reader.IsDBNull(2) ? null : (int?)reader.GetInt32(2),
                precision = reader.IsDBNull(3) ? null : (byte?)reader.GetByte(3),
                scale = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4),
                isNullable = reader.GetString(5) == "YES",
                defaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                isPrimaryKey = reader.GetString(7) == "YES"
            });
        }

        // Get foreign keys
        var foreignKeysQuery = @"
            SELECT 
                fk.name AS FK_NAME,
                c.name AS COLUMN_NAME,
                OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS REFERENCED_SCHEMA,
                OBJECT_NAME(fk.referenced_object_id) AS REFERENCED_TABLE,
                rc.name AS REFERENCED_COLUMN
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.columns c ON fkc.parent_column_id = c.column_id AND fkc.parent_object_id = c.object_id
            INNER JOIN sys.columns rc ON fkc.referenced_column_id = rc.column_id AND fkc.referenced_object_id = rc.object_id
            WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) = @schemaName
                AND OBJECT_NAME(fk.parent_object_id) = @tableName";

        command.CommandText = foreignKeysQuery;
        using var fkReader = await command.ExecuteReaderAsync();
        var foreignKeys = new List<object>();

        while (await fkReader.ReadAsync())
        {
            foreignKeys.Add(new
            {
                name = fkReader.GetString(0),
                column = fkReader.GetString(1),
                referencedSchema = fkReader.GetString(2),
                referencedTable = fkReader.GetString(3),
                referencedColumn = fkReader.GetString(4)
            });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            schema = schemaName,
            table = tableName,
            columnCount = columns.Count,
            columns = columns,
            foreignKeys = foreignKeys
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}