using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Knowledge.Embeddings;
using Api.RAG.Infrastructure.Knowledge.Observability;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Api.RAG.Tests.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Api.RAG.Tests.Integration;

/// <summary>Aceite integrado T011–T018 em PostgreSQL e HTTP/MCP isolados.</summary>
public sealed partial class FoundationTests(FoundationDatabase database) : IClassFixture<FoundationDatabase>
{
    /// <summary>Idempotência concorrente, isolamento de principal e rollback de auditoria.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentRegistrationIsAtomicAndScopedToPrincipal()
    {
        var operation = NewOperation();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RegisterAsync(
            operation with { OperationId = Guid.NewGuid() }, TestContext.Current.CancellationToken)));
        Assert.Single(results, result => result.Created);
        Assert.Single(results.Select(result => result.Operation.OperationId).Distinct());
        var conflict = await Assert.ThrowsAsync<KnowledgeException>(() => RegisterAsync(
            operation with { RequestDigest = ContentHash.Compute("other-payload"u8) }, TestContext.Current.CancellationToken));
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflict.Code);
        var other = await RegisterAsync(operation with { PrincipalId = "another-principal", OperationId = Guid.NewGuid() }, TestContext.Current.CancellationToken);
        Assert.True(other.Created);
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM rag.audit_entries WHERE object_id=@id", results[0].Operation.OperationId));

        var rolledBack = NewOperation();
        await using (var unit = await database.Source.BeginAsync(TestContext.Current.CancellationToken))
        {
            await unit.Operations.RegisterAsync(rolledBack, TestContext.Current.CancellationToken);
            await unit.Audit.WriteAsync(new AuditEntry(Guid.NewGuid(), rolledBack.PrincipalId, null, "ingestion",
                rolledBack.OperationId, "accepted", database.Clock.GetUtcNow(), rolledBack.CorrelationId), TestContext.Current.CancellationToken);
            // Não confirmar simula interrupção antes de ambos os efeitos serem persistidos.
        }

        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM rag.operations WHERE operation_id=@id", rolledBack.OperationId));
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM rag.audit_entries WHERE object_id=@id", rolledBack.OperationId));
    }

    /// <summary>Aplicação tem DML, mas não migrações nem DDL.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApplicationIdentityCannotMigrateOrCreateTables()
    {
        await using var connection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("CREATE TABLE rag.must_not_exist(id integer)", connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        var options = new RagOptions
        {
            ConnectionStrings = new ConnectionOptions
            {
                Rag = database.Options.ConnectionStrings.Rag,
                RagMigration = database.Options.ConnectionStrings.Rag
            }
        };
        var identity = await Assert.ThrowsAsync<KnowledgeException>(() => MigrationRunner.RunAsync(options, TestContext.Current.CancellationToken));
        Assert.Equal("MIGRATION_IDENTITY_REQUIRED", identity.Code);
        await Program.Main(["--migrate", $"--ConnectionStrings:Rag={database.Options.ConnectionStrings.Rag}",
            $"--ConnectionStrings:RagMigration={database.Options.ConnectionStrings.RagMigration}"]);
    }

    /// <summary>Cancelamento do chamador não impede auditoria com orçamento próprio.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CancelledTransportStillWritesAudit()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        using var client = host.CreateClient();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = Guid.NewGuid().ToString("N"),
            User = Principal("cancelled-principal", "knowledge.read"),
            RequestAborted = new CancellationToken(canceled: true)
        };
        context.Request.Path = "/mcp";
        var middleware = new TransportAuditMiddleware(_ => throw new OperationCanceledException(context.RequestAborted));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context, database.Source,
            database.Clock, Options.Create(database.Options), host.Services.GetRequiredService<IHostEnvironment>()));
        await using var connection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM rag.audit_entries WHERE correlation_id=@correlation AND result='cancelled'", connection);
        command.Parameters.AddWithValue("correlation", context.TraceIdentifier);
        Assert.Equal(1L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Falhas e retries têm linhas próprias; dados desconhecidos permanecem nulos.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConsumptionRecordsRetriesNullsPricesAndCancellation()
    {
        var operation = NewOperation();
        await RegisterAsync(operation, TestContext.Current.CancellationToken);
        var request = new EmbeddingRequest(operation.OperationId, operation.CorrelationId, "never-store-this-text", "Fake", "synthetic-vector-v1", 3);
        var repository = new ConsumptionRepository(database.Source);
        var fake = new ScriptedEmbeddingProvider();
        var provider = new MeasuredEmbeddingProvider(fake, repository, database.Clock, database.Options.Embeddings);
        await provider.GenerateAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(2, fake.Calls);
        Assert.Equal(2L, await ScalarAsync("SELECT count(*) FROM rag.consumption_attempts WHERE operation_id=@id", operation.OperationId));
        Assert.Equal(2L, await ScalarAsync("SELECT count(*) FROM rag.consumption_attempts WHERE operation_id=@id AND embedding_tokens IS NULL AND estimated_cost IS NULL AND provider_reported_cost IS NULL", operation.OperationId));
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM rag.consumption_attempts WHERE operation_id=@id AND status='failed'", operation.OperationId));

        var priced = new MeasuredEmbeddingProvider(new FixedEmbeddingProvider(), repository, database.Clock, database.Options.Embeddings,
            new ConsumptionPrice(2m, "TEST", "synthetic-price-table", "test-v1"));
        await priced.GenerateAsync(request, TestContext.Current.CancellationToken);
        await using var connection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
        await using var cost = new NpgsqlCommand("SELECT estimated_cost FROM rag.consumption_attempts WHERE operation_id=@id AND price_version='test-v1'", connection);
        cost.Parameters.AddWithValue("id", operation.OperationId);
        Assert.Equal(0.0002m, (decimal)(await cost.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);

        using var cancellation = new CancellationTokenSource();
        var blocking = new BlockingEmbeddingProvider();
        var cancelled = new MeasuredEmbeddingProvider(blocking, repository, database.Clock, database.Options.Embeddings);
        var execution = cancelled.GenerateAsync(request, cancellation.Token);
        await blocking.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM rag.consumption_attempts WHERE operation_id=@id AND status='cancelled'", operation.OperationId));
    }

    /// <summary>Timeout não vira resultado vazio e falha de política impede a chamada faturável.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TimeoutAndIncompleteConsumptionRemainExplicit()
    {
        var operation = NewOperation();
        await RegisterAsync(operation, TestContext.Current.CancellationToken);
        var request = new EmbeddingRequest(operation.OperationId, operation.CorrelationId, "synthetic", "Fake", "synthetic-vector-v1", 3);
        var options = new EmbeddingOptions
        {
            Provider = "Fake",
            Model = "synthetic-vector-v1",
            Dimensions = 3,
            MaxAttempts = 2,
            TimeoutSeconds = 1,
            RetryDelayMilliseconds = 0
        };
        var repository = new ConsumptionRepository(database.Source);
        var provider = new MeasuredEmbeddingProvider(new BlockingEmbeddingProvider(), repository, database.Clock, options);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GenerateAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ScalarAsync("SELECT count(*) FROM rag.consumption_attempts WHERE operation_id=@id AND status='timeout'", operation.OperationId));

        await repository.BeginAsync(request, database.Clock.GetUtcNow(), TestContext.Current.CancellationToken);
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM rag.consumption_attempts WHERE operation_id=@id AND status='pending' AND usage_availability='unknown' AND finished_at IS NULL AND estimated_cost IS NULL", operation.OperationId));

        var blockedOptions = new RagOptions { ConnectionStrings = database.Options.ConnectionStrings };
        await using var blocked = new RagDataSource(blockedOptions, synthetic: true);
        var fake = new ScriptedEmbeddingProvider();
        var measured = new MeasuredEmbeddingProvider(fake, new ConsumptionRepository(blocked), database.Clock, options);
        var denied = await Assert.ThrowsAsync<KnowledgeException>(() => measured.GenerateAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("RETENTION_NOT_CONFIGURED", denied.Code);
        Assert.Equal(0, fake.Calls);
    }

    /// <summary>JWT, permissões centrais, guardas HTTP e descoberta MCP autenticada real.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task FoundationEnforcesBearerAndCentralPermissionsWithAudit()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        host.UseKestrel(0);
        using var client = host.CreateClient();
        using var anonymous = await PostMcpAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Contains("resource_metadata", anonymous.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        var problem = await anonymous.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("AUTHENTICATION_REQUIRED", problem, StringComparison.Ordinal);
        Assert.Contains("contractVersion", problem, StringComparison.Ordinal);

        foreach (var invalid in new[]
        {
            Token(host, "reader", "knowledge.read", audience: "another-api"),
            Token(host, "reader", "knowledge.read", issuer: "https://wrong.synthetic.invalid"),
            Token(host, "reader", "knowledge.read", expired: true),
            Token(host, "reader", "knowledge.read", key: new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)))
        })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalid);
            using var response = await PostMcpAsync(client);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(host, "reader", "knowledge.ingest"));
        using var forbidden = await PostMcpAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var readerToken = Token(host, "reader", "knowledge.read");
        var endpoint = new Uri(client.BaseAddress!, "/mcp");
        var result = await McpConsumerProcess.ExecuteAsync(new McpConsumerRequest(endpoint, null, null, readerToken), TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Tools.Length);
        Assert.NotEqual(Environment.ProcessId, result.ProcessId);

        using var scope = host.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var organizationalAdmin = Principal("org-admin", "knowledge.publish knowledge.audit");
        Assert.False((await authorization.AuthorizeAsync(organizationalAdmin, null, "knowledge.publish")).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(Principal("central-admin", "knowledge.publish"), null, "knowledge.publish")).Succeeded);
        foreach (var permission in Api.RAG.Infrastructure.Security.KnowledgeAuthorization.Permissions.Where(permission => permission != "knowledge.read"))
        {
            Assert.False((await authorization.AuthorizeAsync(Principal("org-admin", permission), null, permission)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(Principal("central-admin", permission), null, permission)).Succeeded);
        }
        var auditReader = scope.ServiceProvider.GetRequiredService<KnowledgeAuditReader>();
        await Assert.ThrowsAsync<KnowledgeException>(() => auditReader.ReadAsync(organizationalAdmin, "reader", 20, TestContext.Current.CancellationToken));
        var entries = await auditReader.ReadAsync(Principal("central-admin", "knowledge.audit"), "reader", 20, TestContext.Current.CancellationToken);
        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.Equal("reader", entry.PrincipalId));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", readerToken);
        client.DefaultRequestHeaders.Add("Origin", "https://evil.synthetic.invalid");
        using var origin = await PostMcpAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, origin.StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Host = "evil.synthetic.invalid";
        using var badHost = await PostMcpAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, badHost.StatusCode);
    }

    private Operacao NewOperation() => new()
    {
        OperationId = Guid.NewGuid(),
        Type = "ingestion",
        PrincipalId = "foundation-principal",
        CorrelationId = Guid.NewGuid().ToString("N"),
        State = "queued",
        CreatedAt = database.Clock.GetUtcNow(),
        IdempotencyKeyDigest = ContentHash.Compute(Guid.NewGuid().ToByteArray()),
        RequestDigest = ContentHash.Compute(Encoding.UTF8.GetBytes("synthetic-payload"))
    };

    /// <summary>Registra a operação e sua auditoria na mesma transação.</summary>
    private async Task<(Operacao Operation, bool Created)> RegisterAsync(Operacao operation, CancellationToken token)
    {
        await using var unit = await database.Source.BeginAsync(token);
        var result = await unit.Operations.RegisterAsync(operation, token);
        if (result.Created)
        {
            await unit.Audit.WriteAsync(new AuditEntry(Guid.NewGuid(), operation.PrincipalId, null, operation.Type,
                operation.OperationId, "accepted", database.Clock.GetUtcNow(), operation.CorrelationId), token);
        }

        await unit.CommitAsync(token);
        return result;
    }

    /// <summary>A constraint impede cronologia inválida mesmo em escrita direta no banco.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DatabaseRejectsFinishBeforeCreationWithoutStart()
    {
        var operation = NewOperation();
        await RegisterAsync(operation, TestContext.Current.CancellationToken);
        await using var connection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE rag.operations SET state='cancelled',finished_at=created_at - interval '1 second'
            WHERE operation_id=@id AND started_at IS NULL
            """, connection);
        command.Parameters.AddWithValue("id", operation.OperationId);
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.CheckViolation, rejected.SqlState);
        Assert.Equal("operation_finish_not_before_creation", rejected.ConstraintName);
    }

    /// <summary>O decorator preserva custo real e moeda omitida ou diferente da estimativa.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("TEST-REPORTED")]
    [Trait("Category", "Integration")]
    public async Task ConsumptionPersistsIndependentCurrencies(string? reportedCurrency)
    {
        var operation = NewOperation();
        await RegisterAsync(operation, TestContext.Current.CancellationToken);
        var request = new EmbeddingRequest(operation.OperationId, operation.CorrelationId, "synthetic", "Fake", "synthetic-vector-v1", 3);
        var provider = new MeasuredEmbeddingProvider(new FixedEmbeddingProvider(reportedCurrency, 0.5m),
            new ConsumptionRepository(database.Source), database.Clock, database.Options.Embeddings,
            new ConsumptionPrice(2m, "TEST-ESTIMATE", "synthetic-table", "v1"));
        await provider.GenerateAsync(request, TestContext.Current.CancellationToken);
        await using var connection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT provider_reported_cost,currency,estimated_cost,estimated_currency,status
            FROM rag.consumption_attempts WHERE operation_id=@id
            """, connection);
        command.Parameters.AddWithValue("id", operation.OperationId);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0.5m, reader.GetDecimal(0));
        Assert.Equal(reportedCurrency, reader.IsDBNull(1) ? null : reader.GetString(1));
        Assert.Equal(0.0002m, reader.GetDecimal(2));
        Assert.Equal("TEST-ESTIMATE", reader.GetString(3));
        Assert.Equal("succeeded", reader.GetString(4));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<HttpResponseMessage> PostMcpAsync(HttpClient client)
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        return await client.PostAsync("/mcp", content, TestContext.Current.CancellationToken);
    }

    private async Task<long> ScalarAsync(string sql, Guid operationId)
    {
        await using var connection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", operationId);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static ClaimsPrincipal Principal(string subject, string scopes) =>
        new(new ClaimsIdentity([new Claim("sub", subject), new Claim("scope", scopes), new Claim("role", "OrganizationAdmin")], "synthetic"));

    private static string Token(RagTestHost host, string subject, string scopes, string audience = "api-rag-synthetic",
        string issuer = "https://identity.synthetic.invalid", bool expired = false, SecurityKey? key = null)
    {
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim("sub", subject), new Claim("scope", scopes)], DateTime.UtcNow.AddMinutes(-10),
            expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(key ?? host.SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ScriptedEmbeddingProvider : IEmbeddingProvider
    {
        public int Calls { get; private set; }
        public Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Calls == 1 ? Task.FromException<EmbeddingResponse>(new EmbeddingTransientException()) : Task.FromResult(new EmbeddingResponse([1, 2, 3]));
        }
    }

    private sealed class FixedEmbeddingProvider(string? currency = null, decimal? reportedCost = null) : IEmbeddingProvider
    {
        public Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingResponse([1, 2, 3], EmbeddingTokens: 100, ProviderReportedCost: reportedCost, Currency: currency));
    }

    private sealed class BlockingEmbeddingProvider : IEmbeddingProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }
}
