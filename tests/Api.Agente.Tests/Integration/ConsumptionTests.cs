using Api.Agente.Analise;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Llm;
using Api.Agente.Tests.Fixtures;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Api.Agente.Tests.Integration;

/// <summary>Banco próprio descartável, papéis separados e retenção sintética explícita.</summary>
public sealed class ConsumptionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Migração idempotente, tentativas independentes, NULL e consultas isoladas por sujeito/organização.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task LedgerPersistsEveryAttemptAndEnforcesOwnerAndExpiry()
    {
        await using var database = new AgentDatabase();
        await database.StartAsync(Token);
        await AgentMigrationRunner.RunAsync(database.Options, Token);
        var clock = new Clock();
        var ledger = new PostgresAnalysisLedger(Options.Create(database.Options), clock);
        var identity = AnalysisFixtures.Identity;
        await ledger.StartAsync(identity, Token);
        var unknown = await ledger.BeginAttemptAsync(identity, "synthetic-model", "parecer-v1", Token);
        await ledger.FinishAttemptAsync(unknown, "timeout", null, null, Token);
        var known = await ledger.BeginAttemptAsync(identity, "synthetic-model", "parecer-v1", Token);
        await ledger.FinishAttemptAsync(known, "succeeded", new("not persisted", 11, 5, "succeeded"), null, Token);
        var priced = await ledger.BeginAttemptAsync(identity, "synthetic-model", "parecer-v1", Token);
        var usage = new LlmResponse("not persisted", 1_000_000, 500_000, "succeeded", 400_000);
        var price = new LlmPrice(0.4m, 0.1m, 1.6m, "USD", "synthetic-table", "v-test");
        await ledger.FinishAttemptAsync(priced, "succeeded", usage, price.Estimate(usage), Token);
        await ledger.CompleteAsync(identity, "partial", null, Token);
        var attempts = await ledger.ReadAsync(identity.ExecutionId, identity.PrincipalId, identity.OrganizationId, Token);
        Assert.Equal(3, attempts.Length);
        var cost = attempts.Single(attempt => attempt.AttemptId == priced);
        Assert.Equal((1.08m, "USD", "synthetic-table", "v-test", 400_000L),
            (cost.EstimatedCost!.Value, cost.EstimatedCurrency, cost.PriceReference, cost.PriceVersion, cost.CachedInputTokens!.Value));
        Assert.Null(cost.ProviderReportedCost);
        Assert.Null(attempts.Single(attempt => attempt.AttemptId == unknown).InputTokens);
        Assert.Equal(11L, attempts.Single(attempt => attempt.AttemptId == known).InputTokens);
        Assert.All(attempts.Where(attempt => attempt.AttemptId != priced), attempt => { Assert.Null(attempt.EstimatedCost); Assert.Null(attempt.ProviderReportedCost); Assert.Null(attempt.Currency); });
        foreach (var owner in new[] { ("other-user", identity.OrganizationId), (identity.PrincipalId, "other-org"), (identity.PrincipalId, (string?)null) })
        {
            Assert.Equal("EXECUTION_NOT_FOUND", (await Assert.ThrowsAsync<AnalysisException>(() => ledger.ReadAsync(identity.ExecutionId, owner.Item1, owner.Item2, Token))).Code);
        }
        Assert.Equal("EXECUTION_NOT_AVAILABLE", (await Assert.ThrowsAsync<AnalysisException>(() => ledger.BeginAttemptAsync(identity, "synthetic", "v1", Token))).Code);
        clock.Now = clock.Now.AddDays(2).AddSeconds(1);
        Assert.Empty(await ledger.ReadAsync(identity.ExecutionId, identity.PrincipalId, identity.OrganizationId, Token));
        clock.Now = clock.Now.AddDays(1);
        Assert.Equal("EXECUTION_NOT_FOUND", (await Assert.ThrowsAsync<AnalysisException>(() => ledger.ReadAsync(identity.ExecutionId, identity.PrincipalId, identity.OrganizationId, Token))).Code);
        var fresh = AnalysisFixtures.Identity;
        await ledger.StartAsync(fresh, Token);
        // O lote não remove retenções ainda vigentes; pai aguarda todos os filhos expirados.
        var firstPurge = await AgentRetentionRunner.RunAsync(database.Options, clock, Token);
        Assert.Equal((1, 0), firstPurge);
        Assert.Equal((1, 0), await AgentRetentionRunner.RunAsync(database.Options, clock, Token));
        var secondPurge = await AgentRetentionRunner.RunAsync(database.Options, clock, Token);
        Assert.Equal((1, 1), secondPurge);
        Assert.Equal((0, 0), await AgentRetentionRunner.RunAsync(database.Options, clock, Token));
        Assert.Empty(await ledger.ReadAsync(fresh.ExecutionId, fresh.PrincipalId, fresh.OrganizationId, Token));
        await using var connection = new NpgsqlConnection(database.Options.ConnectionString);
        await connection.OpenAsync(Token);
        await using var command = new NpgsqlCommand("CREATE TABLE agente.forbidden(id int)", connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Token));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    /// <summary>Sem aprovação/prazos não conecta e não presume defaults.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task MissingPolicyPreventsPersistence()
    {
        var ledger = new PostgresAnalysisLedger(Options.Create(new AgentDataOptions { TimeoutSeconds = 1 }), TimeProvider.System);
        Assert.Equal("AUDIT_NOT_CONFIGURED", (await Assert.ThrowsAsync<AnalysisException>(() => ledger.StartAsync(AnalysisFixtures.Identity, Token))).Code);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
