using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace McpMsSqlServer.Services;

public class TransactionService
{
    private readonly ConfigService _configService;
    private readonly ILogger<TransactionService> _logger;
    
    public TransactionService(ConfigService configService, ILogger<TransactionService> logger)
    {
        _configService = configService;
        _logger = logger;
    }
    
    public async Task<T> ExecuteInTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> operation)
    {
        using var connection = new SqlConnection(_configService.CurrentConfig.ConnectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await operation(connection, transaction);
            await transaction.CommitAsync();
            _logger.LogDebug("Transaction committed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction failed, rolling back");
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    public async Task ExecuteInTransactionAsync(Func<SqlConnection, SqlTransaction, Task> operation)
    {
        await ExecuteInTransactionAsync(async (conn, trans) =>
        {
            await operation(conn, trans);
            return 0;
        });
    }
    
    public async Task LogOperationAsync(string operationType, string tableName, string description, string? details = null)
    {
        await Task.Run(() =>
        {
            _logger.LogInformation($"[AUDIT] {operationType} on {tableName}: {description}");
            if (!string.IsNullOrEmpty(details))
            {
                _logger.LogDebug($"[AUDIT DETAILS] {details}");
            }
        });
    }
}