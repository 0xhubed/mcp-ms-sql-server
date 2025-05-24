# MCP SQL Server Project

## Overview
This project implements a Model Context Protocol (MCP) server in C# that provides secure access to MS SQL databases through Claude Code. The server supports multiple project configurations with granular permissions and comprehensive database operations.

## Project Structure
```
McpMsSqlServer/
├── Program.cs              # Entry point and MCP server setup
├── Tools/                  # MCP tool implementations
│   ├── QueryTools.cs       # Execute SELECT queries
│   ├── InsertTools.cs      # Insert new records
│   ├── UpdateTools.cs      # Update existing records
│   ├── DeleteTools.cs      # Delete records
│   ├── SchemaTools.cs      # Database schema inspection
│   ├── TableInfoTools.cs   # Table metadata and structure
│   ├── ConfigurationTools.cs # Manage configurations
│   ├── ConnectionTools.cs  # Test database connectivity
│   ├── QueryBuilderTool.cs # Generate queries from natural language
│   ├── PerformanceAnalysisTool.cs # Query performance analysis
│   └── DataDiscoveryTool.cs # Data discovery and profiling
├── Services/
│   ├── DatabaseService.cs   # SQL connection and execution logic
│   ├── SecurityService.cs   # Query validation and sanitization
│   ├── ConfigService.cs     # Configuration management
│   └── TransactionService.cs # Transaction management
├── Models/
│   └── DatabaseConfig.cs   # Database configuration models
├── Configurations/         # Project-based configurations
│   ├── default.json
│   ├── project1.json
│   └── analytics.json
└── appsettings.json
```

## Development Commands

### Build and Run
```bash
# Build the project
dotnet build

# Run the MCP server
dotnet run --project McpMsSqlServer

# Run with specific configuration
MCP_CONFIG_NAME=project1 dotnet run --project McpMsSqlServer

# Run tests
dotnet test

# Run with debug logging
MCP_DEBUG=true dotnet run --project McpMsSqlServer
```

### Package Management
```bash
# Required packages (already added)
# - ModelContextProtocol --version 0.1.0-preview.1.25171.12
# - Microsoft.Data.SqlClient
```

## Core MCP Tools

### Total Implemented: 16 Tools

### Phase 1 Tools (Configuration Management)
1. **TestConnection** - Test database connectivity with current configuration
2. **ListConfigurations** - List all available project configurations
3. **SwitchConfiguration** - Switch to a different project configuration
4. **GetCurrentConfiguration** - Get current configuration details

### Phase 2 Tools (Core Database Operations)
5. **ExecuteQuery** - Execute SELECT queries within configured schema
6. **GetSchemaInfo** - Get database schema information
7. **GetTableInfo** - Get detailed table metadata and sample data
8. **InsertRecords** - Insert new records with transaction support
9. **UpdateRecords** - Update existing records (requires WHERE clause)
10. **DeleteRecords** - Delete records (requires WHERE clause)

### Phase 3 Tools (Advanced Features)
11. **BuildQuery** - Generate SQL queries from natural language descriptions
12. **AnalyzeQueryPerformance** - Analyze query performance and provide optimization suggestions
13. **GetDatabasePerformanceStats** - Get database performance statistics and recommendations
14. **DiscoverData** - Search for tables/columns by name patterns and discover relationships
15. **AnalyzeTableRelationships** - Find all relationships and dependencies for specified tables
16. **ProfileDataQuality** - Profile data quality and statistics for specified tables

## Security Features
- Schema-based access control
- Mandatory WHERE clauses for updates/deletes
- Transaction management for write operations
- Query validation and sanitization
- Parameterized queries to prevent SQL injection
- Per-project permission configuration
- Audit logging for write operations

## Configuration Structure
Each project configuration includes:
- Connection string
- Allowed schema
- Read/write/delete permissions
- Security settings (max rows, audit logging)
- Query settings (timeout, joins allowed)
- Restricted tables list
- Allowed operations

## Implementation Status
- [x] Phase 1: Basic MCP Server Setup ✅ COMPLETED
  - [x] .NET console application created
  - [x] MCP SDK (v0.2.0-preview.1) and SQL Client packages added
  - [x] Project structure with folders
  - [x] STDIO transport configured
  - [x] Basic server initialization with hosting
  - [x] Configuration service and models
  - [x] Database connection service
  - [x] Security and Transaction services
  - [x] Connection testing tool
  - [x] Configuration management tools (4 tools)
  - [x] Sample configuration files
  - [x] Successful build with all core services
- [x] Phase 2: Core Database Tools ✅ COMPLETED
  - [x] QueryTools - Execute SELECT queries
  - [x] SchemaTools - Database schema inspection  
  - [x] TableInfoTools - Table metadata and structure
  - [x] InsertTools - Insert new records
  - [x] UpdateTools - Update existing records
  - [x] DeleteTools - Delete records
- [x] Phase 3: Advanced Features ✅ COMPLETED
  - [x] QueryBuilderTool - Natural language query generation
  - [x] PerformanceAnalysisTool - Query performance analysis
  - [x] DataDiscoveryTool - Data discovery and profiling
- [ ] Phase 4: Claude Code Integration

## Testing Strategy
- Unit tests for all services
- Integration tests for MCP communication
- Security tests for SQL injection prevention
- End-to-end tests with Claude Code

## Known Issues
None at this time - project is in initial setup phase.

## Notes
- Always use parameterized queries
- Validate schema access before query execution
- Log all write operations when audit is enabled
- Use transactions for data consistency
- Respect per-project permission settings