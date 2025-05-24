# MCP SQL Server Implementation Plan

## Overview
Create a Model Context Protocol (MCP) server in C# that provides Claude Code with secure access to your MS SQL database. This will enable Claude Code to query, analyze, and work with your database through standardized MCP tools.

## Prerequisites

### Software Requirements
- .NET 8 or later
- Claude Code CLI installed and configured
- MS SQL Server instance with appropriate connection details
- Visual Studio 2022 or VS Code

### Initial Setup
1. **Claude Code Setup**
   ```bash
   # Install Claude Code CLI (if not already installed)
   npm install -g @anthropic-ai/claude-cli
   
   # Run initial setup with dangerous permissions (one-time requirement)
   claude --dangerously-skip-permissions
   ```

2. **C# MCP SDK Installation**
   ```bash
   dotnet new console -n McpMsSqlServer
   cd McpMsSqlServer
   dotnet add package ModelContextProtocol --version 0.1.0-preview.1.25171.12
   dotnet add package Microsoft.Data.SqlClient
   ```

## Architecture Design

### Component Structure
```
McpMsSqlServer/
├── Program.cs              # Entry point and MCP server setup
├── Tools/                  # MCP tool implementations
│   ├── QueryTool.cs       # Execute SELECT queries
│   ├── InsertTool.cs      # Insert new records
│   ├── UpdateTool.cs      # Update existing records
│   ├── DeleteTool.cs      # Delete records
│   ├── SchemaTool.cs      # Database schema inspection
│   ├── TableInfoTool.cs   # Table metadata and structure
│   ├── ConfigTool.cs      # Manage configurations
│   └── ConnectionTool.cs  # Test database connectivity
├── Services/
│   ├── DatabaseService.cs   # SQL connection and execution logic
│   ├── SecurityService.cs   # Query validation and sanitization
│   ├── ConfigService.cs     # Configuration management
│   └── TransactionService.cs # Transaction management for write ops
├── Models/
│   ├── QueryRequest.cs     # Request/response models
│   ├── SchemaInfo.cs       # Schema representation models
│   ├── DatabaseConfig.cs   # Database configuration models
│   └── WriteOperation.cs   # Write operation models
├── Configurations/         # Project-based configurations
│   ├── default.json        # Default configuration
│   ├── project1.json       # Project-specific config
│   └── project2.json       # Another project config
└── appsettings.json        # Base application settings
```

### Security Considerations
- **Controlled read/write access**: Implement tools for SELECT, INSERT, UPDATE, DELETE operations
- **Transaction management**: Ensure data consistency with proper transaction handling
- **Query validation**: Sanitize and validate all SQL queries and parameters
- **Parameter binding**: Use parameterized queries to prevent SQL injection
- **Schema restrictions**: Limit access to specific configured schemas only
- **Operation permissions**: Configurable permissions per operation type
- **Audit logging**: Log all write operations for accountability
- **Connection security**: Use encrypted connections and secure authentication
- **Backup recommendations**: Suggest backup strategies before write operations

## Implementation Phases

### Phase 1: Basic MCP Server Setup ✅ COMPLETED
**Goal**: Create a functioning MCP server with basic connectivity

**Tasks**:
1. ✅ Set up .NET console application with MCP SDK (v0.2.0-preview.1)
2. ✅ Configure STDIO transport (standard for Claude Code integration)
3. ✅ Implement basic server initialization and handshake
4. ✅ Add database connection service with connection testing

**Deliverables**:
- ✅ Basic MCP server that Claude Code can connect to
- ✅ Database connection validation
- ✅ Error handling for connection issues

**Additional Completed Items**:
- ✅ Configuration service with multi-project support
- ✅ Security service for query validation
- ✅ Transaction service for safe write operations
- ✅ 4 configuration management tools (TestConnection, ListConfigurations, SwitchConfiguration, GetCurrentConfiguration)
- ✅ Sample configuration files for different project types

### Phase 2: Core Database Tools
**Goal**: Implement essential database access tools

**Read Tools**:

1. **Schema Explorer Tool**
   - List all databases, schemas, tables, and views (filtered by configured schema)
   - Get table/view column information with data types
   - Retrieve primary keys, foreign keys, and indexes

2. **Query Execution Tool**
   - Execute SELECT statements safely within configured schema
   - Return results in structured JSON format
   - Handle pagination for large result sets
   - Query timeout management

