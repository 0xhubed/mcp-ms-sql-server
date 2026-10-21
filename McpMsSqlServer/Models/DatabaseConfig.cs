using System.Text.Json.Serialization;

namespace McpMsSqlServer.Models;

public class DatabaseConfig
{
    public string Name { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Legacy single-schema setting. Ignored when <see cref="AllowedSchemas"/> is non-empty.</summary>
    public string AllowedSchema { get; set; } = string.Empty;

    /// <summary>Schemas the tools may explore and operate on. The first entry is the default for unqualified table names.</summary>
    public List<string> AllowedSchemas { get; set; } = new();

    /// <summary>
    /// The resolved schema list: <see cref="AllowedSchemas"/> when set, otherwise the legacy
    /// <see cref="AllowedSchema"/> as a single entry, otherwise empty (meaning unrestricted).
    /// Blank and case-insensitive duplicate entries are dropped, order is preserved.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> EffectiveSchemas
    {
        get
        {
            var source = AllowedSchemas.Count > 0 ? AllowedSchemas : new List<string> { AllowedSchema };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return source
                .Select(s => s?.Trim() ?? string.Empty)
                .Where(s => s.Length > 0 && seen.Add(s))
                .ToList();
        }
    }

    /// <summary>The schema used when a table name carries no schema qualifier. Empty when unrestricted.</summary>
    [JsonIgnore]
    public string PrimarySchema => EffectiveSchemas.Count > 0 ? EffectiveSchemas[0] : string.Empty;

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