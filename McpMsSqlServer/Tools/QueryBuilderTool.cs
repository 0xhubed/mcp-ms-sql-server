using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using McpMsSqlServer.Services;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class QueryBuilderTool
{
    [McpServerTool]
    [Description("Generate SQL queries based on natural language descriptions")]
    public static async Task<string> BuildQuery(
        string description,
        string? tables,
        string queryType,
        DatabaseService databaseService,
        ConfigService configService,
        SecurityService securityService)
    {
        try
        {
            // Get current configuration
            if (configService.CurrentConfig == null)
            {
                return JsonSerializer.Serialize(new { error = "No configuration loaded" });
            }

            var config = configService.CurrentConfig;
            
            // Default values for optional parameters
            tables ??= null;
            queryType ??= "SELECT";
            
            // Validate query type
            queryType = queryType.ToUpper();
            if (!new[] { "SELECT", "INSERT", "UPDATE", "DELETE" }.Contains(queryType))
            {
                return JsonSerializer.Serialize(new { error = "Invalid query type. Must be SELECT, INSERT, UPDATE, or DELETE" });
            }

            // Parse the description to identify key components
            var queryBuilder = new StringBuilder();
            var descriptionLower = description.ToLower();
            
            // Parse table names if provided
            var tableList = tables?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                  .ToList() ?? new List<string>();

            // If no tables provided, try to extract from description
            if (!tableList.Any())
            {
                tableList = await ExtractTableNamesFromDescription(databaseService, config.AllowedSchema, descriptionLower);
            }

            if (!tableList.Any())
            {
                return JsonSerializer.Serialize(new { 
                    error = "Could not identify tables. Please specify table names explicitly.",
                    suggestion = "Use the 'tables' parameter to specify which tables to query"
                });
            }

            // Build query based on type
            switch (queryType)
            {
                case "SELECT":
                    queryBuilder = BuildSelectQuery(descriptionLower, tableList, config.AllowedSchema);
                    break;
                case "INSERT":
                    queryBuilder = BuildInsertTemplate(descriptionLower, tableList[0], config.AllowedSchema);
                    break;
                case "UPDATE":
                    queryBuilder = BuildUpdateTemplate(descriptionLower, tableList[0], config.AllowedSchema);
                    break;
                case "DELETE":
                    queryBuilder = BuildDeleteTemplate(descriptionLower, tableList[0], config.AllowedSchema);
                    break;
            }

            // Get column information for the tables
            var tableInfo = new Dictionary<string, List<ColumnInfo>>();
            foreach (var table in tableList)
            {
                var columns = await GetTableColumns(databaseService, config.AllowedSchema, table);
                if (columns.Any())
                {
                    tableInfo[table] = columns;
                }
            }

            // Generate join suggestions if multiple tables
            var joinSuggestions = new List<string>();
            if (tableList.Count > 1 && queryType == "SELECT")
            {
                joinSuggestions = await GenerateJoinSuggestions(databaseService, config.AllowedSchema, tableList);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                generatedQuery = queryBuilder.ToString(),
                tables = tableList,
                tableInfo = tableInfo,
                joinSuggestions = joinSuggestions,
                notes = GenerateQueryNotes(queryType, descriptionLower),
                examples = GenerateQueryExamples(queryType, tableList[0], config.AllowedSchema)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Query builder error: {ex.Message}" });
        }
    }

    private static async Task<List<string>> ExtractTableNamesFromDescription(
        DatabaseService databaseService, 
        string schema, 
        string description)
    {
        var tables = new List<string>();
        
        // Get all table names from the schema
        using var connection = databaseService.CreateConnection();
        await connection.OpenAsync();
        
        var query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = @schema 
            AND TABLE_TYPE = 'BASE TABLE'";
        
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@schema", schema);
        
        using var reader = await command.ExecuteReaderAsync();
        var allTables = new List<string>();
        while (await reader.ReadAsync())
        {
            allTables.Add(reader.GetString(0));
        }

        // Check if any table names appear in the description
        foreach (var table in allTables)
        {
            if (description.Contains(table.ToLower()) || 
                description.Contains(table.Replace("_", " ").ToLower()))
            {
                tables.Add(table);
            }
        }

        return tables;
    }

    private static async Task<List<ColumnInfo>> GetTableColumns(
        DatabaseService databaseService,
        string schema,
        string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        using var connection = databaseService.CreateConnection();
        await connection.OpenAsync();
        
        var query = @"
            SELECT 
                COLUMN_NAME,
                DATA_TYPE,
                IS_NULLABLE,
                CHARACTER_MAXIMUM_LENGTH,
                NUMERIC_PRECISION,
                NUMERIC_SCALE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION";
        
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", tableName);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                NumericPrecision = reader.IsDBNull(4) ? null : reader.GetByte(4),
                NumericScale = reader.IsDBNull(5) ? null : reader.GetInt32(5)
            });
        }

        return columns;
    }

    private static async Task<List<string>> GenerateJoinSuggestions(
        DatabaseService databaseService,
        string schema,
        List<string> tables)
    {
        var suggestions = new List<string>();
        
        using var connection = databaseService.CreateConnection();
        await connection.OpenAsync();
        
        // Find foreign key relationships
        var query = @"
            SELECT 
                fk.TABLE_NAME AS FK_Table,
                fk.COLUMN_NAME AS FK_Column,
                pk.TABLE_NAME AS PK_Table,
                pk.COLUMN_NAME AS PK_Column
            FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE fk 
                ON rc.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE pk 
                ON rc.UNIQUE_CONSTRAINT_NAME = pk.CONSTRAINT_NAME
            WHERE fk.TABLE_SCHEMA = @schema 
            AND pk.TABLE_SCHEMA = @schema
            AND fk.TABLE_NAME IN (@table1, @table2)
            AND pk.TABLE_NAME IN (@table1, @table2)";
        
        for (int i = 0; i < tables.Count - 1; i++)
        {
            for (int j = i + 1; j < tables.Count; j++)
            {
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table1", tables[i]);
                command.Parameters.AddWithValue("@table2", tables[j]);
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var fkTable = reader.GetString(0);
                    var fkColumn = reader.GetString(1);
                    var pkTable = reader.GetString(2);
                    var pkColumn = reader.GetString(3);
                    
                    suggestions.Add($"JOIN {schema}.{pkTable} ON {schema}.{fkTable}.{fkColumn} = {schema}.{pkTable}.{pkColumn}");
                }
            }
        }

        return suggestions;
    }

    private static StringBuilder BuildSelectQuery(string description, List<string> tables, string schema)
    {
        var query = new StringBuilder();
        
        // Determine columns
        var columns = "*";
        if (description.Contains("count"))
        {
            columns = "COUNT(*)";
        }
        else if (description.Contains("sum"))
        {
            columns = "SUM(amount)"; // placeholder
        }
        else if (description.Contains("average") || description.Contains("avg"))
        {
            columns = "AVG(value)"; // placeholder
        }

        query.AppendLine($"SELECT {columns}");
        query.AppendLine($"FROM {schema}.{tables[0]}");
        
        // Add WHERE clause components based on description
        var whereConditions = new List<string>();
        
        if (description.Contains("last") && (description.Contains("day") || description.Contains("month") || description.Contains("year")))
        {
            if (description.Contains("30 day"))
                whereConditions.Add("date_column >= DATEADD(day, -30, GETDATE())");
            else if (description.Contains("7 day") || description.Contains("week"))
                whereConditions.Add("date_column >= DATEADD(day, -7, GETDATE())");
            else if (description.Contains("month"))
                whereConditions.Add("date_column >= DATEADD(month, -1, GETDATE())");
            else if (description.Contains("year"))
                whereConditions.Add("date_column >= DATEADD(year, -1, GETDATE())");
        }
        
        if (description.Contains("active"))
        {
            whereConditions.Add("is_active = 1");
        }
        
        if (description.Contains("greater than") || description.Contains("more than"))
        {
            whereConditions.Add("value_column > ?");
        }
        
        if (description.Contains("less than"))
        {
            whereConditions.Add("value_column < ?");
        }

        if (whereConditions.Any())
        {
            query.AppendLine("WHERE " + string.Join(" AND ", whereConditions));
        }

        // Add GROUP BY if aggregation
        if (columns.Contains("COUNT") || columns.Contains("SUM") || columns.Contains("AVG"))
        {
            query.AppendLine("-- Add GROUP BY clause if needed");
        }

        // Add ORDER BY based on description
        if (description.Contains("top") || description.Contains("highest"))
        {
            query.AppendLine("ORDER BY value_column DESC");
        }
        else if (description.Contains("recent") || description.Contains("latest"))
        {
            query.AppendLine("ORDER BY date_column DESC");
        }

        return query;
    }

    private static StringBuilder BuildInsertTemplate(string description, string table, string schema)
    {
        var query = new StringBuilder();
        query.AppendLine($"INSERT INTO {schema}.{table}");
        query.AppendLine("(column1, column2, column3) -- Replace with actual columns");
        query.AppendLine("VALUES");
        query.AppendLine("(@value1, @value2, @value3); -- Use parameters for safety");
        
        return query;
    }

    private static StringBuilder BuildUpdateTemplate(string description, string table, string schema)
    {
        var query = new StringBuilder();
        query.AppendLine($"UPDATE {schema}.{table}");
        query.AppendLine("SET");
        query.AppendLine("    column1 = @value1, -- Replace with actual columns");
        query.AppendLine("    column2 = @value2");
        query.AppendLine("WHERE");
        query.AppendLine("    id = @id; -- Always include WHERE clause for safety");
        
        return query;
    }

    private static StringBuilder BuildDeleteTemplate(string description, string table, string schema)
    {
        var query = new StringBuilder();
        query.AppendLine($"DELETE FROM {schema}.{table}");
        query.AppendLine("WHERE");
        query.AppendLine("    id = @id; -- Always include WHERE clause for safety");
        
        if (description.Contains("soft delete"))
        {
            query.Clear();
            query.AppendLine($"-- Soft delete suggestion:");
            query.AppendLine($"UPDATE {schema}.{table}");
            query.AppendLine("SET is_deleted = 1, deleted_date = GETDATE()");
            query.AppendLine("WHERE id = @id;");
        }
        
        return query;
    }

    private static List<string> GenerateQueryNotes(string queryType, string description)
    {
        var notes = new List<string>();
        
        switch (queryType)
        {
            case "SELECT":
                notes.Add("Replace column names and table references with actual values");
                notes.Add("Adjust date calculations based on your date column names");
                if (description.Contains("join"))
                    notes.Add("Review join conditions for correctness");
                break;
            case "INSERT":
                notes.Add("Always use parameterized values to prevent SQL injection");
                notes.Add("Ensure all required columns are included");
                notes.Add("Consider using transactions for bulk inserts");
                break;
            case "UPDATE":
                notes.Add("WHERE clause is mandatory for safety");
                notes.Add("Consider impact before running mass updates");
                notes.Add("Test with SELECT first to verify affected rows");
                break;
            case "DELETE":
                notes.Add("WHERE clause is mandatory for safety");
                notes.Add("Consider soft deletes instead of hard deletes");
                notes.Add("Always backup before mass deletions");
                break;
        }
        
        return notes;
    }

    private static Dictionary<string, string> GenerateQueryExamples(string queryType, string table, string schema)
    {
        var examples = new Dictionary<string, string>();
        
        switch (queryType)
        {
            case "SELECT":
                examples["Basic"] = $"SELECT * FROM {schema}.{table} WHERE is_active = 1";
                examples["With Join"] = $"SELECT t1.*, t2.name FROM {schema}.{table} t1 JOIN {schema}.related_table t2 ON t1.id = t2.{table}_id";
                examples["Aggregation"] = $"SELECT status, COUNT(*) as count FROM {schema}.{table} GROUP BY status";
                break;
            case "INSERT":
                examples["Single"] = $"INSERT INTO {schema}.{table} (name, email) VALUES (@name, @email)";
                examples["Multiple"] = $"INSERT INTO {schema}.{table} (name, email) VALUES (@name1, @email1), (@name2, @email2)";
                break;
            case "UPDATE":
                examples["Single"] = $"UPDATE {schema}.{table} SET status = @status WHERE id = @id";
                examples["Conditional"] = $"UPDATE {schema}.{table} SET last_updated = GETDATE() WHERE status = 'pending' AND created_date < DATEADD(day, -7, GETDATE())";
                break;
            case "DELETE":
                examples["Single"] = $"DELETE FROM {schema}.{table} WHERE id = @id";
                examples["Soft Delete"] = $"UPDATE {schema}.{table} SET is_deleted = 1, deleted_date = GETDATE() WHERE id = @id";
                break;
        }
        
        return examples;
    }

    private class ColumnInfo
    {
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public bool IsNullable { get; set; }
        public int? MaxLength { get; set; }
        public byte? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
    }
}