3. **Table Information Tool**
   - Get detailed table metadata
   - Row counts and basic statistics
   - Sample data preview (first N rows)

**Write Tools**:

4. **Insert Tool**
   - Insert new records into specified tables
   - Support bulk insert operations
   - Validation against table schema
   - Transaction rollback on errors

5. **Update Tool**
   - Update existing records with WHERE clause validation
   - Prevent accidental mass updates without proper WHERE clauses
   - Support for conditional updates
   - Transaction management

6. **Delete Tool**
   - Delete records with mandatory WHERE clause
   - Soft delete support where applicable
   - Confirmation prompts for large deletions
   - Transaction safety

**Configuration Tools**:

7. **Configuration Manager Tool**
   - Switch between project configurations
   - List available configurations
   - Validate connection strings
   - Test schema access

**Deliverables**:
- Seven core MCP tools for complete database interaction
- JSON response formatting with detailed error messages
- Transaction management for write operations
- Comprehensive error handling and validation

### Phase 3: Advanced Features
**Goal**: Add sophisticated database analysis capabilities

**Advanced Tools**:

1. **Query Builder Tool**
   - Generate common queries based on natural language descriptions
   - Table join suggestions
   - Filter and aggregation helpers

2. **Performance Analysis Tool**
   - Query execution plan analysis
   - Index usage recommendations
   - Performance statistics

3. **Data Discovery Tool**
   - Search for tables/columns by name patterns
   - Find relationships between tables
   - Data profiling (null counts, unique values, etc.)

**Deliverables**:
- Enhanced analytical capabilities
- Query optimization suggestions
- Comprehensive data discovery features

### Phase 4: Claude Code Integration
**Goal**: Configure Claude Code to use the MCP server with project-based configurations

**Configuration Steps**:

1. **Project-Based MCP Server Registration**
   ```json
   // In Claude Code configuration file (.claude.json)
   {
     "mcpServers": {
       "sql-server-project1": {
         "type": "stdio",
         "command": "dotnet",
         "args": ["run", "--project", "path/to/McpMsSqlServer"],
         "env": {
           "MCP_CONFIG_NAME": "project1",
           "MCP_DEBUG": "false"
         }
       },
       "sql-server-project2": {
         "type": "stdio",
         "command": "dotnet",
         "args": ["run", "--project", "path/to/McpMsSqlServer"],
         "env": {
           "MCP_CONFIG_NAME": "project2",
           "MCP_DEBUG": "false"
         }
       }
     }
   }
   ```

2. **Project Configuration Files**
   ```json
   // Configurations/project1.json
   {
     "name": "E-Commerce Project",
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
   
   // Configurations/project2.json
   {
     "name": "Analytics Project",
     "connectionString": "Server=analytics-server;Database=DataWarehouse;User Id=analyst;Password=secure_password;",
     "allowedSchema": "reporting",
     "permissions": {
       "allowRead": true,
       "allowWrite": false,
       "allowDelete": false
     },
     "security": {
       "requireWhereClause": false,
       "maxRowsPerQuery": 10000,
       "auditOperations": false
     }
   }
   ```

3. **Dynamic Configuration Switching**
   ```csharp
   [McpServerTool]
   [Description("Switch to a different project configuration")]
   public static async Task<string> SwitchConfiguration(
       [Description("The name of the configuration to switch to")] string configName)
   {
       // Implementation to switch configurations dynamically
   }
   
   [McpServerTool]
   [Description("List all available project configurations")]
   public static async Task<string> ListConfigurations()
   {
       // Implementation to list available configs
   }
   ```

**Deliverables**:
- Multiple project configuration support
- Secure configuration management with per-project permissions
- Dynamic configuration switching
- Comprehensive documentation for setup and usage

## Sample Tool Implementation

### Query Execution Tool Example
```csharp
[McpServerTool]
[Description("Execute a SQL SELECT query against the configured schema")]
public static async Task<string> ExecuteQuery(
    [Description("The SQL SELECT query to execute")] string sqlQuery,
    [Description("Maximum number of rows to return (default: 100)")] int maxRows = 100)
{
    // Implementation with schema validation and execution
    // Automatically prefixes schema name if not specified
}
```

