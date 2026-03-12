using System.Text.Json;
using McpMsSqlServer.Models;

namespace McpMsSqlServer.Services;

public class ConfigService
{
    private readonly string _configPath;
    private Dictionary<string, DatabaseConfig> _configurations = new();
    private string _currentConfigName = "default";
    private string? _currentDatabase;

    public DatabaseConfig CurrentConfig => GetConfiguration(_currentConfigName);
    public string CurrentDatabase => _currentDatabase
        ?? CurrentConfig.DefaultDatabase
        ?? string.Empty;
    
    public ConfigService()
    {
        // Get the base directory where the executable is located
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        
        // Look for Configurations folder in the project root (going up from bin/Release/net9.0)
        var projectRoot = baseDirectory;
        if (baseDirectory.Contains("bin"))
        {
            // Navigate up to project root
            projectRoot = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", ".."));
        }
        
        _configPath = Environment.GetEnvironmentVariable("MCP_CONFIG_PATH") 
            ?? Path.Combine(projectRoot, "Configurations");
            
        LoadConfigurations();
        
        var configName = Environment.GetEnvironmentVariable("MCP_CONFIG_NAME");
        if (!string.IsNullOrEmpty(configName))
        {
            SwitchConfiguration(configName);
        }
    }
    
    private void LoadConfigurations()
    {
        if (!Directory.Exists(_configPath))
        {
            Directory.CreateDirectory(_configPath);
        }
        
        var configFiles = Directory.GetFiles(_configPath, "*.json");
        
        foreach (var file in configFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var config = JsonSerializer.Deserialize<DatabaseConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    config.ConnectionString = ResolveEnvironmentVariables(config.ConnectionString);
                    var configName = Path.GetFileNameWithoutExtension(file);
                    _configurations[configName] = config;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading configuration {file}: {ex.Message}");
            }
        }
        
        if (_configurations.Count == 0)
        {
            _configurations["default"] = CreateDefaultConfig();
        }
    }
    
    public DatabaseConfig GetConfiguration(string name)
    {
        return _configurations.TryGetValue(name, out var config) 
            ? config 
            : _configurations["default"];
    }
    
    public List<string> ListConfigurations()
    {
        return _configurations.Keys.ToList();
    }
    
    public string SwitchConfiguration(string configName)
    {
        if (_configurations.ContainsKey(configName))
        {
            _currentConfigName = configName;
            _currentDatabase = null;
            return $"Switched to configuration: {configName}";
        }
        return $"Configuration '{configName}' not found. Available: {string.Join(", ", _configurations.Keys)}";
    }

    public string SwitchDatabase(string databaseName)
    {
        var available = CurrentConfig.AvailableDatabases;
        if (available.Count > 0 && !available.Contains(databaseName, StringComparer.OrdinalIgnoreCase))
        {
            return $"Database '{databaseName}' is not in the available databases list. Available: {string.Join(", ", available)}";
        }
        _currentDatabase = databaseName;
        return $"Switched to database: {databaseName}";
    }

    public List<string> ListAvailableDatabases()
    {
        return CurrentConfig.AvailableDatabases;
    }
    
    public string GetCurrentConfigurationInfo()
    {
        var config = CurrentConfig;
        return JsonSerializer.Serialize(new
        {
            Name = config.Name,
            CurrentDatabase = CurrentDatabase,
            AvailableDatabases = config.AvailableDatabases,
            Schema = config.AllowedSchema,
            Permissions = config.Permissions,
            Security = new
            {
                config.Security.RequireWhereClause,
                config.Security.MaxRowsPerQuery,
                config.Security.AuditOperations
            },
            RestrictedTables = config.RestrictedTables,
            AllowedOperations = config.AllowedOperations
        }, new JsonSerializerOptions { WriteIndented = true });
    }
    
    private static string ResolveEnvironmentVariables(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        return System.Text.RegularExpressions.Regex.Replace(value, @"\$\{(\w+)\}", match =>
        {
            var envVar = Environment.GetEnvironmentVariable(match.Groups[1].Value);
            return envVar ?? match.Value;
        });
    }

    private DatabaseConfig CreateDefaultConfig()
    {
        return new DatabaseConfig
        {
            Name = "Default Configuration",
            ConnectionString = "Server=localhost;Database=TestDB;Integrated Security=true;",
            AllowedSchema = "dbo",
            Permissions = new PermissionSettings
            {
                AllowRead = true,
                AllowWrite = false,
                AllowDelete = false,
                AllowSchemaChanges = false
            },
            Security = new SecuritySettings
            {
                RequireWhereClause = true,
                MaxRowsPerQuery = 100,
                MaxRowsPerUpdate = 10,
                MaxRowsPerDelete = 5,
                AuditOperations = true,
                BackupRecommendations = true
            },
            QuerySettings = new QuerySettings
            {
                TimeoutSeconds = 30,
                EnableQueryPlan = false,
                AllowJoins = true,
                AllowSubqueries = true
            },
            RestrictedTables = new List<string>(),
            AllowedOperations = new List<string> { "SELECT" }
        };
    }
}