using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using McpMsSqlServer.Services;

var builder = Host.CreateApplicationBuilder(args);

// Register services
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<SecurityService>();
builder.Services.AddSingleton<TransactionService>();

// Add MCP Server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Configure logging
if (Environment.GetEnvironmentVariable("MCP_DEBUG") == "true")
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
}

var host = builder.Build();

// Log startup
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("MCP SQL Server starting...");

// Test services are available
var configService = host.Services.GetRequiredService<ConfigService>();
logger.LogInformation($"Loaded configurations: {string.Join(", ", configService.ListConfigurations())}");

await host.RunAsync();