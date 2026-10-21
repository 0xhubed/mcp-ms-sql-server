using Microsoft.Data.SqlClient;
using McpMsSqlServer.Models;

namespace McpMsSqlServer.Services;

/// <summary>A table name split into its optional schema qualifier and table part.</summary>
public sealed record TableReference(string? Schema, string Table);

/// <summary>A parameterised SQL predicate restricting a schema column to the configured schemas.</summary>
public sealed record SchemaFilter(string Sql, IReadOnlyDictionary<string, string> Parameters)
{
    public void ApplyTo(SqlCommand command)
    {
        foreach (var (name, value) in Parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
    }
}

/// <summary>
/// Pure helpers for working with the configured schema list: parsing user-supplied table
/// references, enforcing the allow-list, and building catalog filters that span every schema.
/// </summary>
public static class SchemaScope
{
    /// <summary>
    /// Splits <c>schema.table</c> or <c>[schema].[table]</c> into parts. A bare table name yields a null schema.
    /// </summary>
    /// <exception cref="ArgumentException">The input is blank, has an empty part, or has more than two parts.</exception>
    public static TableReference ParseTableReference(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Table name cannot be empty.", nameof(input));

        var parts = input.Trim().Split('.');
        if (parts.Length > 2)
            throw new ArgumentException($"Table reference '{input}' must be 'table' or 'schema.table'.", nameof(input));

        var cleaned = parts.Select(p => p.Trim().TrimStart('[').TrimEnd(']').Trim()).ToArray();
        if (cleaned.Any(p => p.Length == 0))
            throw new ArgumentException($"Table reference '{input}' has an empty schema or table part.", nameof(input));

        return cleaned.Length == 2
            ? new TableReference(cleaned[0], cleaned[1])
            : new TableReference(null, cleaned[0]);
    }

    /// <summary>True when the schema is in the configured list, or when no schema restriction is configured.</summary>
    public static bool IsAllowed(string schema, DatabaseConfig config)
    {
        var schemas = config.EffectiveSchemas;
        return schemas.Count == 0 || schemas.Contains(schema, StringComparer.OrdinalIgnoreCase);
    }

    /// <exception cref="UnauthorizedAccessException">The schema is outside the configured list.</exception>
    public static void EnsureAllowed(string schema, DatabaseConfig config)
    {
        if (!IsAllowed(schema, config))
        {
            throw new UnauthorizedAccessException(
                $"Access to schema '{schema}' is not allowed. Allowed schemas: {string.Join(", ", config.EffectiveSchemas)}");
        }
    }

    /// <summary>
    /// Builds <c>column IN (@schema0, @schema1, ...)</c> over the configured schemas,
    /// or an always-true predicate when no restriction is configured.
    /// </summary>
    public static SchemaFilter BuildSchemaFilter(string columnExpression, DatabaseConfig config)
    {
        var schemas = config.EffectiveSchemas;
        if (schemas.Count == 0)
            return new SchemaFilter("1 = 1", new Dictionary<string, string>());

        var parameters = new Dictionary<string, string>();
        for (var i = 0; i < schemas.Count; i++)
        {
            parameters[$"@schema{i}"] = schemas[i];
        }

        return new SchemaFilter($"{columnExpression} IN ({string.Join(", ", parameters.Keys)})", parameters);
    }

    /// <summary>
    /// Resolves a table reference without touching the database: a qualified name is validated
    /// against the allow-list, a bare name takes the configured primary schema.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The qualifier names a schema outside the allow-list.</exception>
    public static TableReference ResolveWithDefault(string input, DatabaseConfig config)
    {
        var reference = ParseTableReference(input);
        if (reference.Schema is null)
            return reference with { Schema = config.PrimarySchema };

        EnsureAllowed(reference.Schema, config);
        return reference;
    }

    /// <summary>Formats a reference as <c>[schema].[table]</c> for use in generated SQL.</summary>
    public static string QualifiedName(TableReference reference) => $"[{reference.Schema}].[{reference.Table}]";

    /// <summary>
    /// Resolves a user-supplied table reference to a concrete (schema, table) pair.
    /// A qualified name is validated against the allow-list. A bare name is looked up across
    /// every allowed schema; it must match exactly one, otherwise the caller gets an actionable error.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The qualifier names a schema outside the allow-list.</exception>
    /// <exception cref="InvalidOperationException">The bare name matches zero or several allowed schemas.</exception>
    public static async Task<TableReference> ResolveAsync(SqlConnection connection, DatabaseConfig config, string input)
    {
        var reference = ParseTableReference(input);
        if (reference.Schema is not null)
        {
            EnsureAllowed(reference.Schema, config);
            return reference;
        }

        if (config.EffectiveSchemas.Count == 1)
            return reference with { Schema = config.EffectiveSchemas[0] };

        var filter = BuildSchemaFilter("TABLE_SCHEMA", config);
        var query = $@"
            SELECT TABLE_SCHEMA
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @tableName AND {filter.Sql}
            ORDER BY TABLE_SCHEMA";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@tableName", reference.Table);
        filter.ApplyTo(command);

        var matches = new List<string>();
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                matches.Add(reader.GetString(0));
            }
        }

        return matches.Count switch
        {
            1 => reference with { Schema = matches[0] },
            0 => throw new InvalidOperationException(
                $"Table '{reference.Table}' was not found in any allowed schema ({string.Join(", ", config.EffectiveSchemas)})."),
            _ => throw new InvalidOperationException(
                $"Table '{reference.Table}' exists in several schemas ({string.Join(", ", matches)}). Qualify it as schema.table."),
        };
    }
}
