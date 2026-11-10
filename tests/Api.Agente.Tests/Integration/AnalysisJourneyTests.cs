using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Agente.Analise;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Tests.Fixtures;
using Api.Agente.Validacao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Api.Agente.Tests.Integration;

/// <summary>REST real + SDK MCP em processo separado; provedor LLM sintético e banco próprio.</summary>
public sealed class AnalysisJourneyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Jornada completa sintética com persistência real, retry medido e GET protegido.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task SeparateMcpProcessAndOwnPostgresProduceTraceablePartialDraft()
    {
        await using var database = new AgentDatabase();
        await database.StartAsync(Token);
        await using var server = await KnowledgeServerProcess.StartAsync("journey", Token);
        var provider = new FakeParecerLlm("retry");
        await using var host = new AgentTestHost(server.Endpoint, services =>
        {
            services.RemoveAll<IParecerLlm>(); services.AddSingleton<IParecerLlm>(provider);
            services.Configure<AgentDataOptions>(options => Copy(database.Options, options));
            services.Configure<LayoutPolicyOptions>(options => options.Profiles = AnalysisFixtures.Layouts.Profiles);
        });
        host.UseKestrel(0);
        using var client = host.CreateClient();
        Authenticate(client, host, "synthetic-user", "synthetic-org");
        using var response = await Send(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        var root = json.RootElement;
        Assert.Equal("partial", root.GetProperty("status").GetString());
        Assert.Equal("partial", root.GetProperty("validation").GetProperty("state").GetString());
        Assert.True(root.GetProperty("humanReviewRequired").GetBoolean());
        Assert.Equal(root.GetProperty("evidences")[0].GetProperty("referenceId").GetGuid(), root.GetProperty("parecer").GetProperty("referenceIds")[0].GetGuid());
        Assert.Equal(2, provider.Calls);
        Assert.All(root.GetProperty("knowledgeCalls").EnumerateArray(), trace => Assert.Equal("server-correlation", trace.GetProperty("remoteCorrelationId").GetString()));
        await using (var connection = new NpgsqlConnection(database.Options.ConnectionString))
        {
            await connection.OpenAsync(Token);
            await using var command = new NpgsqlCommand("SELECT jsonb_array_length(knowledge_calls) FROM agente.executions WHERE execution_id=@id", connection);
            command.Parameters.AddWithValue("id", root.GetProperty("executionId").GetGuid());
            Assert.Equal(2, (int)(await command.ExecuteScalarAsync(Token))!);
        }
        var id = root.GetProperty("executionId").GetGuid();
        using var consumption = await client.GetAsync($"/api/v1/analises/{id}/consumo", Token);
        Assert.Equal(HttpStatusCode.OK, consumption.StatusCode);
        using var usage = JsonDocument.Parse(await consumption.Content.ReadAsStringAsync(Token));
        Assert.Equal(2, usage.RootElement.GetProperty("attemptCount").GetInt32());
        Assert.Equal(2, usage.RootElement.GetProperty("unknownCostAttempts").GetInt32());
        Assert.Equal(22, usage.RootElement.GetProperty("byModel")[0].GetProperty("knownInputTokens").GetInt32());
        Authenticate(client, host, "other-user", "synthetic-org");
        using var otherUser = await client.GetAsync($"/api/v1/analises/{id}/consumo", Token);
        Assert.Equal(HttpStatusCode.NotFound, otherUser.StatusCode);
        Authenticate(client, host, "synthetic-user", "other-org");
        using var otherOrg = await client.GetAsync($"/api/v1/analises/{id}/consumo", Token);
        Assert.Equal(HttpStatusCode.NotFound, otherOrg.StatusCode);
    }

    /// <summary>Falhas do transporte/contrato e do gerador não são conclusões fiscais.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("remote_failure", "valid", "failed", 0)]
    [InlineData("wrong_reference", "valid", "failed", 0)]
    [InlineData("journey", "invalid", "failed", 2)]
    [InlineData("journey", "refusal", "failed", 1)]
    public async Task DependencyFailureNeverReturnsAcceptedParecer(string scenario, string llmScenario, string state, int calls)
    {
        await using var server = await KnowledgeServerProcess.StartAsync(scenario, Token);
        var provider = new FakeParecerLlm(llmScenario); var ledger = new MemoryAnalysisLedger();
        await using var host = new AgentTestHost(server.Endpoint, services =>
        {
            services.RemoveAll<IParecerLlm>(); services.AddSingleton<IParecerLlm>(provider);
            services.RemoveAll<IAnalysisLedger>(); services.AddSingleton<IAnalysisLedger>(ledger);
            services.Configure<LayoutPolicyOptions>(options => options.Profiles = AnalysisFixtures.Layouts.Profiles);
        });
        host.UseKestrel(0);
        using var client = host.CreateClient(); Authenticate(client, host, "synthetic-user", "synthetic-org");
        using var response = await Send(client);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(state, json.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("parecer").ValueKind);
        Assert.Equal(calls, provider.Calls);
        if (scenario == "remote_failure")
        {
            var trace = Assert.Single(json.RootElement.GetProperty("knowledgeCalls").EnumerateArray());
            Assert.Equal("technical_failure", trace.GetProperty("status").GetString());
            Assert.Equal("server-correlation", trace.GetProperty("remoteCorrelationId").GetString());
            Assert.Single(ledger.KnowledgeCalls);
        }
    }

    /// <summary>Host pode executar leitura sem política de dados, porém bloqueia análise e custos.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task UnconfiguredDataPolicyReturnsExplicitUnavailable()
    {
        await using var host = new AgentTestHost(new Uri("http://127.0.0.1:1/mcp"));
        host.UseKestrel(0); using var client = host.CreateClient();
        using var anonymous = await Send(client);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Authenticate(client, host, "synthetic-user", "synthetic-org");
        using var response = await Send(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("AUDIT_NOT_CONFIGURED", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    /// <summary>Negativos do arquivo são classificados antes de qualquer chamada de conhecimento/IA.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("unknown", "unsupported")]
    [InlineData("invalid", "failed")]
    public async Task FileFailuresStopBeforeRemoteDependencies(string scenario, string state)
    {
        var ledger = new MemoryAnalysisLedger(); var provider = new FakeParecerLlm();
        await using var host = LocalHost(ledger, provider);
        host.UseKestrel(0); using var client = host.CreateClient();
        Authenticate(client, host, "synthetic-user", "synthetic-org");
        var file = scenario == "unknown" ? AnalysisFixtures.File.Replace("|006|", "|999|", StringComparison.Ordinal) :
            AnalysisFixtures.File.Replace("|9999|2|", "|9999|9|", StringComparison.Ordinal);
        using var content = new ByteArrayContent(Encoding.Latin1.GetBytes(file));
        content.Headers.ContentType = new("application/octet-stream");
        using var response = await client.PostAsync("/api/v1/analises", content, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(state, json.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("parecer").ValueKind);
        Assert.Equal(0, provider.Calls); Assert.Empty(ledger.Attempts);
    }

    /// <summary>Entrada bruta não aceita JSON/multipart, charset arbitrário ou limite excedido sem Content-Length.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("application/json", 415)]
    [InlineData("multipart/form-data", 415)]
    [InlineData("text/plain; charset=utf-8", 415)]
    [InlineData("large", 413)]
    [InlineData("chunked", 413)]
    public async Task UploadContractIsEnforced(string scenario, int status)
    {
        var ledger = new MemoryAnalysisLedger(); var provider = new FakeParecerLlm();
        await using var host = LocalHost(ledger, provider);
        host.UseKestrel(0); using var client = host.CreateClient();
        Authenticate(client, host, "synthetic-user", "synthetic-org");
        using HttpContent content = scenario == "chunked" ? new UnknownLengthContent() :
            new ByteArrayContent(scenario == "large" ? new byte[2048] : Encoding.Latin1.GetBytes(AnalysisFixtures.File));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(scenario is "large" or "chunked" ? "application/octet-stream" : scenario);
        using var response = await client.PostAsync("/api/v1/analises", content, Token);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(0, provider.Calls); Assert.Empty(ledger.Attempts);
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(new byte[2048], CancellationToken.None).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private static AgentTestHost LocalHost(MemoryAnalysisLedger ledger, FakeParecerLlm provider) => new(new Uri("http://127.0.0.1:1/mcp"), services =>
    {
        services.RemoveAll<IParecerLlm>(); services.AddSingleton<IParecerLlm>(provider);
        services.RemoveAll<IAnalysisLedger>(); services.AddSingleton<IAnalysisLedger>(ledger);
        services.Configure<LayoutPolicyOptions>(options => options.Profiles = AnalysisFixtures.Layouts.Profiles);
    });

    private static async Task<HttpResponseMessage> Send(HttpClient client)
    {
        using var content = new ByteArrayContent(Encoding.Latin1.GetBytes(AnalysisFixtures.File));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; charset=iso-8859-1");
        return await client.PostAsync("/api/v1/analises", content, Token);
    }
    private static void Authenticate(HttpClient client, AgentTestHost host, string subject, string organization)
    {
        var jwt = new JwtSecurityToken("https://identity.synthetic.invalid", "api-agente-synthetic",
            [new Claim("sub", subject), new Claim("scope", "analysis.execute"), new Claim("organization_id", organization)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(host.Key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
    }
    private static void Copy(AgentDataOptions source, AgentDataOptions target)
    {
        target.ConnectionString = source.ConnectionString; target.PolicyVersion = source.PolicyVersion;
        target.ApprovalReference = source.ApprovalReference; target.AuditDays = source.AuditDays;
        target.ConsumptionDays = source.ConsumptionDays; target.TimeoutSeconds = source.TimeoutSeconds;
    }
}
