using System.Text.Json;
using McpMsSqlServer.Models;
using Xunit;

namespace McpMsSqlServer.Tests;

public class DatabaseConfigTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void EffectiveSchemas_UsesAllowedSchemasList_WhenProvided()
    {
        var config = JsonSerializer.Deserialize<DatabaseConfig>(
            """{ "allowedSchemas": ["dbo", "sales", "hr"] }""", Options)!;

        Assert.Equal(new[] { "dbo", "sales", "hr" }, config.EffectiveSchemas);
        Assert.Equal("dbo", config.PrimarySchema);
    }

    [Fact]
    public void EffectiveSchemas_FallsBackToLegacyAllowedSchema()
    {
        var config = JsonSerializer.Deserialize<DatabaseConfig>(
            """{ "allowedSchema": "sales" }""", Options)!;

        Assert.Equal(new[] { "sales" }, config.EffectiveSchemas);
        Assert.Equal("sales", config.PrimarySchema);
    }

    [Fact]
    public void EffectiveSchemas_ListWins_WhenBothProvided()
    {
        var config = JsonSerializer.Deserialize<DatabaseConfig>(
            """{ "allowedSchema": "dbo", "allowedSchemas": ["sales", "hr"] }""", Options)!;

        Assert.Equal(new[] { "sales", "hr" }, config.EffectiveSchemas);
        Assert.Equal("sales", config.PrimarySchema);
    }

    [Fact]
    public void EffectiveSchemas_IsEmpty_WhenNeitherProvided()
    {
        var config = JsonSerializer.Deserialize<DatabaseConfig>("{}", Options)!;

        Assert.Empty(config.EffectiveSchemas);
        Assert.Equal(string.Empty, config.PrimarySchema);
    }

    [Fact]
    public void EffectiveSchemas_DropsBlankAndDuplicateEntries()
    {
        var config = JsonSerializer.Deserialize<DatabaseConfig>(
            """{ "allowedSchemas": ["dbo", " ", "DBO", "sales", ""] }""", Options)!;

        Assert.Equal(new[] { "dbo", "sales" }, config.EffectiveSchemas);
    }
}
