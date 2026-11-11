using System.Text.Json;
using Api.Agente.Analise;
using Api.Agente.Contracts;
using Api.Agente.Llm;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Api.Agente.Infrastructure.Persistence;

internal sealed class PostgresAnalysisLedger(IOptions<AgentDataOptions> configured, TimeProvider clock) : IAnalysisLedger
{
    private AgentDataOptions Options => configured.Value;
    private async Task<NpgsqlConnection> OpenAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(Options.ConnectionString) || !Options.HasApprovedPolicy)
        { throw new AnalysisException("AUDIT_NOT_CONFIGURED"); }
        var settings = new NpgsqlConnectionStringBuilder(Options.ConnectionString) { Timeout = Options.TimeoutSeconds, CommandTimeout = Options.TimeoutSeconds };
        var connection = new NpgsqlConnection(settings.ConnectionString);
        try { await connection.OpenAsync(token); return connection; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { await connection.DisposeAsync(); throw; }
        catch { await connection.DisposeAsync(); throw new AnalysisException("AUDIT_UNAVAILABLE"); }
    }
    public async Task StartAsync(ExecutionIdentity identity, CancellationToken token)
    {
        if (identity.ExecutionId == Guid.Empty || string.IsNullOrWhiteSpace(identity.PrincipalId) || string.IsNullOrWhiteSpace(identity.CorrelationId))
        { throw new AnalysisException("IDENTITY_REQUIRED"); }
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            INSERT INTO agente.executions(execution_id,principal_id,organization_id,correlation_id,state,error_code,started_at,finished_at,expires_at,policy_version,approval_reference)
            VALUES (@id,@principal,@organization,@correlation,'running',NULL,@at,NULL,@expires,@policy,@approval)
            """, connection);
        command.Parameters.AddWithValue("id", identity.ExecutionId);
        command.Parameters.AddWithValue("principal", identity.PrincipalId);
        Add(command, "organization", NpgsqlDbType.Text, identity.OrganizationId);
        command.Parameters.AddWithValue("correlation", identity.CorrelationId);
        var at = clock.GetUtcNow();
        command.Parameters.AddWithValue("at", at);
        command.Parameters.AddWithValue("expires", at.AddDays(Options.AuditDays!.Value));
        command.Parameters.AddWithValue("policy", Options.PolicyVersion!);
        command.Parameters.AddWithValue("approval", Options.ApprovalReference!);
        await command.ExecuteNonQueryAsync(token);
    }
    public async Task RecordKnowledgeAsync(ExecutionIdentity identity, KnowledgeTrace trace, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            UPDATE agente.executions SET knowledge_calls=knowledge_calls || @trace
            WHERE execution_id=@id AND principal_id=@principal AND organization_id IS NOT DISTINCT FROM @organization AND state='running'
            """, connection);
        command.Parameters.AddWithValue("id", identity.ExecutionId); command.Parameters.AddWithValue("principal", identity.PrincipalId);
        Add(command, "organization", NpgsqlDbType.Text, identity.OrganizationId);
        command.Parameters.AddWithValue("trace", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new[] { trace }, AnalysisJson.Options));
        if (await command.ExecuteNonQueryAsync(token) != 1) { throw new AnalysisException("EXECUTION_NOT_AVAILABLE"); }
    }
    public async Task<Guid> BeginAttemptAsync(ExecutionIdentity identity, string model, string promptVersion, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        var id = Guid.NewGuid(); var at = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("""
            INSERT INTO agente.consumption_attempts(attempt_id,execution_id,service,provider,model,prompt_version,started_at,state,expires_at,policy_version,approval_reference)
            SELECT @attempt,execution_id,'Api.Agente','OpenAI',@model,@prompt,@at,'pending',@expires,@policy,@approval
            FROM agente.executions WHERE execution_id=@id AND principal_id=@principal AND organization_id IS NOT DISTINCT FROM @organization AND state='running'
            """, connection);
        command.Parameters.AddWithValue("attempt", id);
        command.Parameters.AddWithValue("id", identity.ExecutionId);
        command.Parameters.AddWithValue("principal", identity.PrincipalId);
        Add(command, "organization", NpgsqlDbType.Text, identity.OrganizationId);
        command.Parameters.AddWithValue("model", model); command.Parameters.AddWithValue("prompt", promptVersion);
        command.Parameters.AddWithValue("at", at); command.Parameters.AddWithValue("expires", at.AddDays(Options.ConsumptionDays!.Value));
        command.Parameters.AddWithValue("policy", Options.PolicyVersion!); command.Parameters.AddWithValue("approval", Options.ApprovalReference!);
        if (await command.ExecuteNonQueryAsync(token) != 1) { throw new AnalysisException("EXECUTION_NOT_AVAILABLE"); }
        return id;
    }
    public async Task FinishAttemptAsync(Guid attemptId, string state, LlmResponse? usage, LlmCost? cost, CancellationToken token)
    {
        if (usage?.InputTokens is < 0 || usage?.OutputTokens is < 0 || usage?.CachedInputTokens is < 0 || cost?.Amount < 0)
        { throw new AnalysisException("USAGE_INVALID"); }
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            UPDATE agente.consumption_attempts SET state=@state,finished_at=@at,input_tokens=@input,output_tokens=@output,
            cached_input_tokens=@cached,estimated_cost=@cost,estimated_currency=@currency,price_reference=@reference,price_version=@version
            WHERE attempt_id=@id AND state='pending'
            """, connection);
        command.Parameters.AddWithValue("id", attemptId); command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("at", clock.GetUtcNow());
        Add(command, "input", NpgsqlDbType.Bigint, usage?.InputTokens); Add(command, "output", NpgsqlDbType.Bigint, usage?.OutputTokens);
        Add(command, "cached", NpgsqlDbType.Bigint, usage?.CachedInputTokens); Add(command, "cost", NpgsqlDbType.Numeric, cost?.Amount);
        Add(command, "currency", NpgsqlDbType.Text, cost?.Currency); Add(command, "reference", NpgsqlDbType.Text, cost?.Reference);
        Add(command, "version", NpgsqlDbType.Text, cost?.Version);
        if (await command.ExecuteNonQueryAsync(token) != 1) { throw new AnalysisException("ATTEMPT_NOT_AVAILABLE"); }
    }
    public async Task CompleteAsync(ExecutionIdentity identity, string state, string? code, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            UPDATE agente.executions SET state=@state,error_code=@code,finished_at=@at
            WHERE execution_id=@id AND principal_id=@principal AND organization_id IS NOT DISTINCT FROM @organization AND state='running'
            """, connection);
        command.Parameters.AddWithValue("id", identity.ExecutionId); command.Parameters.AddWithValue("principal", identity.PrincipalId);
        Add(command, "organization", NpgsqlDbType.Text, identity.OrganizationId); command.Parameters.AddWithValue("state", state);
        Add(command, "code", NpgsqlDbType.Text, code); command.Parameters.AddWithValue("at", clock.GetUtcNow());
        if (await command.ExecuteNonQueryAsync(token) != 1) { throw new AnalysisException("EXECUTION_NOT_AVAILABLE"); }
    }
    public async Task<ConsumptionAttempt[]> ReadAsync(Guid executionId, string principal, string? organization, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            SELECT a.attempt_id,a.state,a.model,a.prompt_version,a.started_at,a.finished_at,a.input_tokens,a.output_tokens,
            a.estimated_cost,a.estimated_currency,a.provider_reported_cost,a.currency,a.price_reference,a.price_version,a.cached_input_tokens
            FROM agente.executions e LEFT JOIN agente.consumption_attempts a ON a.execution_id=e.execution_id AND a.expires_at>@at
            WHERE e.execution_id=@id AND e.principal_id=@principal AND e.organization_id IS NOT DISTINCT FROM @organization
              AND e.expires_at>@at ORDER BY a.started_at,a.attempt_id
            """, connection);
        command.Parameters.AddWithValue("id", executionId); command.Parameters.AddWithValue("principal", principal);
        Add(command, "organization", NpgsqlDbType.Text, organization); command.Parameters.AddWithValue("at", clock.GetUtcNow());
        var attempts = new List<ConsumptionAttempt>();
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) { throw new AnalysisException("EXECUTION_NOT_FOUND"); }
        do
        {
            if (!reader.IsDBNull(0))
            {
                attempts.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5), reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    reader.IsDBNull(7) ? null : reader.GetInt64(7), reader.IsDBNull(8) ? null : reader.GetDecimal(8), reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetDecimal(10), reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetInt64(14)));
            }
        } while (await reader.ReadAsync(token));
        return attempts.ToArray();
    }
    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) => command.Parameters.AddWithValue(name, type, value ?? DBNull.Value);
}
