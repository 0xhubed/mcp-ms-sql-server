using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using McpMsSqlServer.Models;
using System.Text.Json;

namespace McpMsSqlServer.Services;

public class DatabaseService
{
    private readonly ConfigService _configService;
    private readonly ILogger<DatabaseService> _logger;
    
    public DatabaseService(ConfigService configService, ILogger<DatabaseService> logger)
    {
        _configService = configService;
        _logger = logger;
    }
    
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return false;
        }
    }
    
    public async Task<string> TestConnectionDetailedAsync()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            
            var info = new
            {
                Success = true,
                Database = connection.Database,
                DataSource = connection.DataSource,
                ServerVersion = connection.ServerVersion,
                State = connection.State.ToString(),
                ConfigName = _configService.CurrentConfig.Name,
                Schema = _configService.CurrentConfig.AllowedSchema
            };
            
            return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            var error = new
            {
                Success = false,
                Error = ex.Message,
                ConfigName = _configService.CurrentConfig.Name
            };
            
            return JsonSerializer.Serialize(error, new JsonSerializerOptions { WriteIndented = true });
        }
    }
    
    public async Task<DataTable> ExecuteQueryAsync(string query, Dictionary<string, object>? parameters = null)
    {
        using var connection = CreateConnection();
        using var command = new SqlCommand(query, connection);
        
        command.CommandTimeout = _configService.CurrentConfig.QuerySettings.TimeoutSeconds;
        
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
        }
        
        await connection.OpenAsync();
        
        using var adapter = new SqlDataAdapter(command);
        var dataTable = new DataTable();
        adapter.Fill(dataTable);
        
        return dataTable;
    }
    
    public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object>? parameters = null)
    {
        using var connection = CreateConnection();
        using var command = new SqlCommand(query, connection);
        
        command.CommandTimeout = _configService.CurrentConfig.QuerySettings.TimeoutSeconds;
        
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
        }
        
        await connection.OpenAsync();
        return await command.ExecuteNonQueryAsync();
    }
    
    public async Task<object?> ExecuteScalarAsync(string query, Dictionary<string, object>? parameters = null)
    {
        using var connection = CreateConnection();
        using var command = new SqlCommand(query, connection);
        
        command.CommandTimeout = _configService.CurrentConfig.QuerySettings.TimeoutSeconds;
        
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
        }
        
        await connection.OpenAsync();
        return await command.ExecuteScalarAsync();
    }
    
    public string GetSchemaPrefix()
    {
        var schema = _configService.CurrentConfig.AllowedSchema;
        return string.IsNullOrEmpty(schema) ? "" : $"[{schema}].";
    }
    
    public string EnsureSchemaPrefix(string tableName)
    {
        if (tableName.Contains('.'))
            return tableName;
            
        return GetSchemaPrefix() + $"[{tableName}]";
    }
    
    private SqlConnection CreateConnection()
    {
        return new SqlConnection(_configService.CurrentConfig.ConnectionString);
    }
}