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

    [Fact]
    public void ResolveWithDefault_UsesPrimarySchema_ForBareName()
    {
        var reference = SchemaScope.ResolveWithDefault("Orders", Config("dbo", "sales"));

        Assert.Equal("dbo", reference.Schema);
        Assert.Equal("Orders", reference.Table);
    }

    [Fact]
    public void ResolveWithDefault_KeepsQualifiedSchema_WhenAllowed()
    {
        var reference = SchemaScope.ResolveWithDefault("sales.Orders", Config("dbo", "sales"));

        Assert.Equal("sales", reference.Schema);
        Assert.Equal("Orders", reference.Table);
    }

    [Fact]
    public void ResolveWithDefault_RejectsQualifiedSchema_OutsideList()
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => SchemaScope.ResolveWithDefault("hr.People", Config("dbo", "sales")));
    }

    [Fact]
    public void BuildInFilter_UsesGivenPrefix_AndMatchesNothingWhenEmpty()
    {
        var filter = SchemaScope.BuildInFilter("OBJECT_NAME(fk.parent_object_id)", new[] { "Orders", "Lines" }, "table");

        Assert.Equal("OBJECT_NAME(fk.parent_object_id) IN (@table0, @table1)", filter.Sql);
        Assert.Equal(new Dictionary<string, string> { ["@table0"] = "Orders", ["@table1"] = "Lines" }, filter.Parameters);

        var empty = SchemaScope.BuildInFilter("x", Array.Empty<string>(), "table");
        Assert.Equal("1 = 0", empty.Sql);
        Assert.Empty(empty.Parameters);
    }

    [Fact]
    public void QualifiedName_BracketsBothParts()
    {
        Assert.Equal("[sales].[Order Lines]", SchemaScope.QualifiedName(new TableReference("sales", "Order Lines")));
    }
}
