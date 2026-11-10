using Api.Agente.Analise;
using Npgsql;

namespace Api.Agente.Infrastructure.Persistence;

internal static class AgentRetentionRunner
{
    // Execução administrativa explícita: o papel do servidor HTTP não recebe DELETE/DDL.
    public static async Task<(int Attempts, int Executions)> RunAsync(AgentDataOptions options, TimeProvider clock, CancellationToken token)
    {
        if (!options.HasApprovedPolicy || options.RetentionBatchSize <= 0) { throw new AnalysisException("AUDIT_NOT_CONFIGURED"); }
        var migration = AgentMigrationRunner.ValidateIdentities(options);
        await using var connection = new NpgsqlConnection(migration.ConnectionString);
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var attempts = new NpgsqlCommand("""
            DELETE FROM agente.consumption_attempts WHERE attempt_id IN
            (SELECT attempt_id FROM agente.consumption_attempts WHERE expires_at<=@at ORDER BY expires_at,attempt_id LIMIT @batch FOR UPDATE SKIP LOCKED)
            """, connection, transaction);
        attempts.Parameters.AddWithValue("at", clock.GetUtcNow()); attempts.Parameters.AddWithValue("batch", options.RetentionBatchSize);
        var removedAttempts = await attempts.ExecuteNonQueryAsync(token);
        await using var executions = new NpgsqlCommand("""
            DELETE FROM agente.executions WHERE execution_id IN
            (SELECT e.execution_id FROM agente.executions e WHERE e.expires_at<=@at
             AND NOT EXISTS(SELECT 1 FROM agente.consumption_attempts a WHERE a.execution_id=e.execution_id)
             ORDER BY e.expires_at,e.execution_id LIMIT @batch FOR UPDATE SKIP LOCKED)
            """, connection, transaction);
        executions.Parameters.AddWithValue("at", clock.GetUtcNow()); executions.Parameters.AddWithValue("batch", options.RetentionBatchSize);
        var removedExecutions = await executions.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        return (removedAttempts, removedExecutions);
    }
}
