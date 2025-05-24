using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class ConnectionTools
{
    [McpServerTool]
    [Description("Test database connectivity with current configuration")]
    public static async Task<string> TestConnection(DatabaseService databaseService)
    {
        try
        {
            return await databaseService.TestConnectionDetailedAsync();
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }
}