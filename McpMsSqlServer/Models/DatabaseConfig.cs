namespace McpMsSqlServer.Models;

public class DatabaseConfig
{
    public string Name { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string AllowedSchema { get; set; } = string.Empty;
    public PermissionSettings Permissions { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public QuerySettings QuerySettings { get; set; } = new();
    public List<string> RestrictedTables { get; set; } = new();
    public List<string> AllowedOperations { get; set; } = new();
}

public class PermissionSettings
{
    public bool AllowRead { get; set; } = true;
    public bool AllowWrite { get; set; } = false;
    public bool AllowDelete { get; set; } = false;
    public bool AllowSchemaChanges { get; set; } = false;
}

public class SecuritySettings
{
    public bool RequireWhereClause { get; set; } = true;
    public int MaxRowsPerQuery { get; set; } = 1000;
    public int MaxRowsPerUpdate { get; set; } = 100;
    public int MaxRowsPerDelete { get; set; } = 10;
    public bool AuditOperations { get; set; } = true;
    public bool BackupRecommendations { get; set; } = true;
}

public class QuerySettings
{
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableQueryPlan { get; set; } = false;
    public bool AllowJoins { get; set; } = true;
    public bool AllowSubqueries { get; set; } = true;
}