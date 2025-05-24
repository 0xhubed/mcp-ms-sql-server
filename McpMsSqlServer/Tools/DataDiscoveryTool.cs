using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using McpMsSqlServer.Services;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class DataDiscoveryTool
{
    [McpServerTool]
    [Description("Search for tables and columns by name patterns and discover relationships")]
    public static async Task<string> DiscoverData(
        string searchPattern,
        string searchType,
        bool includeDataProfile,
        DatabaseService databaseService,
        ConfigService configService)
    {
        try
        {
            if (configService.CurrentConfig == null)
            {
                return JsonSerializer.Serialize(new { error = "No configuration loaded" });
            }

            var config = configService.CurrentConfig;
            
            // Default values
            searchType ??= "both";

            var discoveryResult = new DataDiscoveryResult();

            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            // Search based on type
            searchType = searchType.ToLower();
            if (searchType == "table" || searchType == "both")
            {
                discoveryResult.Tables = await SearchTables(connection, config.AllowedSchema, searchPattern);
            }

            if (searchType == "column" || searchType == "both")
            {
                discoveryResult.Columns = await SearchColumns(connection, config.AllowedSchema, searchPattern);
            }

            // Get relationships for found tables
            var allTables = discoveryResult.Tables.Select(t => t.TableName)
                .Union(discoveryResult.Columns.Select(c => c.TableName))
                .Distinct()
                .ToList();

            if (allTables.Any())
            {
                discoveryResult.Relationships = await DiscoverRelationships(connection, config.AllowedSchema, allTables);
            }

            // Get data profiling if requested
            if (includeDataProfile && allTables.Any())
            {
                discoveryResult.DataProfiles = await ProfileData(connection, config.AllowedSchema, allTables.Take(10).ToList());
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                discovery = discoveryResult,
                summary = GenerateDiscoverySummary(discoveryResult)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Data discovery error: {ex.Message}" });
        }
    }

    [McpServerTool]
    [Description("Find all relationships and dependencies for specified tables")]
    public static async Task<string> AnalyzeTableRelationships(
        string tableNames,
        bool includeReferencingTables,
        bool includeReferencedTables,
        DatabaseService databaseService,
        ConfigService configService)
    {
        try
        {
            if (configService.CurrentConfig == null)
            {
                return JsonSerializer.Serialize(new { error = "No configuration loaded" });
            }

            var config = configService.CurrentConfig;
            
            // Default values are already set by parameters

            var tables = tableNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            
            var relationshipAnalysis = new RelationshipAnalysis
            {
                AnalyzedTables = tables,
                ForeignKeys = new List<ForeignKeyInfo>(),
                ReferencingTables = new Dictionary<string, List<string>>(),
                ReferencedTables = new Dictionary<string, List<string>>(),
                DependencyTree = new Dictionary<string, DependencyNode>()
            };

            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            // Get all foreign keys for the tables
            relationshipAnalysis.ForeignKeys = await GetForeignKeys(connection, config.AllowedSchema, tables);

            // Build referencing/referenced tables maps
            foreach (var table in tables)
            {
                if (includeReferencingTables)
                {
                    relationshipAnalysis.ReferencingTables[table] = await GetReferencingTables(connection, config.AllowedSchema, table);
                }

                if (includeReferencedTables)
                {
                    relationshipAnalysis.ReferencedTables[table] = await GetReferencedTables(connection, config.AllowedSchema, table);
                }
            }

            // Build dependency tree
            foreach (var table in tables)
            {
                relationshipAnalysis.DependencyTree[table] = await BuildDependencyTree(connection, config.AllowedSchema, table, new HashSet<string>());
            }

            // Generate relationship diagram
            var diagram = GenerateRelationshipDiagram(relationshipAnalysis);

            return JsonSerializer.Serialize(new
            {
                success = true,
                analysis = relationshipAnalysis,
                diagram = diagram,
                suggestions = GenerateRelationshipSuggestions(relationshipAnalysis)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Relationship analysis error: {ex.Message}" });
        }
    }

    [McpServerTool]
    [Description("Profile data quality and statistics for specified tables")]
    public static async Task<string> ProfileDataQuality(
        string tableNames,
        int sampleSize,
        DatabaseService databaseService,
        ConfigService configService)
    {
        try
        {
            if (configService.CurrentConfig == null)
            {
                return JsonSerializer.Serialize(new { error = "No configuration loaded" });
            }

            var config = configService.CurrentConfig;
            
            // Default value
            sampleSize = sampleSize > 0 ? sampleSize : 1000;

            var tables = tableNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            
            var qualityProfiles = new List<DataQualityProfile>();

            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            foreach (var table in tables)
            {
                var profile = await ProfileTableDataQuality(connection, config.AllowedSchema, table, sampleSize);
                if (profile != null)
                {
                    qualityProfiles.Add(profile);
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                profiles = qualityProfiles,
                summary = GenerateDataQualitySummary(qualityProfiles),
                recommendations = GenerateDataQualityRecommendations(qualityProfiles)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Data quality profiling error: {ex.Message}" });
        }
    }

    private static async Task<List<TableSearchResult>> SearchTables(SqlConnection connection, string schema, string pattern)
    {
        var results = new List<TableSearchResult>();
        
        var query = @"
            SELECT 
                t.TABLE_NAME,
                t.TABLE_TYPE,
                p.rows as RowCount,
                (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME) as ColumnCount
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.partitions p ON p.object_id = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME) AND p.index_id IN (0,1)
            WHERE t.TABLE_SCHEMA = @schema
            AND t.TABLE_NAME LIKE @pattern
            ORDER BY t.TABLE_NAME";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@pattern", pattern.Replace("*", "%"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new TableSearchResult
            {
                TableName = reader.GetString(0),
                TableType = reader.GetString(1),
                RowCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                ColumnCount = reader.GetInt32(3)
            });
        }

        return results;
    }

    private static async Task<List<ColumnSearchResult>> SearchColumns(SqlConnection connection, string schema, string pattern)
    {
        var results = new List<ColumnSearchResult>();
        
        var query = @"
            SELECT 
                c.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.COLUMN_DEFAULT
            FROM INFORMATION_SCHEMA.COLUMNS c
            INNER JOIN INFORMATION_SCHEMA.TABLES t ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
            WHERE c.TABLE_SCHEMA = @schema
            AND (c.COLUMN_NAME LIKE @pattern OR c.TABLE_NAME LIKE @pattern)
            ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@pattern", pattern.Replace("*", "%"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ColumnSearchResult
            {
                TableName = reader.GetString(0),
                ColumnName = reader.GetString(1),
                DataType = reader.GetString(2),
                IsNullable = reader.GetString(3) == "YES",
                MaxLength = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                NumericPrecision = reader.IsDBNull(5) ? null : reader.GetByte(5),
                NumericScale = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                DefaultValue = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return results;
    }

    private static async Task<List<TableRelationship>> DiscoverRelationships(SqlConnection connection, string schema, List<string> tables)
    {
        var relationships = new List<TableRelationship>();
        
        var query = @"
            SELECT 
                fk.name AS FK_Name,
                OBJECT_NAME(fk.parent_object_id) AS FK_Table,
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS FK_Column,
                OBJECT_NAME(fk.referenced_object_id) AS PK_Table,
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS PK_Column,
                fk.delete_referential_action_desc AS DeleteAction,
                fk.update_referential_action_desc AS UpdateAction
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            WHERE SCHEMA_NAME(fk.schema_id) = @schema
            AND (OBJECT_NAME(fk.parent_object_id) IN (SELECT value FROM STRING_SPLIT(@tables, ','))
                OR OBJECT_NAME(fk.referenced_object_id) IN (SELECT value FROM STRING_SPLIT(@tables, ',')))
            ORDER BY FK_Table, FK_Column";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@tables", string.Join(",", tables));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            relationships.Add(new TableRelationship
            {
                ConstraintName = reader.GetString(0),
                ForeignKeyTable = reader.GetString(1),
                ForeignKeyColumn = reader.GetString(2),
                PrimaryKeyTable = reader.GetString(3),
                PrimaryKeyColumn = reader.GetString(4),
                DeleteAction = reader.GetString(5),
                UpdateAction = reader.GetString(6)
            });
        }

        return relationships;
    }

    private static async Task<List<DataProfile>> ProfileData(SqlConnection connection, string schema, List<string> tables)
    {
        var profiles = new List<DataProfile>();

        foreach (var table in tables)
        {
            var profile = new DataProfile { TableName = table, ColumnProfiles = new List<ColumnProfile>() };
            
            // Get column information
            var columnsQuery = @"
                SELECT COLUMN_NAME, DATA_TYPE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                ORDER BY ORDINAL_POSITION";
            
            using var columnsCmd = new SqlCommand(columnsQuery, connection);
            columnsCmd.Parameters.AddWithValue("@schema", schema);
            columnsCmd.Parameters.AddWithValue("@table", table);
            
            var columns = new List<(string name, string type)>();
            using (var reader = await columnsCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columns.Add((reader.GetString(0), reader.GetString(1)));
                }
            }

            // Profile each column
            foreach (var (columnName, dataType) in columns)
            {
                var columnProfile = new ColumnProfile
                {
                    ColumnName = columnName,
                    DataType = dataType
                };

                // Get basic statistics
                var statsQuery = $@"
                    SELECT 
                        COUNT(*) as TotalRows,
                        COUNT(DISTINCT [{columnName}]) as DistinctValues,
                        COUNT(CASE WHEN [{columnName}] IS NULL THEN 1 END) as NullCount
                    FROM [{schema}].[{table}]";

                using var statsCmd = new SqlCommand(statsQuery, connection);
                using var statsReader = await statsCmd.ExecuteReaderAsync();
                
                if (await statsReader.ReadAsync())
                {
                    columnProfile.TotalRows = statsReader.GetInt32(0);
                    columnProfile.DistinctValues = statsReader.GetInt32(1);
                    columnProfile.NullCount = statsReader.GetInt32(2);
                    columnProfile.NullPercentage = columnProfile.TotalRows > 0 
                        ? (columnProfile.NullCount * 100.0 / columnProfile.TotalRows) 
                        : 0;
                }

                // Get min/max for numeric and date types
                if (IsNumericType(dataType) || IsDateType(dataType))
                {
                    var minMaxQuery = $@"
                        SELECT 
                            MIN([{columnName}]) as MinValue,
                            MAX([{columnName}]) as MaxValue
                        FROM [{schema}].[{table}]
                        WHERE [{columnName}] IS NOT NULL";

                    using var minMaxCmd = new SqlCommand(minMaxQuery, connection);
                    using var minMaxReader = await minMaxCmd.ExecuteReaderAsync();
                    
                    if (await minMaxReader.ReadAsync() && !minMaxReader.IsDBNull(0))
                    {
                        columnProfile.MinValue = minMaxReader.GetValue(0).ToString();
                        columnProfile.MaxValue = minMaxReader.GetValue(1).ToString();
                    }
                }

                profile.ColumnProfiles.Add(columnProfile);
            }

            profiles.Add(profile);
        }

        return profiles;
    }

    private static async Task<List<ForeignKeyInfo>> GetForeignKeys(SqlConnection connection, string schema, List<string> tables)
    {
        var foreignKeys = new List<ForeignKeyInfo>();
        
        var query = @"
            SELECT 
                fk.name AS ConstraintName,
                OBJECT_NAME(fk.parent_object_id) AS FKTable,
                STUFF((SELECT ', ' + COL_NAME(fkc2.parent_object_id, fkc2.parent_column_id)
                       FROM sys.foreign_key_columns fkc2
                       WHERE fkc2.constraint_object_id = fk.object_id
                       FOR XML PATH('')), 1, 2, '') AS FKColumns,
                OBJECT_NAME(fk.referenced_object_id) AS PKTable,
                STUFF((SELECT ', ' + COL_NAME(fkc2.referenced_object_id, fkc2.referenced_column_id)
                       FROM sys.foreign_key_columns fkc2
                       WHERE fkc2.constraint_object_id = fk.object_id
                       FOR XML PATH('')), 1, 2, '') AS PKColumns
            FROM sys.foreign_keys fk
            WHERE SCHEMA_NAME(fk.schema_id) = @schema
            AND OBJECT_NAME(fk.parent_object_id) IN (SELECT value FROM STRING_SPLIT(@tables, ','))
            ORDER BY FKTable";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@tables", string.Join(",", tables));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = reader.GetString(0),
                ForeignKeyTable = reader.GetString(1),
                ForeignKeyColumns = reader.GetString(2),
                ReferencedTable = reader.GetString(3),
                ReferencedColumns = reader.GetString(4)
            });
        }

        return foreignKeys;
    }

    private static async Task<List<string>> GetReferencingTables(SqlConnection connection, string schema, string table)
    {
        var referencingTables = new List<string>();
        
        var query = @"
            SELECT DISTINCT OBJECT_NAME(fk.parent_object_id) AS ReferencingTable
            FROM sys.foreign_keys fk
            WHERE SCHEMA_NAME(fk.schema_id) = @schema
            AND OBJECT_NAME(fk.referenced_object_id) = @table
            ORDER BY ReferencingTable";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            referencingTables.Add(reader.GetString(0));
        }

        return referencingTables;
    }

    private static async Task<List<string>> GetReferencedTables(SqlConnection connection, string schema, string table)
    {
        var referencedTables = new List<string>();
        
        var query = @"
            SELECT DISTINCT OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
            FROM sys.foreign_keys fk
            WHERE SCHEMA_NAME(fk.schema_id) = @schema
            AND OBJECT_NAME(fk.parent_object_id) = @table
            ORDER BY ReferencedTable";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            referencedTables.Add(reader.GetString(0));
        }

        return referencedTables;
    }

    private static async Task<DependencyNode> BuildDependencyTree(SqlConnection connection, string schema, string table, HashSet<string> visited)
    {
        if (visited.Contains(table))
        {
            return new DependencyNode { TableName = table, IsCircular = true };
        }

        visited.Add(table);
        
        var node = new DependencyNode
        {
            TableName = table,
            Dependencies = new List<DependencyNode>()
        };

        var referencedTables = await GetReferencedTables(connection, schema, table);
        foreach (var refTable in referencedTables)
        {
            var childNode = await BuildDependencyTree(connection, schema, refTable, new HashSet<string>(visited));
            node.Dependencies.Add(childNode);
        }

        return node;
    }

    private static async Task<DataQualityProfile?> ProfileTableDataQuality(SqlConnection connection, string schema, string table, int sampleSize)
    {
        try
        {
            var profile = new DataQualityProfile
            {
                TableName = table,
                ColumnQualityMetrics = new List<ColumnQualityMetric>()
            };

            // Get total row count
            var countQuery = $"SELECT COUNT(*) FROM [{schema}].[{table}]";
            using var countCmd = new SqlCommand(countQuery, connection);
            var countResult = await countCmd.ExecuteScalarAsync();
            profile.TotalRows = countResult == null ? 0 : (int)countResult;

            // Get columns
            var columnsQuery = @"
                SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                ORDER BY ORDINAL_POSITION";

            var columns = new List<(string name, string type, bool nullable)>();
            using (var columnsCmd = new SqlCommand(columnsQuery, connection))
            {
                columnsCmd.Parameters.AddWithValue("@schema", schema);
                columnsCmd.Parameters.AddWithValue("@table", table);
                
                using var reader = await columnsCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2) == "YES"));
                }
            }

            // Profile each column
            foreach (var (columnName, dataType, isNullable) in columns)
            {
                var metric = new ColumnQualityMetric
                {
                    ColumnName = columnName,
                    DataType = dataType,
                    IsNullable = isNullable
                };

                // Get quality metrics
                var metricsQuery = $@"
                    SELECT TOP (@sampleSize)
                        COUNT(*) as SampleSize,
                        COUNT(DISTINCT [{columnName}]) as DistinctCount,
                        COUNT(CASE WHEN [{columnName}] IS NULL THEN 1 END) as NullCount,
                        COUNT(CASE WHEN LTRIM(RTRIM(CAST([{columnName}] as VARCHAR(MAX)))) = '' THEN 1 END) as EmptyCount
                    FROM [{schema}].[{table}]";

                using var metricsCmd = new SqlCommand(metricsQuery, connection);
                metricsCmd.Parameters.AddWithValue("@sampleSize", sampleSize);
                
                using var metricsReader = await metricsCmd.ExecuteReaderAsync();
                if (await metricsReader.ReadAsync())
                {
                    var sampleCount = metricsReader.GetInt32(0);
                    metric.DistinctCount = metricsReader.GetInt32(1);
                    metric.NullCount = metricsReader.GetInt32(2);
                    metric.EmptyCount = metricsReader.GetInt32(3);
                    
                    metric.NullPercentage = sampleCount > 0 ? (metric.NullCount * 100.0 / sampleCount) : 0;
                    metric.EmptyPercentage = sampleCount > 0 ? (metric.EmptyCount * 100.0 / sampleCount) : 0;
                    metric.UniquePercentage = sampleCount > 0 ? (metric.DistinctCount * 100.0 / sampleCount) : 0;
                }

                // Check for patterns in string columns
                if (IsStringType(dataType))
                {
                    await CheckStringPatterns(connection, schema, table, columnName, metric, sampleSize);
                }

                profile.ColumnQualityMetrics.Add(metric);
            }

            // Calculate overall quality score
            profile.OverallQualityScore = CalculateQualityScore(profile);

            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static async Task CheckStringPatterns(SqlConnection connection, string schema, string table, string column, ColumnQualityMetric metric, int sampleSize)
    {
        // Check for common patterns
        var patternQuery = $@"
            SELECT TOP 10
                [{column}] as Value,
                COUNT(*) as Frequency
            FROM [{schema}].[{table}]
            WHERE [{column}] IS NOT NULL
            GROUP BY [{column}]
            ORDER BY COUNT(*) DESC";

        var patterns = new List<string>();
        using var cmd = new SqlCommand(patternQuery, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            patterns.Add($"{reader.GetString(0)} ({reader.GetInt32(1)} occurrences)");
        }

        metric.CommonPatterns = patterns;

        // Check for potential issues
        var issues = new List<string>();
        
        // Check for leading/trailing spaces
        var spaceCheckQuery = $@"
            SELECT COUNT(*)
            FROM (SELECT TOP (@sampleSize) [{column}] FROM [{schema}].[{table}] WHERE [{column}] IS NOT NULL) t
            WHERE [{column}] != LTRIM(RTRIM([{column}]))";
        
        using var spaceCmd = new SqlCommand(spaceCheckQuery, connection);
        spaceCmd.Parameters.AddWithValue("@sampleSize", sampleSize);
        var spacesResult = await spaceCmd.ExecuteScalarAsync();
        var spacesCount = spacesResult == null ? 0 : (int)spacesResult;
        
        if (spacesCount > 0)
        {
            issues.Add($"{spacesCount} values with leading/trailing spaces");
        }

        metric.DataQualityIssues = issues;
    }

    private static double CalculateQualityScore(DataQualityProfile profile)
    {
        if (!profile.ColumnQualityMetrics.Any()) return 100.0;

        var scores = new List<double>();
        
        foreach (var metric in profile.ColumnQualityMetrics)
        {
            var score = 100.0;
            
            // Deduct for nulls in non-nullable columns
            if (!metric.IsNullable && metric.NullPercentage > 0)
            {
                score -= metric.NullPercentage;
            }
            
            // Deduct for empty values
            if (metric.EmptyPercentage > 5)
            {
                score -= Math.Min(20, metric.EmptyPercentage / 2);
            }
            
            // Deduct for data quality issues
            if (metric.DataQualityIssues?.Any() == true)
            {
                score -= metric.DataQualityIssues.Count * 5;
            }
            
            scores.Add(Math.Max(0, score));
        }

        return scores.Average();
    }

    private static string GenerateRelationshipDiagram(RelationshipAnalysis analysis)
    {
        var diagram = new StringBuilder();
        diagram.AppendLine("Table Relationship Diagram:");
        diagram.AppendLine("==========================");
        
        foreach (var fk in analysis.ForeignKeys)
        {
            diagram.AppendLine($"{fk.ForeignKeyTable} --> {fk.ReferencedTable}");
            diagram.AppendLine($"  [{fk.ForeignKeyColumns}] -> [{fk.ReferencedColumns}]");
        }

        return diagram.ToString();
    }

    private static List<string> GenerateRelationshipSuggestions(RelationshipAnalysis analysis)
    {
        var suggestions = new List<string>();

        // Check for missing indexes on foreign keys
        foreach (var fk in analysis.ForeignKeys)
        {
            suggestions.Add($"Consider indexing foreign key columns: {fk.ForeignKeyTable}.{fk.ForeignKeyColumns}");
        }

        // Check for circular dependencies
        foreach (var node in analysis.DependencyTree.Values)
        {
            if (HasCircularDependency(node, new HashSet<string>()))
            {
                suggestions.Add($"Circular dependency detected involving table: {node.TableName}");
            }
        }

        // Check for orphaned tables
        foreach (var table in analysis.AnalyzedTables)
        {
            if (!analysis.ReferencingTables.ContainsKey(table) || !analysis.ReferencingTables[table].Any())
            {
                if (!analysis.ReferencedTables.ContainsKey(table) || !analysis.ReferencedTables[table].Any())
                {
                    suggestions.Add($"Table '{table}' has no relationships - verify if this is intended");
                }
            }
        }

        return suggestions;
    }

    private static bool HasCircularDependency(DependencyNode node, HashSet<string> visited)
    {
        if (node.IsCircular) return true;
        if (visited.Contains(node.TableName)) return true;
        
        visited.Add(node.TableName);
        
        foreach (var dependency in node.Dependencies)
        {
            if (HasCircularDependency(dependency, new HashSet<string>(visited)))
                return true;
        }
        
        return false;
    }

    private static string GenerateDiscoverySummary(DataDiscoveryResult result)
    {
        var summary = new StringBuilder();
        
        if (result.Tables.Any())
        {
            summary.AppendLine($"Found {result.Tables.Count} matching tables");
            var totalRows = result.Tables.Sum(t => t.RowCount);
            summary.AppendLine($"Total rows across all tables: {totalRows:N0}");
        }
        
        if (result.Columns.Any())
        {
            summary.AppendLine($"Found {result.Columns.Count} matching columns");
            var uniqueTables = result.Columns.Select(c => c.TableName).Distinct().Count();
            summary.AppendLine($"Columns spread across {uniqueTables} tables");
        }
        
        if (result.Relationships.Any())
        {
            summary.AppendLine($"Discovered {result.Relationships.Count} relationships");
        }

        return summary.ToString();
    }

    private static string GenerateDataQualitySummary(List<DataQualityProfile> profiles)
    {
        var summary = new StringBuilder();
        
        var avgQualityScore = profiles.Average(p => p.OverallQualityScore);
        summary.AppendLine($"Average data quality score: {avgQualityScore:F1}%");
        
        var totalRows = profiles.Sum(p => p.TotalRows);
        summary.AppendLine($"Total rows analyzed: {totalRows:N0}");
        
        var tablesWithIssues = profiles.Count(p => p.ColumnQualityMetrics.Any(m => m.DataQualityIssues?.Any() == true));
        if (tablesWithIssues > 0)
        {
            summary.AppendLine($"Tables with data quality issues: {tablesWithIssues}");
        }

        return summary.ToString();
    }

    private static List<string> GenerateDataQualityRecommendations(List<DataQualityProfile> profiles)
    {
        var recommendations = new List<string>();

        foreach (var profile in profiles)
        {
            // Check for high null percentages
            var highNullColumns = profile.ColumnQualityMetrics
                .Where(m => !m.IsNullable && m.NullPercentage > 0)
                .ToList();
            
            if (highNullColumns.Any())
            {
                recommendations.Add($"Table '{profile.TableName}' has non-nullable columns with NULL values: {string.Join(", ", highNullColumns.Select(c => c.ColumnName))}");
            }

            // Check for columns with mostly empty values
            var emptyColumns = profile.ColumnQualityMetrics
                .Where(m => m.EmptyPercentage > 50)
                .ToList();
            
            if (emptyColumns.Any())
            {
                recommendations.Add($"Table '{profile.TableName}' has columns with >50% empty values: {string.Join(", ", emptyColumns.Select(c => c.ColumnName))}");
            }

            // Check for potential duplicate issues
            var lowUniqueColumns = profile.ColumnQualityMetrics
                .Where(m => m.UniquePercentage < 1 && m.DistinctCount == 1)
                .ToList();
            
            if (lowUniqueColumns.Any())
            {
                recommendations.Add($"Table '{profile.TableName}' has columns with only one distinct value: {string.Join(", ", lowUniqueColumns.Select(c => c.ColumnName))}");
            }
        }

        return recommendations;
    }

    private static bool IsNumericType(string dataType)
    {
        var numericTypes = new[] { "int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real", "money", "smallmoney" };
        return numericTypes.Any(t => dataType.ToLower().Contains(t));
    }

    private static bool IsDateType(string dataType)
    {
        var dateTypes = new[] { "date", "datetime", "datetime2", "smalldatetime", "time" };
        return dateTypes.Any(t => dataType.ToLower().Contains(t));
    }

    private static bool IsStringType(string dataType)
    {
        var stringTypes = new[] { "char", "varchar", "text", "nchar", "nvarchar", "ntext" };
        return stringTypes.Any(t => dataType.ToLower().Contains(t));
    }

    // Data models
    private class DataDiscoveryResult
    {
        public List<TableSearchResult> Tables { get; set; } = new();
        public List<ColumnSearchResult> Columns { get; set; } = new();
        public List<TableRelationship> Relationships { get; set; } = new();
        public List<DataProfile> DataProfiles { get; set; } = new();
    }

    private class TableSearchResult
    {
        public string TableName { get; set; } = "";
        public string TableType { get; set; } = "";
        public long RowCount { get; set; }
        public int ColumnCount { get; set; }
    }

    private class ColumnSearchResult
    {
        public string TableName { get; set; } = "";
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public bool IsNullable { get; set; }
        public int? MaxLength { get; set; }
        public byte? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
        public string? DefaultValue { get; set; }
    }

    private class TableRelationship
    {
        public string ConstraintName { get; set; } = "";
        public string ForeignKeyTable { get; set; } = "";
        public string ForeignKeyColumn { get; set; } = "";
        public string PrimaryKeyTable { get; set; } = "";
        public string PrimaryKeyColumn { get; set; } = "";
        public string DeleteAction { get; set; } = "";
        public string UpdateAction { get; set; } = "";
    }

    private class DataProfile
    {
        public string TableName { get; set; } = "";
        public List<ColumnProfile> ColumnProfiles { get; set; } = new();
    }

    private class ColumnProfile
    {
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public int TotalRows { get; set; }
        public int DistinctValues { get; set; }
        public int NullCount { get; set; }
        public double NullPercentage { get; set; }
        public string? MinValue { get; set; }
        public string? MaxValue { get; set; }
    }

    private class RelationshipAnalysis
    {
        public List<string> AnalyzedTables { get; set; } = new();
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
        public Dictionary<string, List<string>> ReferencingTables { get; set; } = new();
        public Dictionary<string, List<string>> ReferencedTables { get; set; } = new();
        public Dictionary<string, DependencyNode> DependencyTree { get; set; } = new();
    }

    private class ForeignKeyInfo
    {
        public string ConstraintName { get; set; } = "";
        public string ForeignKeyTable { get; set; } = "";
        public string ForeignKeyColumns { get; set; } = "";
        public string ReferencedTable { get; set; } = "";
        public string ReferencedColumns { get; set; } = "";
    }

    private class DependencyNode
    {
        public string TableName { get; set; } = "";
        public List<DependencyNode> Dependencies { get; set; } = new();
        public bool IsCircular { get; set; }
    }

    private class DataQualityProfile
    {
        public string TableName { get; set; } = "";
        public int TotalRows { get; set; }
        public List<ColumnQualityMetric> ColumnQualityMetrics { get; set; } = new();
        public double OverallQualityScore { get; set; }
    }

    private class ColumnQualityMetric
    {
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public bool IsNullable { get; set; }
        public int DistinctCount { get; set; }
        public int NullCount { get; set; }
        public int EmptyCount { get; set; }
        public double NullPercentage { get; set; }
        public double EmptyPercentage { get; set; }
        public double UniquePercentage { get; set; }
        public List<string>? CommonPatterns { get; set; }
        public List<string>? DataQualityIssues { get; set; }
    }
}