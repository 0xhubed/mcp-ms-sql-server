using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;
using System.Text.Json;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class ConfigurationTools
{
    [McpServerTool]
    [Description("List all available project configurations")]
    public static string ListConfigurations(ConfigService configService)
    {
        var configs = configService.ListConfigurations();
        return JsonSerializer.Serialize(new { configurations = configs }, new JsonSerializerOptions { WriteIndented = true });
    }
    
    [McpServerTool]
    [Description("Switch to a different project configuration")]
    public static string SwitchConfiguration(
        ConfigService configService,
        [Description("The name of the configuration to switch to")] string configName)
    {
        return configService.SwitchConfiguration(configName);
    }
    
    [McpServerTool]
    [Description("Get current configuration details")]
    public static string GetCurrentConfiguration(ConfigService configService)
    {
        return configService.GetCurrentConfigurationInfo();
    }

    [McpServerTool]
    [Description("Switch to a different database within the current configuration")]
    public static string SwitchDatabase(
        ConfigService configService,
        [Description("The name of the database to switch to")] string databaseName)
    {
        return configService.SwitchDatabase(databaseName);
    }

    [McpServerTool]
    [Description("List all available databases in the current configuration")]
    public static string ListDatabases(ConfigService configService)
    {
        var databases = configService.ListAvailableDatabases();
        return JsonSerializer.Serialize(new
        {
            currentDatabase = configService.CurrentDatabase,
            availableDatabases = databases
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}