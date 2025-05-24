# MCP Microsoft SQL Server

A configurable **Model Context Protocol (MCP) server** for Microsoft SQL Server integration with Claude Code and other MCP clients. Enables AI assistants to securely interact with SQL Server databases through project-based configurations with full read/write capabilities.

## 🌟 Features

### 🔧 **Project-Based Configuration**
- **Multiple database connections** - Switch between different projects and databases
- **Schema-specific access** - Restrict access to specific schemas per project
- **Configurable permissions** - Fine-grained control over read/write/delete operations
- **Environment-based setup** - Different configurations for dev, staging, and production

### 🛡️ **Security & Safety**
- **Transaction management** - Automatic rollback on errors for write operations
- **Query validation** - Prevent SQL injection and validate all operations
- **WHERE clause enforcement** - Mandatory WHERE clauses for UPDATE/DELETE operations
- **Row limit restrictions** - Configurable limits to prevent accidental mass operations
- **Audit logging** - Track all database operations for accountability

### 🔍 **Database Operations**
- **Read Operations**: SELECT queries with pagination and filtering
- **Write Operations**: INSERT, UPDATE, DELETE with transaction safety
- **Schema Exploration**: Browse tables, columns, relationships, and indexes
- **Table Management**: Get metadata, statistics, and sample data
- **Configuration Management**: Switch between project configurations dynamically

### 🚀 **AI Integration**
- **Claude Code integration** - Seamless setup with Claude's development environment
- **MCP protocol compliance** - Works with any MCP-compatible client
- **Natural language interface** - Interact with databases using plain English
- **Error handling** - Clear, actionable error messages for AI and humans

## 📋 Prerequisites

- **.NET 8.0 or later**
- **Microsoft SQL Server** (any supported version)
- **Claude Code CLI** (for Claude integration)
- **Appropriate database permissions** for the operations you want to perform

## 🚀 Quick Start

### 1. Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/mcp-ms-sql-server.git
cd mcp-ms-sql-server

# Build the project
dotnet build

# Test the installation
dotnet run -- --help
```

### 2. Create Project Configuration

Create a configuration file for your project in the `Configurations/` directory:

```json
// Configurations/my-project.json
{
  "name": "My E-Commerce Project",
  "connectionString": "Server=localhost;Database=ECommerceDB;Integrated Security=true;",
  "allowedSchema": "ecommerce",
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": false
  },
  "security": {
    "requireWhereClause": true,
    "maxRowsPerQuery": 1000,
    "auditOperations": true
  }
}
```

### 3. Configure Claude Code

Add to your Claude Code configuration (`.claude.json`):

```json
{
  "mcpServers": {
    "sql-server": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "path/to/mcp-ms-sql-server"],
      "env": {
        "MCP_CONFIG_NAME": "my-project"
      }
    }
  }
}
```

### 4. Start Using

```bash
# In Claude Code, you can now ask:
"Show me all tables in the database"
"Insert a new customer with name 'John Doe' and email 'john@example.com'"
"What are the top 10 products by sales?"
"Update the price of product with ID 123 to $29.99"
```

## 📖 Configuration Guide

### Project Configuration Structure

```json
{
  "name": "Project Display Name",
  "connectionString": "Your SQL Server connection string",
  "allowedSchema": "schema_name",
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": false,
    "allowSchemaChanges": false
  },
  "security": {
    "requireWhereClause": true,
    "maxRowsPerQuery": 1000,
    "maxRowsPerUpdate": 100,
    "maxRowsPerDelete": 10,
    "auditOperations": true
  },
  "querySettings": {
    "timeoutSeconds": 30,
    "enableQueryPlan": false,
    "allowJoins": true,
    "allowSubqueries": true
  },
  "restrictedTables": ["sensitive_table", "audit_log"],
  "allowedOperations": ["SELECT", "INSERT", "UPDATE", "DELETE"]
}
```

### Example Configurations

<details>
<summary><strong>Development Environment</strong></summary>

```json
{
  "name": "Development Database",
  "connectionString": "Server=dev-server;Database=DevDB;Integrated Security=true;",
  "allowedSchema": "dbo",
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": true,
    "allowSchemaChanges": true
  },
  "security": {
    "requireWhereClause": false,
    "maxRowsPerQuery": 5000,
    "auditOperations": false
  }
}
```
</details>

<details>
<summary><strong>Production Environment</strong></summary>

```json
{
  "name": "Production Database",
  "connectionString": "Server=prod-server;Database=ProdDB;User Id=app_user;Password=secure_password;",
  "allowedSchema": "app",
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": false,
    "allowSchemaChanges": false
  },
  "security": {
    "requireWhereClause": true,
    "maxRowsPerQuery": 100,
    "maxRowsPerUpdate": 10,
    "auditOperations": true
  },
  "restrictedTables": ["user_passwords", "payment_info"]
}
```
</details>

## 🛠️ Available Tools

| Tool | Description | Permissions Required |
|------|-------------|---------------------|
| `execute_query` | Run SELECT statements | `allowRead` |
| `insert_records` | Insert new data | `allowWrite` |
| `update_records` | Modify existing data | `allowWrite` |
| `delete_records` | Remove data | `allowDelete` |
| `get_schema_info` | Explore database structure | `allowRead` |
| `get_table_info` | Get table details and stats | `allowRead` |
| `switch_configuration` | Change active project | Always allowed |
| `list_configurations` | Show available configs | Always allowed |

## 🔒 Security Considerations

### Best Practices

- **Use dedicated database users** with minimal required permissions
- **Enable audit logging** for production environments
- **Set appropriate row limits** to prevent accidental mass operations
- **Restrict sensitive tables** using the `restrictedTables` configuration
- **Use WHERE clause requirements** for UPDATE/DELETE operations
- **Regular security reviews** of configurations and permissions

### Connection String Security

```bash
# Use environment variables for sensitive data
export DB_PASSWORD="your_secure_password"
```

```json
{
  "connectionString": "Server=myserver;Database=mydb;User Id=myuser;Password=${DB_PASSWORD};"
}
```

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Setup

```bash
# Clone the repo
git clone https://github.com/yourusername/mcp-ms-sql-server.git
cd mcp-ms-sql-server

# Install dependencies
dotnet restore

# Run tests
dotnet test

# Start development server
dotnet run -- --config dev
```

## 📚 Documentation

- [Configuration Guide](docs/configuration-guide.md)
- [Claude Code Setup](docs/claude-code-setup.md)
- [Security Best Practices](docs/security-guide.md)
- [API Reference](docs/api-reference.md)
- [Troubleshooting](docs/troubleshooting.md)

## 🐛 Troubleshooting

### Common Issues

**Connection Failed**
```bash
# Test your connection string
dotnet run -- --test-connection --config your-project
```

**Permission Denied**
- Check your database user permissions
- Verify the `allowedSchema` configuration
- Ensure the user has access to the specified schema

**Configuration Not Found**
- Verify the configuration file exists in `Configurations/`
- Check the `MCP_CONFIG_NAME` environment variable
- Ensure the JSON syntax is valid

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🌟 Acknowledgments

- [Model Context Protocol](https://modelcontextprotocol.io/) by Anthropic
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) by Microsoft and Anthropic
- The open-source community for inspiration and feedback

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/mcp-ms-sql-server/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/mcp-ms-sql-server/discussions)
- **Documentation**: [Wiki](https://github.com/yourusername/mcp-ms-sql-server/wiki)

---

**Made with ❤️ for the MCP and AI development community**
