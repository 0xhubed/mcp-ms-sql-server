# MCP Microsoft SQL Server

Un **servidor del Protocolo de Contexto de Modelo (MCP)** configurable para la integración de Microsoft SQL Server con Claude Code y otros clientes MCP. Permite que los asistentes de IA interactúen de forma segura con bases de datos SQL Server a través de configuraciones basadas en proyectos con capacidades completas de lectura/escritura.

## 🌟 Características

### 🔧 **Configuración Basada en Proyectos**
- **Múltiples conexiones a bases de datos** - Cambia entre diferentes proyectos y bases de datos.
- **Acceso específico por esquema** - Restringe el acceso a esquemas específicos por proyecto.
- **Permisos configurables** - Control detallado sobre las operaciones de lectura/escritura/eliminación.
- **Configuración basada en entornos** - Configuraciones diferentes para desarrollo (dev), staging y producción.

### 🛡️ **Seguridad y Protección**
- **Gestión de transacciones** - Rollback automático en caso de errores para operaciones de escritura.
- **Validación de consultas** - Previene la inyección de SQL y valida todas las operaciones.
- **Obligatoriedad de la cláusula WHERE** - Cláusulas WHERE obligatorias para operaciones de UPDATE/DELETE.
- **Restricciones de límite de filas** - Límites configurables para evitar operaciones masivas accidentales.
- **Registro de auditoría (Audit logging)** - Rastrea todas las operaciones de la base de datos para mayor rendición de cuentas.

### 🔍 **Operaciones de Base de Datos**
- **Operaciones de Lectura**: Consultas SELECT con paginación y filtrado.
- **Operaciones de Escritura**: INSERT, UPDATE, DELETE con seguridad transaccional.
- **Exploración de Esquemas**: Navega por tablas, columnas, relaciones e índices.
- **Gestión de Tablas**: Obtén metadatos, estadísticas y datos de muestra.
- **Gestión de Configuración**: Cambia entre configuraciones de proyecto dinámicamente.

### 🚀 **Integración con IA**
- **Integración con Claude Desktop** - Configuración sencilla con la aplicación Claude Desktop.
- **Cumplimiento del protocolo MCP** - Funciona con cualquier cliente compatible con MCP.
- **Interfaz de lenguaje natural** - Interactúa con las bases de datos utilizando lenguaje sencillo.
- **Manejo de errores** - Mensajes de error claros y accionables para la IA y los humanos.

## 📋 Requisitos Previos

- **.NET 9.0 o posterior**
- **Microsoft SQL Server** (cualquier versión compatible)
- **Claude Desktop** (para la integración con Claude)
- **Permisos de base de datos adecuados** para las operaciones que desees realizar.

## 🚀 Inicio Rápido

### 1. Instalación

```bash
# Clonar el repositorio
git clone https://github.com/yourusername/mcp-ms-sql-server.git
cd mcp-ms-sql-server

# Compilar el proyecto
dotnet build -c Release
```

### 2. Crear Configuración de Proyecto

Crea un archivo de configuración para tu proyecto en el directorio `Configurations/`:

```json
// Configurations/my-project.json
{
  "name": "My E-Commerce Project",
  "connectionString": "Server=localhost;Database=ECommerceDB;Integrated Security=true;",
  "allowedSchema": "dbo",
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

### 3. Configurar Claude Desktop

Añade lo siguiente a tu configuración de Claude Desktop:

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "sql-server": {
      "type": "stdio",
      "command": "C:\\Git\\mcp-ms-sql-server\\McpMsSqlServer\\bin\\Release\\net9.0\\McpMsSqlServer.exe",
      "env": {
        "MCP_CONFIG_NAME": "my-project"
      }
    }
  }
}
```

**Nota**: Primero compila el proyecto con `dotnet build -c Release` para crear el ejecutable.

### 4. Empezar a Usar

En Claude Desktop, ahora puedes preguntar:
- "Muéstrame todas las tablas de la base de datos"
- "Inserta un nuevo cliente con nombre 'John Doe' y correo 'john@example.com'"
- "¿Cuáles son los 10 productos más vendidos?"
- "Actualiza el precio del producto con ID 123 a $29.99"

## 📖 Guía de Configuración

### Estructura de Configuración del Proyecto

```json
{
  "name": "Nombre Visible del Proyecto",
  "connectionString": "Tu cadena de conexión de SQL Server",
  "allowedSchema": "nombre_del_esquema",
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
  "restrictedTables": ["tabla_sensible", "registro_auditoria"],
  "allowedOperations": ["SELECT", "INSERT", "UPDATE", "DELETE"]
}
```

### Ejemplos de Configuración

<details>
<summary><strong>Entorno de Desarrollo</strong></summary>

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
<summary><strong>Entorno de Producción</strong></summary>

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