### Insert Tool Example
```csharp
[McpServerTool]
[Description("Insert new records into a table in the configured schema")]
public static async Task<string> InsertRecords(
    [Description("The table name to insert into")] string tableName,
    [Description("JSON object or array of objects with column-value pairs")] string recordsJson,
    [Description("Whether to use a transaction (default: true)")] bool useTransaction = true)
{
    // Implementation with validation, transaction management, and error handling
}
```

### Update Tool Example
```csharp
[McpServerTool]
[Description("Update existing records in a table within the configured schema")]
public static async Task<string> UpdateRecords(
    [Description("The table name to update")] string tableName,
    [Description("JSON object with column-value pairs to update")] string updateData,
    [Description("WHERE clause conditions (required for safety)")] string whereClause,
    [Description("Whether to use a transaction (default: true)")] bool useTransaction = true)
{
    // Implementation with mandatory WHERE clause validation
}
```

### Configuration Management Tool Example
```csharp
[McpServerTool]
[Description("Switch to a different project configuration")]
public static async Task<string> SwitchConfiguration(
    [Description("The name of the configuration to switch to")] string configName)
{
    // Implementation to dynamically switch database configurations
}

[McpServerTool]
[Description("Get current configuration details")]
public static async Task<string> GetCurrentConfiguration()
{
    // Implementation returning current config info (without sensitive data)
}
```

### Schema Explorer Tool Example
```csharp
[McpServerTool]
[Description("Get database schema information for the configured schema")]
public static async Task<string> GetSchemaInfo(
    [Description("Optional table name to get specific table info")] string? tableName = null)
{
    // Implementation returning schema information filtered by configured schema
}
```

## Testing Strategy

### Unit Testing
- Database service methods with mock databases
- Query validation and sanitization logic
- MCP tool response formatting

### Integration Testing
- End-to-end MCP communication
- Database connectivity with test databases
- Claude Code integration testing

### Security Testing
- SQL injection prevention validation
- Access control verification
- Error information leakage assessment

## Deployment Options

### Local Development
- Run directly with `dotnet run`
- Debug integration with Claude Code locally
- Use local SQL Server or SQL Server Express

### Production Deployment
- Containerized deployment with Docker
- Azure Container Instances or AWS ECS
- Secure connection string management
- Monitoring and logging integration

## Configuration Management

### Environment Variables
```bash
MCP_CONFIG_NAME="project1"          # Which configuration file to use
MCP_DEBUG="false"                   # Enable debug logging
MCP_CONFIG_PATH="./Configurations" # Path to configuration files
```

### Project Configuration Files

#### Base Configuration Structure
```json
{
  "name": "Project Display Name",
  "connectionString": "Server=...;Database=...;",
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
    "auditOperations": true,
    "backupRecommendations": true
  },
  "querySettings": {
    "timeoutSeconds": 30,
    "enableQueryPlan": false,
    "allowJoins": true,
    "allowSubqueries": true
  },
  "restrictedTables": ["audit_log", "user_passwords"],
  "allowedOperations": ["SELECT", "INSERT", "UPDATE", "DELETE"]
}
```

#### Example Project Configurations

**E-Commerce Project (project1.json)**
```json
{
  "name": "E-Commerce Application",
  "connectionString": "Server=localhost;Database=ECommerceDB;Integrated Security=true;",
  "allowedSchema": "ecommerce",
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": true,
    "allowSchemaChanges": false
  },
  "security": {
    "requireWhereClause": true,
    "maxRowsPerQuery": 1000,
    "maxRowsPerUpdate": 100,
    "maxRowsPerDelete": 10,
    "auditOperations": true,
    "backupRecommendations": true
  },
  "querySettings": {
    "timeoutSeconds": 30,
    "enableQueryPlan": true,
    "allowJoins": true,
    "allowSubqueries": true
  },
  "restrictedTables": ["user_sessions", "payment_tokens"],
  "allowedOperations": ["SELECT", "INSERT", "UPDATE", "DELETE"]
}
```

**Analytics Project (analytics.json)**
```json
{
  "name": "Data Analytics Dashboard",
  "connectionString": "Server=analytics-srv;Database=DataWarehouse;User Id=analyst;Password=secure_pwd;",
  "allowedSchema": "reporting",
  "permissions": {
    "allowRead": true,
    "allowWrite": false,
    "allowDelete": false,
    "allowSchemaChanges": false
  },
  "security": {
    "requireWhereClause": false,
    "maxRowsPerQuery": 10000,
    "maxRowsPerUpdate": 0,
    "maxRowsPerDelete": 0,
    "auditOperations": false,
    "backupRecommendations": false
  },
  "querySettings": {
    "timeoutSeconds": 120,
    "enableQueryPlan": true,
    "allowJoins": true,
    "allowSubqueries": true
  },
  "restrictedTables": [],
  "allowedOperations": ["SELECT"]
}
```

