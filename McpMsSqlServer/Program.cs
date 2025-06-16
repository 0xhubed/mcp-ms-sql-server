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

// Configure logging - disable all console logging to avoid interfering with JSON-RPC
builder.Logging.ClearProviders();
// No logging providers added - this ensures nothing goes to stdout/stderr

var host = builder.Build();

// No startup logging to avoid stdout pollution
await host.RunAsync();