using System.Text.RegularExpressions;
using McpMsSqlServer.Models;

namespace McpMsSqlServer.Services;

public class SecurityService
{
    private readonly ConfigService _configService;
    private readonly List<string> _dangerousPatterns = new()
    {
        @";\s*(DROP|CREATE|ALTER|TRUNCATE|EXEC|EXECUTE)\s",
        @"--.*$",
        @"/\*.*?\*/",
        @"xp_cmdshell",
        @"sp_executesql",
        @"OPENROWSET",
        @"OPENDATASOURCE",
        @"OPENQUERY"
    };
    
    public SecurityService(ConfigService configService)
    {
        _configService = configService;
    }
    
    public (bool IsValid, string? Error) ValidateQuery(string query, string operationType)
    {
        var config = _configService.CurrentConfig;
        
        if (!config.AllowedOperations.Contains(operationType, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Operation '{operationType}' is not allowed in current configuration");
        }
        
        foreach (var pattern in _dangerousPatterns)
        {
            if (Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                return (false, "Query contains potentially dangerous SQL patterns");
            }
        }
        
        if (operationType.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) ||
            operationType.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            if (config.Security.RequireWhereClause && !ContainsWhereClause(query))
            {
                return (false, $"{operationType} operations require a WHERE clause for safety");
            }
        }
        
        var tablesInQuery = ExtractTableNames(query);
        foreach (var table in tablesInQuery)
        {
            if (config.RestrictedTables.Any(rt => rt.Equals(table, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, $"Access to table '{table}' is restricted");
            }
        }
        
        return (true, null);
    }
    
    public string SanitizeIdentifier(string identifier)
    {
        return Regex.Replace(identifier, @"[^\w\d_]", "");
    }
    
    public bool IsSchemaAllowed(string schema)
    {
        var allowedSchema = _configService.CurrentConfig.AllowedSchema;
        if (string.IsNullOrEmpty(allowedSchema))
            return true;
            
        return schema.Equals(allowedSchema, StringComparison.OrdinalIgnoreCase);
    }
    
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateQueryAsync(string query)
    {
        return await Task.Run(() =>
        {
            var result = ValidateQuery(query, "SELECT");
            return (result.IsValid, result.Error);
        });
    }
    
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateTableAccessAsync(string schema, string tableName)
    {
        return await Task.Run(() =>
        {
            if (!IsSchemaAllowed(schema))
            {
                return (false, $"Access to schema '{schema}' is not allowed");
            }
            
            var config = _configService.CurrentConfig;
            if (config.RestrictedTables.Any(rt => rt.Equals(tableName, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, $"Access to table '{tableName}' is restricted");
            }
            
            return (true, null);
        });
    }
    
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateWhereClauseAsync(string whereClause)
    {
        return await Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(whereClause))
            {
                return (false, "WHERE clause cannot be empty");
            }
            
            foreach (var pattern in _dangerousPatterns)
            {
                if (Regex.IsMatch(whereClause, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
                {
                    return (false, "WHERE clause contains potentially dangerous SQL patterns");
                }
            }
            
            return (true, null);
        });
    }
    
    private bool ContainsWhereClause(string query)
    {
        var wherePattern = @"\bWHERE\s+.+";
        return Regex.IsMatch(query, wherePattern, RegexOptions.IgnoreCase);
    }
    
    private List<string> ExtractTableNames(string query)
    {
        var tables = new List<string>();
        var patterns = new[]
        {
            @"FROM\s+\[?(\w+)\]?",
            @"JOIN\s+\[?(\w+)\]?",
            @"UPDATE\s+\[?(\w+)\]?",
            @"INSERT\s+INTO\s+\[?(\w+)\]?",
            @"DELETE\s+FROM\s+\[?(\w+)\]?"
        };
        
        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(query, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    tables.Add(match.Groups[1].Value);
                }
            }
        }
        
        return tables.Distinct().ToList();
    }
}