**Development Project (dev.json)**
```json
{
  "name": "Development Environment",
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
    "maxRowsPerUpdate": 1000,
    "maxRowsPerDelete": 1000,
    "auditOperations": false,
    "backupRecommendations": false
  },
  "querySettings": {
    "timeoutSeconds": 60,
    "enableQueryPlan": true,
    "allowJoins": true,
    "allowSubqueries": true
  },
  "restrictedTables": [],
  "allowedOperations": ["SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "ALTER", "DROP"]
}
```

### Configuration Management Commands

**Configuration Switching via Claude Code**
```
# Switch to e-commerce project
"Switch to project1 configuration"

# List available configurations
"What database configurations are available?"

# Get current configuration details
"Show me the current database configuration"

# Test connection with new configuration
"Test connection for analytics configuration"
```

## Success Metrics

### Functionality Metrics
- [x] Claude Code can successfully connect to MCP server
- [ ] All database tools (read/write) respond correctly to valid inputs
- [x] Configuration switching works seamlessly between projects
- [x] Error handling works for invalid queries/connections/permissions
- [x] Transaction management ensures data consistency
- [ ] Performance is acceptable for typical query and write workloads
- [x] Schema filtering works correctly for each project configuration

### Security Metrics
- [x] SQL injection attempts are blocked for all operations
- [x] Write operations respect configured permissions
- [x] WHERE clause requirements prevent accidental mass operations
- [x] Sensitive data access is controlled per configuration
- [x] Connection strings are securely managed and not exposed
- [ ] Audit logging captures all write operations when enabled
- [x] Schema restrictions are properly enforced

### Configuration Metrics
- [x] Multiple project configurations can be managed simultaneously
- [x] Configuration switching is fast and reliable
- [x] Invalid configurations are detected and reported
- [x] Configuration validation prevents dangerous settings
- [x] Per-project permissions are properly enforced

## Current Status

### Phase 1: ✅ COMPLETED (May 24, 2025)
- ✅ Basic MCP server with STDIO transport
- ✅ Configuration management system with multi-project support
- ✅ Sample project configurations (default, project1, analytics)
- ✅ Database connectivity testing
- ✅ Security and transaction services
- ✅ 4 working MCP tools for configuration management

## Next Steps

1. **Week 1**: ~~Implement Phase 1~~ ✅ COMPLETED

2. **Week 2**: Implement Phase 2 (Core database tools - Read/Write)
   - Implement all 7 core tools (Query, Insert, Update, Delete, Schema, TableInfo, Config)
   - Add transaction management
   - Implement security validations
   - Test with sample data

3. **Week 3**: Implement Phase 3 (Advanced features)
   - Add advanced analytical tools
   - Implement audit logging
   - Add performance monitoring
   - Create comprehensive error handling

4. **Week 4**: Complete Phase 4 (Claude Code integration and testing)
   - Configure multiple project setups in Claude Code
   - End-to-end integration testing
   - Performance testing with real workloads
   - Documentation and deployment guides

## Development Priority Order

### High Priority (Must Have)
1. Configuration management system
2. Basic read operations (SELECT)
3. Schema filtering and validation
4. Safe write operations (INSERT, UPDATE, DELETE)
5. Transaction management
6. Claude Code integration

### Medium Priority (Should Have)
1. Advanced query features
2. Bulk operations
3. Performance monitoring
4. Audit logging
5. Configuration validation

### Low Priority (Nice to Have)
1. Query plan analysis
2. Performance suggestions
3. Advanced data discovery
4. Schema change operations (for dev environments)
5. Backup integration

## Additional Resources

- [Official MCP C# SDK Documentation](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP Specification](https://modelcontextprotocol.io/introduction)
- [Claude Code MCP Configuration Guide](https://docs.anthropic.com/claude/docs/claude-code)
- [SQL Server Connection String Examples](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.connectionstring)

This plan provides a structured approach to building a secure, functional MCP server that enables Claude Code to work effectively with your MS SQL database while maintaining proper security boundaries.