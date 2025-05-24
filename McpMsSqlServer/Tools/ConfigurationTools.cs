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
}