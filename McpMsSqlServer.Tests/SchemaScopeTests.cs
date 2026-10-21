using McpMsSqlServer.Models;
using McpMsSqlServer.Services;
using Xunit;

namespace McpMsSqlServer.Tests;

public class SchemaScopeTests
{
    private static DatabaseConfig Config(params string[] schemas) => new() { AllowedSchemas = schemas.ToList() };

    [Fact]
    public void ParseTableReference_SplitsSchemaQualifiedName()
    {
        var reference = SchemaScope.ParseTableReference("sales.Orders");

        Assert.Equal("sales", reference.Schema);
        Assert.Equal("Orders", reference.Table);
    }

    [Fact]
    public void ParseTableReference_StripsBrackets()
    {
        var reference = SchemaScope.ParseTableReference("[sales].[Order Lines]");

        Assert.Equal("sales", reference.Schema);
        Assert.Equal("Order Lines", reference.Table);
    }

    [Fact]
    public void ParseTableReference_LeavesSchemaNull_ForBareName()
    {
        var reference = SchemaScope.ParseTableReference("Orders");

        Assert.Null(reference.Schema);
        Assert.Equal("Orders", reference.Table);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a.b.c")]
    [InlineData(".Orders")]
    [InlineData("sales.")]
    public void ParseTableReference_RejectsMalformedInput(string input)
    {
        Assert.Throws<ArgumentException>(() => SchemaScope.ParseTableReference(input));
    }

    [Fact]
    public void BuildSchemaFilter_ProducesParameterisedInClause()
    {
        var filter = SchemaScope.BuildSchemaFilter("t.TABLE_SCHEMA", Config("dbo", "sales"));

        Assert.Equal("t.TABLE_SCHEMA IN (@schema0, @schema1)", filter.Sql);
        Assert.Equal(new Dictionary<string, string> { ["@schema0"] = "dbo", ["@schema1"] = "sales" }, filter.Parameters);
    }

    [Fact]
    public void BuildSchemaFilter_IsAlwaysTrue_WhenNoSchemasConfigured()
    {
        var filter = SchemaScope.BuildSchemaFilter("t.TABLE_SCHEMA", Config());

        Assert.Equal("1 = 1", filter.Sql);
        Assert.Empty(filter.Parameters);
    }

    [Fact]
    public void EnsureAllowed_RejectsSchemaOutsideList()
    {
        var ex = Assert.Throws<UnauthorizedAccessException>(
            () => SchemaScope.EnsureAllowed("hr", Config("dbo", "sales")));

        Assert.Contains("hr", ex.Message);
        Assert.Contains("dbo, sales", ex.Message);
    }

    [Fact]
    public void EnsureAllowed_AcceptsListedSchema_CaseInsensitive()
    {
        SchemaScope.EnsureAllowed("SALES", Config("dbo", "sales"));
    }
}
