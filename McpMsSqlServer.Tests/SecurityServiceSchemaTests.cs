using McpMsSqlServer.Services;
using Xunit;

namespace McpMsSqlServer.Tests;

// Tests in one class run sequentially under xunit, which keeps the
// process-wide MCP_CONFIG_* environment variables from racing.
public class SecurityServiceSchemaTests : IDisposable
{
    private readonly string _configDir;

    public SecurityServiceSchemaTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "mcp-mssql-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("MCP_CONFIG_PATH", null);
        Environment.SetEnvironmentVariable("MCP_CONFIG_NAME", null);
        Directory.Delete(_configDir, recursive: true);
    }

    private SecurityService Build(string json)
    {
        File.WriteAllText(Path.Combine(_configDir, "test.json"), json);
        Environment.SetEnvironmentVariable("MCP_CONFIG_PATH", _configDir);
        Environment.SetEnvironmentVariable("MCP_CONFIG_NAME", "test");
        return new SecurityService(new ConfigService());
    }

    [Fact]
    public void IsSchemaAllowed_AcceptsAnySchemaInList_CaseInsensitive()
    {
        var security = Build("""{ "name": "t", "connectionString": "x", "allowedSchemas": ["dbo", "sales"] }""");

        Assert.True(security.IsSchemaAllowed("dbo"));
        Assert.True(security.IsSchemaAllowed("SALES"));
        Assert.False(security.IsSchemaAllowed("hr"));
    }

    [Fact]
    public void IsSchemaAllowed_StillHonoursLegacySingleSchema()
    {
        var security = Build("""{ "name": "t", "connectionString": "x", "allowedSchema": "sales" }""");

        Assert.True(security.IsSchemaAllowed("sales"));
        Assert.False(security.IsSchemaAllowed("dbo"));
    }

    [Fact]
    public void IsSchemaAllowed_AllowsEverything_WhenNoSchemaConfigured()
    {
        var security = Build("""{ "name": "t", "connectionString": "x" }""");

        Assert.True(security.IsSchemaAllowed("anything"));
    }
}
