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

namespace Api.Agente.Tests.Integration;

/// <summary>
/// AG013: Api.Agente (Kestrel) → SDK MCP → Api.RAG real em outro processo (JWT, PostgreSQL/PgVector,
/// busca híbrida, contrato 1.0) → gerador sintético. Embeddings e LLM são dublês; nenhum dado sai da máquina.
/// </summary>
public sealed class CrossApiJourneyTests(RealRagServer rag) : IClassFixture<RealRagServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Layout conhecido: evidências reais da RAG, referências resolvidas, parecer rastreável e consumo isolado.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task KnownLayoutUsesRealRagEvidenceAndKeepsUsageIsolated()
    {
        await using var database = new AgentDatabase();
        await database.StartAsync(Token);
        var provider = new FakeParecerLlm();
        await using var host = Host(provider, services => services.Configure<AgentDataOptions>(options =>
        {
            var source = database.Options;
            options.ConnectionString = source.ConnectionString; options.PolicyVersion = source.PolicyVersion;
            options.ApprovalReference = source.ApprovalReference; options.AuditDays = source.AuditDays;
            options.ConsumptionDays = source.ConsumptionDays; options.TimeoutSeconds = source.TimeoutSeconds;
        }));
        using var client = Client(host, "synthetic-user", "synthetic-org");
        var root = await AnalyzeAsync(client);

        Assert.Equal(("partial", "partial"), (root.GetProperty("status").GetString(), root.GetProperty("validation").GetProperty("state").GetString()));
        Assert.True(root.GetProperty("humanReviewRequired").GetBoolean());
        Assert.Contains("FISCAL_RULES_NOT_EXECUTED", root.GetProperty("checksNotPerformed").EnumerateArray().Select(item => item.GetString()));
        var evidences = root.GetProperty("evidences").EnumerateArray().ToArray();
        Assert.NotEmpty(evidences);
        Assert.All(evidences, evidence =>
        {
            Assert.Contains("Synthetic cross-API evidence", evidence.GetProperty("text").GetString(), StringComparison.Ordinal);
            Assert.Equal("synthetic-layout", evidence.GetProperty("applicability").GetProperty("layoutVersion").GetString());
            Assert.Equal("active", evidence.GetProperty("referenceState").GetString());
        });
        Assert.Equal(evidences.Select(evidence => evidence.GetProperty("referenceId").GetGuid()),
            root.GetProperty("parecer").GetProperty("referenceIds").EnumerateArray().Select(item => item.GetGuid()));
        var calls = root.GetProperty("knowledgeCalls").EnumerateArray().ToArray();
        Assert.Equal("BuscarConhecimento", calls[0].GetProperty("operation").GetString());
        Assert.Equal(evidences.Length, calls.Count(call => call.GetProperty("operation").GetString() == "ResolverReferencia"));
        Assert.All(calls, call =>
        {
            Assert.Equal("evidence_found", call.GetProperty("status").GetString());
            Assert.False(string.IsNullOrWhiteSpace(call.GetProperty("remoteCorrelationId").GetString()));
        });
        Assert.Equal(1, provider.Calls);

        var id = root.GetProperty("executionId").GetGuid();
        using (var consumption = await client.GetAsync($"/api/v1/analises/{id}/consumo", Token))
        {
            Assert.Equal(HttpStatusCode.OK, consumption.StatusCode);
            using var usage = JsonDocument.Parse(await consumption.Content.ReadAsStringAsync(Token));
            var attempt = Assert.Single(usage.RootElement.GetProperty("attempts").EnumerateArray());
            Assert.Equal(("succeeded", 11, 5), (attempt.GetProperty("state").GetString(), attempt.GetProperty("inputTokens").GetInt32(), attempt.GetProperty("outputTokens").GetInt32()));
        }
        using var other = Client(host, "other-user", "synthetic-org");
        using var denied = await other.GetAsync($"/api/v1/analises/{id}/consumo", Token);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    /// <summary>Negativos e falhas entre as APIs reais: nunca parecer aceito nem conformidade.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("no-documents", "valid", "partial", "NO_ELIGIBLE_EVIDENCE", 0, "insufficient_evidence")]
    [InlineData("rag-embedding-failure", "valid", "failed", "KNOWLEDGE_REMOTE_FAILURE", 0, "technical_failure")]
    [InlineData("rag-unauthorized", "valid", "failed", "KNOWLEDGE_ACCESS_DENIED", 0, null)]
    [InlineData("known", "refusal", "failed", "LLM_REFUSED", 1, "evidence_found")]
    [InlineData("known", "invalid", "failed", "PARECER_INVALID", 2, "evidence_found")]
    public async Task FailuresAcrossRealApisNeverProduceAcceptedParecer(string scenario, string llm, string status, string code, int llmCalls, string? search)
    {
        var provider = new FakeParecerLlm(llm); var ledger = new MemoryAnalysisLedger();
        var layouts = AnalysisFixtures.Layouts;
        if (scenario == "no-documents") { layouts.Profiles[0].KnowledgeLayoutVersion = "layout-without-documents"; }
        await using var host = Host(provider, services =>
        {
            services.RemoveAll<IAnalysisLedger>(); services.AddSingleton<IAnalysisLedger>(ledger);
            services.Configure<LayoutPolicyOptions>(options => options.Profiles = layouts.Profiles);
        }, scenario == "rag-unauthorized" ? "synthetic-invalid-token" : null);
        using var client = Client(host, "synthetic-user", "synthetic-org");
        if (scenario == "rag-embedding-failure") { await rag.SetEmbeddingFailureAsync(true, Token); }
        JsonElement root;
        try { root = await AnalyzeAsync(client); }
        finally { if (scenario == "rag-embedding-failure") { await rag.SetEmbeddingFailureAsync(false, Token); } }

        Assert.Equal((status, code), (root.GetProperty("status").GetString(), root.GetProperty("errorCode").GetString()));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parecer").ValueKind);
        Assert.True(root.GetProperty("humanReviewRequired").GetBoolean());
        Assert.Equal(llmCalls, provider.Calls);
        Assert.Equal(llmCalls, ledger.Attempts.Count);
        var first = root.GetProperty("knowledgeCalls").EnumerateArray().FirstOrDefault();
        Assert.Equal(search, first.ValueKind == JsonValueKind.Undefined ? null : first.GetProperty("status").GetString());
    }

    private AgentTestHost Host(FakeParecerLlm provider, Action<IServiceCollection> configure, string? bearer = null)
    {
        var host = new AgentTestHost(rag.Endpoint, services =>
        {
            services.RemoveAll<IParecerLlm>(); services.AddSingleton<IParecerLlm>(provider);
            services.Configure<LayoutPolicyOptions>(options => options.Profiles = AnalysisFixtures.Layouts.Profiles);
            configure(services);
        }, settings: new Dictionary<string, string?>
        {
            ["KnowledgeMcp:AllowUnauthenticatedLoopback"] = "false",
            ["KnowledgeMcp:BearerToken"] = bearer ?? rag.Token
        });
        host.UseKestrel(0);
        return host;
    }

    private static HttpClient Client(AgentTestHost host, string subject, string organization)
    {
        var client = host.CreateClient();
        var jwt = new JwtSecurityToken("https://identity.synthetic.invalid", "api-agente-synthetic",
            [new Claim("sub", subject), new Claim("scope", "analysis.execute"), new Claim("organization_id", organization)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(host.Key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        return client;
    }

    private static async Task<JsonElement> AnalyzeAsync(HttpClient client)
    {
        using var content = new ByteArrayContent(Encoding.Latin1.GetBytes(AnalysisFixtures.File));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; charset=iso-8859-1");
        using var response = await client.PostAsync("/api/v1/analises", content, Token);
        var text = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        using var json = JsonDocument.Parse(text);
        return json.RootElement.Clone();
    }
}