## 🛠️ Herramientas Disponibles

### Gestión de Configuración (4 herramientas)
- **ListConfigurations** - Muestra todas las configuraciones de proyecto disponibles.
- **SwitchConfiguration** - Cambia a una configuración de proyecto diferente.
- **GetCurrentConfiguration** - Muestra los detalles de la configuración actual.
- **TestConnection** - Prueba la conectividad a la base de datos.

### Operaciones Core de Base de Datos (6 herramientas)
- **ExecuteQuery** - Ejecuta consultas SELECT dentro del esquema permitido.
- **GetSchemaInfo** - Explora la estructura y los objetos de la base de datos.
- **GetTableInfo** - Obtiene metadatos de la tabla y datos de muestra.
- **InsertRecords** - Inserta nuevos registros con soporte de transacciones.
- **UpdateRecords** - Actualiza registros existentes (requiere cláusula WHERE).
- **DeleteRecords** - Elimina registros (requiere cláusula WHERE).

### Características Avanzadas (6 herramientas)
- **BuildQuery** - Genera consultas SQL a partir de lenguaje natural.
- **AnalyzeQueryPerformance** - Analiza los planes de ejecución de las consultas.
- **GetDatabasePerformanceStats** - Métricas de rendimiento de la base de datos.
- **DiscoverData** - Busca tablas/columnas mediante patrones.
- **AnalyzeTableRelationships** - Encuentra relaciones entre tablas.
- **ProfileDataQuality** - Analiza la calidad de los datos y estadísticas.

## 🔒 Consideraciones de Seguridad

### Mejores Prácticas

- **Usa usuarios de base de datos dedicados** con los permisos mínimos necesarios.
- **Habilita el registro de auditoría** para entornos de producción.
- **Establece límites de filas adecuados** para evitar operaciones masivas accidentales.
- **Restringe tablas sensibles** utilizando la configuración `restrictedTables`.
- **Usa requisitos de cláusula WHERE** para operaciones de UPDATE/DELETE.
- **Realiza revisiones de seguridad periódicas** de las configuraciones y permisos.

### Seguridad de la Cadena de Conexión

```bash
# Usa variables de entorno para datos sensibles
export DB_PASSWORD="your_secure_password"
```

```json
{
  "connectionString": "Server=myserver;Database=mydb;User Id=myuser;Password=${DB_PASSWORD};"
}
```

## 🤝 Contribuciones

¡Agradecemos las contribuciones! Por favor, consulta nuestra [Guía de Contribución](CONTRIBUTING.md) para más detalles.

### Configuración de Desarrollo

```bash
# Clonar el repo
git clone https://github.com/yourusername/mcp-ms-sql-server.git
cd mcp-ms-sql-server

# Instalar dependencias
dotnet restore

# Ejecutar pruebas
dotnet test

# Compilar y probar
dotnet build -c Release
```

## 📚 Documentación

- [Guía de Configuración](docs/configuration-guide.md)
- [Configuración de Claude Desktop](docs/claude-desktop-setup.md)
- [Mejores Prácticas de Seguridad](docs/security-guide.md)
- [Referencia de la API](docs/api-reference.md)
- [Solución de Problemas](docs/troubleshooting.md)

## 🐛 Solución de Problemas

### Problemas Comunes

**Fallo de Conexión (Connection Failed)**
```bash
# Prueba tu cadena de conexión
dotnet run -- --test-connection --config your-project
```

**Permiso Denegado (Permission Denied)**
- Verifica los permisos del usuario de tu base de datos.
- Verifica la configuración de `allowedSchema`.
- Asegúrate de que el usuario tenga acceso al esquema especificado.

**Configuración no Encontrada (Configuration Not Found)**
- Verifica que el archivo de configuración exista en `Configurations/`.
- Verifica la variable de entorno `MCP_CONFIG_NAME`.
- Asegúrate de que la sintaxis JSON sea válida.

## 📄 Licencia

Este proyecto está bajo la Licencia MIT - consulta el archivo [LICENSE](LICENSE) para más detalles.

## 🌟 Agradecimientos

- [Model Context Protocol](https://modelcontextprotocol.io/) por Anthropic.
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) por Microsoft y Anthropic.
- La comunidad de código abierto por la inspiración y los comentarios.

## 📞 Soporte

- **Issues**: [GitHub Issues](https://github.com/yourusername/mcp-ms-sql-server/issues)
- **Discusiones**: [GitHub Discussions](https://github.com/yourusername/mcp-ms-sql-server/discussions)
- **Documentación**: [Wiki](https://github.com/yourusername/mcp-ms-sql-server/wiki)

---

**Hecho con ❤️ para la comunidad de desarrollo de MCP e IA**
