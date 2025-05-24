using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using McpMsSqlServer.Services;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class PerformanceAnalysisTool
{
    [McpServerTool]
    [Description("Analyze query performance and provide optimization suggestions")]
    public static async Task<string> AnalyzeQueryPerformance(
        string sqlQuery,
        bool includeExecutionPlan,
        bool includeIndexSuggestions,
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
            
            // Default values are already set by parameters

            // Validate it's a SELECT query
            var validationResult = await securityService.ValidateQueryAsync(sqlQuery);
            if (!validationResult.IsValid)
            {
                return JsonSerializer.Serialize(new { 
                    error = "Only SELECT queries can be analyzed for performance",
                    suggestion = "Use ExecuteQuery tool for non-SELECT queries",
                    validationError = validationResult.ErrorMessage
                });
            }

            // Prepare results
            var analysisResults = new PerformanceAnalysisResult
            {
                OriginalQuery = sqlQuery,
                ExecutionStats = new ExecutionStatistics(),
                IndexSuggestions = new List<IndexSuggestion>(),
                QueryOptimizations = new List<string>(),
                ExecutionPlan = null
            };

            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            // Get execution statistics
            await GetExecutionStatistics(connection, sqlQuery, analysisResults.ExecutionStats);

            // Get execution plan if requested
            if (includeExecutionPlan && config.QuerySettings.EnableQueryPlan)
            {
                analysisResults.ExecutionPlan = await GetExecutionPlan(connection, sqlQuery);
                AnalyzeExecutionPlan(analysisResults.ExecutionPlan, analysisResults);
            }

            // Get missing index suggestions
            if (includeIndexSuggestions)
            {
                analysisResults.IndexSuggestions = await GetMissingIndexSuggestions(connection, config.AllowedSchema);
            }

            // Analyze query for common issues
            AnalyzeQueryForIssues(sqlQuery, analysisResults);

            // Generate optimization suggestions
            GenerateOptimizationSuggestions(analysisResults);

            return JsonSerializer.Serialize(new
            {
                success = true,
                analysis = analysisResults,
                summary = GeneratePerformanceSummary(analysisResults)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Performance analysis error: {ex.Message}" });
        }
    }

    [McpServerTool]
    [Description("Get database performance statistics and recommendations")]
    public static async Task<string> GetDatabasePerformanceStats(
        int topQueries,
        bool includeWaitStats,
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
            topQueries = topQueries > 0 ? topQueries : 10;

            using var connection = databaseService.CreateConnection();
            await connection.OpenAsync();

            var performanceStats = new DatabasePerformanceStats();

            // Get top resource-consuming queries
            performanceStats.TopQueries = await GetTopResourceConsumingQueries(connection, topQueries);

            // Get wait statistics
            if (includeWaitStats)
            {
                performanceStats.WaitStats = await GetWaitStatistics(connection);
            }

            // Get index usage statistics
            performanceStats.IndexUsageStats = await GetIndexUsageStatistics(connection, config.AllowedSchema);

            // Get table statistics
            performanceStats.TableStats = await GetTableStatistics(connection, config.AllowedSchema);

            return JsonSerializer.Serialize(new
            {
                success = true,
                performanceStats = performanceStats,
                recommendations = GeneratePerformanceRecommendations(performanceStats)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Performance stats error: {ex.Message}" });
        }
    }

    private static async Task GetExecutionStatistics(SqlConnection connection, string query, ExecutionStatistics stats)
    {
        // Enable statistics
        using (var cmd = new SqlCommand("SET STATISTICS TIME ON; SET STATISTICS IO ON;", connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Create a new connection for message handling
        var messageLog = new List<string>();
        connection.InfoMessage += (sender, e) =>
        {
            messageLog.Add(e.Message);
        };

        // Execute query to get statistics
        using (var cmd = new SqlCommand(query, connection))
        {
            cmd.CommandTimeout = 30;
            var startTime = DateTime.UtcNow;
            
            using var reader = await cmd.ExecuteReaderAsync();
            var rowCount = 0;
            while (await reader.ReadAsync())
            {
                rowCount++;
            }
            
            stats.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            stats.RowsReturned = rowCount;
        }

        // Parse statistics from messages
        foreach (var message in messageLog)
        {
            if (message.Contains("logical reads"))
            {
                ParseIOStatistics(message, stats);
            }
            else if (message.Contains("CPU time"))
            {
                ParseTimeStatistics(message, stats);
            }
        }

        // Disable statistics
        using (var cmd = new SqlCommand("SET STATISTICS TIME OFF; SET STATISTICS IO OFF;", connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static void ParseIOStatistics(string message, ExecutionStatistics stats)
    {
        // Parse logical reads
        var logicalReadsMatch = System.Text.RegularExpressions.Regex.Match(message, @"logical reads (\d+)");
        if (logicalReadsMatch.Success)
        {
            stats.LogicalReads = int.Parse(logicalReadsMatch.Groups[1].Value);
        }

        // Parse physical reads
        var physicalReadsMatch = System.Text.RegularExpressions.Regex.Match(message, @"physical reads (\d+)");
        if (physicalReadsMatch.Success)
        {
            stats.PhysicalReads = int.Parse(physicalReadsMatch.Groups[1].Value);
        }
    }

    private static void ParseTimeStatistics(string message, ExecutionStatistics stats)
    {
        // Parse CPU time
        var cpuTimeMatch = System.Text.RegularExpressions.Regex.Match(message, @"CPU time = (\d+) ms");
        if (cpuTimeMatch.Success)
        {
            stats.CpuTimeMs = int.Parse(cpuTimeMatch.Groups[1].Value);
        }
    }

    private static async Task<ExecutionPlanInfo?> GetExecutionPlan(SqlConnection connection, string query)
    {
        try
        {
            // Get estimated execution plan
            using var cmd = new SqlCommand("SET SHOWPLAN_XML ON", connection);
            await cmd.ExecuteNonQueryAsync();

            using var planCmd = new SqlCommand(query, connection);
            using var reader = await planCmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var planXml = reader.GetString(0);
                
                // Reset showplan
                using var resetCmd = new SqlCommand("SET SHOWPLAN_XML OFF", connection);
                await resetCmd.ExecuteNonQueryAsync();

                return ParseExecutionPlan(planXml);
            }
        }
        catch
        {
            // If execution plan fails, continue without it
        }

        return null;
    }

    private static ExecutionPlanInfo ParseExecutionPlan(string planXml)
    {
        var planInfo = new ExecutionPlanInfo
        {
            PlanXml = planXml,
            Warnings = new List<string>(),
            CostlyOperations = new List<string>(),
            MissingIndexes = new List<string>()
        };

        // Simple XML parsing for key information
        if (planXml.Contains("NoJoinPredicate"))
            planInfo.Warnings.Add("Missing join predicate detected");
        
        if (planXml.Contains("ColumnsWithNoStatistics"))
            planInfo.Warnings.Add("Columns with no statistics found");
        
        if (planXml.Contains("Table Scan"))
            planInfo.CostlyOperations.Add("Table scan detected - consider adding indexes");
        
        if (planXml.Contains("Clustered Index Scan"))
            planInfo.CostlyOperations.Add("Clustered index scan - may benefit from additional indexes");
        
        if (planXml.Contains("Sort"))
            planInfo.CostlyOperations.Add("Sort operation - consider indexed columns for ORDER BY");
        
        if (planXml.Contains("Hash Match"))
            planInfo.CostlyOperations.Add("Hash join - might benefit from indexes on join columns");

        return planInfo;
    }

    private static void AnalyzeExecutionPlan(ExecutionPlanInfo? plan, PerformanceAnalysisResult result)
    {
        if (plan == null) return;

        result.QueryOptimizations.AddRange(plan.Warnings);
        result.QueryOptimizations.AddRange(plan.CostlyOperations);
    }

    private static async Task<List<IndexSuggestion>> GetMissingIndexSuggestions(SqlConnection connection, string schema)
    {
        var suggestions = new List<IndexSuggestion>();
        
        var query = @"
            SELECT TOP 10
                d.statement AS TableName,
                d.equality_columns,
                d.inequality_columns,
                d.included_columns,
                s.avg_total_user_cost * s.avg_user_impact * (s.user_seeks + s.user_scans) AS Score
            FROM sys.dm_db_missing_index_details d
            INNER JOIN sys.dm_db_missing_index_groups g ON d.index_handle = g.index_handle
            INNER JOIN sys.dm_db_missing_index_group_stats s ON g.index_group_handle = s.group_handle
            WHERE d.database_id = DB_ID()
            ORDER BY Score DESC";

        using var cmd = new SqlCommand(query, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            if (tableName.Contains($"[{schema}]"))
            {
                suggestions.Add(new IndexSuggestion
                {
                    TableName = tableName,
                    EqualityColumns = reader.IsDBNull(1) ? null : reader.GetString(1),
                    InequalityColumns = reader.IsDBNull(2) ? null : reader.GetString(2),
                    IncludedColumns = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ImpactScore = reader.GetDouble(4)
                });
            }
        }

        return suggestions;
    }

    private static void AnalyzeQueryForIssues(string query, PerformanceAnalysisResult result)
    {
        var queryLower = query.ToLower();

        // Check for SELECT *
        if (queryLower.Contains("select *"))
        {
            result.QueryOptimizations.Add("Avoid SELECT * - specify only needed columns");
        }

        // Check for missing WHERE clause
        if (!queryLower.Contains("where") && queryLower.Contains("from"))
        {
            result.QueryOptimizations.Add("Consider adding WHERE clause to filter results");
        }

        // Check for LIKE with leading wildcard
        if (queryLower.Contains("like '%"))
        {
            result.QueryOptimizations.Add("Leading wildcards in LIKE prevent index usage");
        }

        // Check for functions in WHERE clause
        if (queryLower.Contains("where") && 
            (queryLower.Contains("upper(") || queryLower.Contains("lower(") || 
             queryLower.Contains("datepart(") || queryLower.Contains("substring(")))
        {
            result.QueryOptimizations.Add("Functions in WHERE clause may prevent index usage");
        }

        // Check for NOT IN
        if (queryLower.Contains("not in"))
        {
            result.QueryOptimizations.Add("Consider using NOT EXISTS instead of NOT IN for better performance");
        }

        // Check for OR conditions
        if (queryLower.Contains(" or "))
        {
            result.QueryOptimizations.Add("OR conditions may prevent optimal index usage - consider UNION");
        }
    }

    private static void GenerateOptimizationSuggestions(PerformanceAnalysisResult result)
    {
        // Add suggestions based on statistics
        if (result.ExecutionStats.LogicalReads > 1000)
        {
            result.QueryOptimizations.Add($"High logical reads ({result.ExecutionStats.LogicalReads}) - consider adding indexes");
        }

        if (result.ExecutionStats.PhysicalReads > 100)
        {
            result.QueryOptimizations.Add($"Physical reads detected ({result.ExecutionStats.PhysicalReads}) - data not in cache");
        }

        if (result.ExecutionStats.ExecutionTimeMs > 1000)
        {
            result.QueryOptimizations.Add($"Slow query execution ({result.ExecutionStats.ExecutionTimeMs}ms) - review execution plan");
        }

        // Add index creation statements
        foreach (var index in result.IndexSuggestions.Take(3))
        {
            var indexStatement = GenerateIndexStatement(index);
            result.QueryOptimizations.Add($"Suggested index: {indexStatement}");
        }
    }

    private static string GenerateIndexStatement(IndexSuggestion suggestion)
    {
        var indexName = $"IX_{suggestion.TableName.Replace("[", "").Replace("]", "").Replace(".", "_")}_{DateTime.UtcNow:yyyyMMdd}";
        var columns = suggestion.EqualityColumns ?? "";
        
        if (!string.IsNullOrEmpty(suggestion.InequalityColumns))
        {
            columns += string.IsNullOrEmpty(columns) ? suggestion.InequalityColumns : $", {suggestion.InequalityColumns}";
        }

        var statement = $"CREATE INDEX {indexName} ON {suggestion.TableName} ({columns})";
        
        if (!string.IsNullOrEmpty(suggestion.IncludedColumns))
        {
            statement += $" INCLUDE ({suggestion.IncludedColumns})";
        }

        return statement;
    }

    private static async Task<List<TopQuery>> GetTopResourceConsumingQueries(SqlConnection connection, int topN)
    {
        var queries = new List<TopQuery>();
        
        var query = @"
            SELECT TOP (@topN)
                qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
                qs.total_worker_time / qs.execution_count AS avg_cpu_time,
                qs.total_elapsed_time / qs.execution_count AS avg_elapsed_time,
                qs.execution_count,
                SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
                    ((CASE qs.statement_end_offset
                        WHEN -1 THEN DATALENGTH(st.text)
                        ELSE qs.statement_end_offset
                    END - qs.statement_start_offset)/2) + 1) AS query_text
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            ORDER BY qs.total_logical_reads DESC";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@topN", topN);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            queries.Add(new TopQuery
            {
                AvgLogicalReads = reader.GetInt64(0),
                AvgCpuTimeUs = reader.GetInt64(1),
                AvgElapsedTimeUs = reader.GetInt64(2),
                ExecutionCount = reader.GetInt64(3),
                QueryText = reader.GetString(4)
            });
        }

        return queries;
    }

    private static async Task<List<WaitStatistic>> GetWaitStatistics(SqlConnection connection)
    {
        var waitStats = new List<WaitStatistic>();
        
        var query = @"
            SELECT TOP 10
                wait_type,
                wait_time_ms,
                wait_time_ms * 100.0 / SUM(wait_time_ms) OVER() AS wait_percentage
            FROM sys.dm_os_wait_stats
            WHERE wait_type NOT IN (
                'CLR_SEMAPHORE', 'LAZYWRITER_SLEEP', 'RESOURCE_QUEUE',
                'SLEEP_TASK', 'SLEEP_SYSTEMTASK', 'SQLTRACE_BUFFER_FLUSH',
                'WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE',
                'REQUEST_FOR_DEADLOCK_SEARCH', 'XE_TIMER_EVENT',
                'BROKER_TO_FLUSH', 'BROKER_TASK_STOP', 'CLR_MANUAL_EVENT',
                'CLR_AUTO_EVENT', 'DISPATCHER_QUEUE_SEMAPHORE'
            )
            AND wait_time_ms > 0
            ORDER BY wait_time_ms DESC";

        using var cmd = new SqlCommand(query, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            waitStats.Add(new WaitStatistic
            {
                WaitType = reader.GetString(0),
                WaitTimeMs = reader.GetInt64(1),
                WaitPercentage = reader.GetDouble(2)
            });
        }

        return waitStats;
    }

    private static async Task<List<IndexUsageStatistic>> GetIndexUsageStatistics(SqlConnection connection, string schema)
    {
        var indexStats = new List<IndexUsageStatistic>();
        
        var query = @"
            SELECT 
                t.name AS TableName,
                i.name AS IndexName,
                i.type_desc AS IndexType,
                us.user_seeks,
                us.user_scans,
                us.user_lookups,
                us.user_updates
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            LEFT JOIN sys.dm_db_index_usage_stats us ON i.object_id = us.object_id AND i.index_id = us.index_id
            WHERE t.schema_id = SCHEMA_ID(@schema)
            AND i.type > 0
            ORDER BY t.name, i.name";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexStats.Add(new IndexUsageStatistic
            {
                TableName = reader.GetString(0),
                IndexName = reader.GetString(1),
                IndexType = reader.GetString(2),
                UserSeeks = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                UserScans = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                UserLookups = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                UserUpdates = reader.IsDBNull(6) ? 0 : reader.GetInt64(6)
            });
        }

        return indexStats;
    }

    private static async Task<List<TableStatistic>> GetTableStatistics(SqlConnection connection, string schema)
    {
        var tableStats = new List<TableStatistic>();
        
        var query = @"
            SELECT 
                t.name AS TableName,
                p.rows AS RowCount,
                SUM(a.total_pages) * 8 AS TotalSpaceKB,
                SUM(a.used_pages) * 8 AS UsedSpaceKB
            FROM sys.tables t
            INNER JOIN sys.indexes i ON t.object_id = i.object_id
            INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
            INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE t.schema_id = SCHEMA_ID(@schema)
            AND i.index_id <= 1
            GROUP BY t.name, p.rows
            ORDER BY p.rows DESC";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tableStats.Add(new TableStatistic
            {
                TableName = reader.GetString(0),
                RowCount = reader.GetInt64(1),
                TotalSpaceKB = reader.GetInt64(2),
                UsedSpaceKB = reader.GetInt64(3)
            });
        }

        return tableStats;
    }

    private static string GeneratePerformanceSummary(PerformanceAnalysisResult result)
    {
        var summary = new StringBuilder();
        
        summary.AppendLine($"Query executed in {result.ExecutionStats.ExecutionTimeMs:F2}ms");
        summary.AppendLine($"Returned {result.ExecutionStats.RowsReturned} rows");
        summary.AppendLine($"Logical reads: {result.ExecutionStats.LogicalReads}, Physical reads: {result.ExecutionStats.PhysicalReads}");
        
        if (result.IndexSuggestions.Any())
        {
            summary.AppendLine($"Found {result.IndexSuggestions.Count} missing index suggestions");
        }
        
        if (result.QueryOptimizations.Any())
        {
            summary.AppendLine($"Identified {result.QueryOptimizations.Count} optimization opportunities");
        }

        return summary.ToString();
    }

    private static List<string> GeneratePerformanceRecommendations(DatabasePerformanceStats stats)
    {
        var recommendations = new List<string>();

        // Analyze top queries
        var highCpuQueries = stats.TopQueries.Where(q => q.AvgCpuTimeUs > 100000).ToList();
        if (highCpuQueries.Any())
        {
            recommendations.Add($"{highCpuQueries.Count} queries with high CPU usage detected - review for optimization");
        }

        var highReadQueries = stats.TopQueries.Where(q => q.AvgLogicalReads > 10000).ToList();
        if (highReadQueries.Any())
        {
            recommendations.Add($"{highReadQueries.Count} queries with high logical reads - consider adding indexes");
        }

        // Analyze wait statistics
        var topWait = stats.WaitStats.FirstOrDefault();
        if (topWait != null && topWait.WaitPercentage > 20)
        {
            recommendations.Add($"Top wait type '{topWait.WaitType}' accounts for {topWait.WaitPercentage:F1}% of waits");
        }

        // Analyze index usage
        var unusedIndexes = stats.IndexUsageStats
            .Where(i => i.UserSeeks + i.UserScans == 0 && i.UserUpdates > 0)
            .ToList();
        if (unusedIndexes.Any())
        {
            recommendations.Add($"{unusedIndexes.Count} unused indexes found - consider removing to improve write performance");
        }

        // Analyze table sizes
        var largeTables = stats.TableStats.Where(t => t.RowCount > 1000000).ToList();
        if (largeTables.Any())
        {
            recommendations.Add($"{largeTables.Count} large tables (>1M rows) - ensure proper indexing and partitioning");
        }

        return recommendations;
    }

    // Data models
    private class PerformanceAnalysisResult
    {
        public string OriginalQuery { get; set; } = "";
        public ExecutionStatistics ExecutionStats { get; set; } = new();
        public List<IndexSuggestion> IndexSuggestions { get; set; } = new();
        public List<string> QueryOptimizations { get; set; } = new();
        public ExecutionPlanInfo? ExecutionPlan { get; set; }
    }

    private class ExecutionStatistics
    {
        public double ExecutionTimeMs { get; set; }
        public int RowsReturned { get; set; }
        public int LogicalReads { get; set; }
        public int PhysicalReads { get; set; }
        public int CpuTimeMs { get; set; }
    }

    private class ExecutionPlanInfo
    {
        public string PlanXml { get; set; } = "";
        public List<string> Warnings { get; set; } = new();
        public List<string> CostlyOperations { get; set; } = new();
        public List<string> MissingIndexes { get; set; } = new();
    }

    private class IndexSuggestion
    {
        public string TableName { get; set; } = "";
        public string? EqualityColumns { get; set; }
        public string? InequalityColumns { get; set; }
        public string? IncludedColumns { get; set; }
        public double ImpactScore { get; set; }
    }

    private class DatabasePerformanceStats
    {
        public List<TopQuery> TopQueries { get; set; } = new();
        public List<WaitStatistic> WaitStats { get; set; } = new();
        public List<IndexUsageStatistic> IndexUsageStats { get; set; } = new();
        public List<TableStatistic> TableStats { get; set; } = new();
    }

    private class TopQuery
    {
        public long AvgLogicalReads { get; set; }
        public long AvgCpuTimeUs { get; set; }
        public long AvgElapsedTimeUs { get; set; }
        public long ExecutionCount { get; set; }
        public string QueryText { get; set; } = "";
    }

    private class WaitStatistic
    {
        public string WaitType { get; set; } = "";
        public long WaitTimeMs { get; set; }
        public double WaitPercentage { get; set; }
    }

    private class IndexUsageStatistic
    {
        public string TableName { get; set; } = "";
        public string IndexName { get; set; } = "";
        public string IndexType { get; set; } = "";
        public long UserSeeks { get; set; }
        public long UserScans { get; set; }
        public long UserLookups { get; set; }
        public long UserUpdates { get; set; }
    }

    private class TableStatistic
    {
        public string TableName { get; set; } = "";
        public long RowCount { get; set; }
        public long TotalSpaceKB { get; set; }
        public long UsedSpaceKB { get; set; }
    }
}