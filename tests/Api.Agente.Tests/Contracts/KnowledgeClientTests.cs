using Api.Agente.Mcp;
using Api.Agente.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Agente.Tests.Contracts;

/// <summary>Consumidor real do SDK conversa com servidor sintético em outro processo.</summary>
public sealed class KnowledgeClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Descoberta, busca e resolução preservam evidências e conteúdo não confiável como dados.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("valid")]
    [InlineData("text_only")]
    public async Task ClientDiscoversSearchesAndResolvesSeparateServer(string scenario)
    {
        await using var server = await KnowledgeServerProcess.StartAsync(scenario, Token);
        Assert.NotEqual(Environment.ProcessId, server.ProcessId);
        await using var host = new AgentTestHost(server.Endpoint);
        using var scope = host.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IConhecimentoClient>();
        Assert.Equal(["BuscarConhecimento", "ResolverReferencia"], await client.DiscoverAsync("synthetic-correlation", Token));
        var found = await client.SearchAsync("synthetic", "synthetic-type", "synthetic-layout", "synthetic-correlation", Token);
        var evidence = Assert.Single(found.Evidences);
        Assert.Contains("untrusted data", evidence.Text, StringComparison.Ordinal);
        var resolved = await client.ResolveAsync(evidence.ReferenceId, "synthetic-correlation", Token);
        Assert.Equal(evidence.ReferenceId, Assert.Single(resolved.Evidences).ReferenceId);
    }

    /// <summary>Falhas e contratos inválidos nunca viram busca vazia ou evidência aceita.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("missing_tool", "KNOWLEDGE_SCHEMA_INCOMPATIBLE")]
    [InlineData("bad_schema", "KNOWLEDGE_SCHEMA_INCOMPATIBLE")]
    [InlineData("bad_json", "KNOWLEDGE_RESPONSE_INVALID")]
    [InlineData("divergent", "KNOWLEDGE_RESPONSE_INVALID")]
    [InlineData("wrong_layout", "KNOWLEDGE_RESPONSE_INVALID")]
    [InlineData("wrong_error_flag", "KNOWLEDGE_RESPONSE_INVALID")]
    [InlineData("remote_failure", "KNOWLEDGE_REMOTE_FAILURE")]
    [InlineData("oversize", "KNOWLEDGE_RESPONSE_TOO_LARGE")]
    [InlineData("auth", "KNOWLEDGE_ACCESS_DENIED")]
    [InlineData("timeout", "KNOWLEDGE_TIMEOUT")]
    [InlineData("unavailable", "KNOWLEDGE_UNAVAILABLE")]
    public async Task InvalidResponsesAreRejected(string scenario, string code)
    {
        await using var server = await KnowledgeServerProcess.StartAsync(scenario, Token);
        await using var host = new AgentTestHost(server.Endpoint);
        using var scope = host.Services.CreateScope();
        if (scenario == "timeout")
        {
            host.Services.GetRequiredService<IOptions<ConhecimentoMcpOptions>>().Value.TimeoutSeconds = 1;
        }
        var client = scope.ServiceProvider.GetRequiredService<IConhecimentoClient>();
        var error = await Assert.ThrowsAsync<KnowledgeClientException>(() => client.SearchAsync("synthetic", "synthetic-type", "synthetic-layout", "synthetic-correlation", Token));
        Assert.Equal(code, error.Code);
        if (scenario == "remote_failure") { Assert.Equal("server-correlation", error.RemoteCorrelationId); }
        Assert.DoesNotContain(server.Endpoint.AbsoluteUri, error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Credencial de serviço configurada é enviada, sem usar identidade no corpo.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task ServiceCredentialIsUsedOnlyInTransport()
    {
        await using var server = await KnowledgeServerProcess.StartAsync("auth", Token);
        await using var host = new AgentTestHost(server.Endpoint);
        using var scope = host.Services.CreateScope();
        host.Services.GetRequiredService<IOptions<ConhecimentoMcpOptions>>().Value.BearerToken = "synthetic-service-token";
        var client = scope.ServiceProvider.GetRequiredService<IConhecimentoClient>();
        Assert.Equal("evidence_found", (await client.SearchAsync("synthetic", "synthetic-type", "synthetic-layout", "synthetic-correlation", Token)).Status);
    }

    /// <summary>UUID diferente do solicitado é recusado; ausência de evidência é uma resposta normal.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("wrong_reference")]
    [InlineData("empty")]
    public async Task ReferenceIdentityAndEmptyResultsRemainExplicit(string scenario)
    {
        await using var server = await KnowledgeServerProcess.StartAsync(scenario, Token);
        await using var host = new AgentTestHost(server.Endpoint);
        using var scope = host.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IConhecimentoClient>();
        if (scenario == "wrong_reference")
        {
            Assert.Equal("KNOWLEDGE_RESPONSE_INVALID", (await Assert.ThrowsAsync<KnowledgeClientException>(() => client.ResolveAsync(Guid.NewGuid(), "correlation", Token))).Code);
        }
        else
        {
            var result = await client.SearchAsync("synthetic", "synthetic-type", "synthetic-layout", "correlation", Token);
            Assert.Equal("insufficient_evidence", result.Status);
            Assert.Empty(result.Evidences);
            Assert.Null(result.Error);
        }
    }

    /// <summary>Contexto ausente é recusado antes de rede; cancelamento do chamador é preservado.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task InputValidationAndCallerCancellationPrecedeNetwork()
    {
        await using var host = new AgentTestHost(new Uri("http://127.0.0.1:1/mcp"));
        using var scope = host.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IConhecimentoClient>();
        Assert.Equal("KNOWLEDGE_CONTEXT_REQUIRED", (await Assert.ThrowsAsync<KnowledgeClientException>(() => client.SearchAsync("query", "", "layout", "correlation", Token))).Code);
        Assert.Equal("KNOWLEDGE_REFERENCE_INVALID", (await Assert.ThrowsAsync<KnowledgeClientException>(() => client.ResolveAsync(Guid.Empty, "correlation", Token))).Code);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DiscoverAsync("correlation", cancelled.Token));
    }

    /// <summary>Cancelamento durante chamada pendente não retorna evidência nem desfecho normal.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task CancellationDuringTransportIsPreserved()
    {
        await using var server = await KnowledgeServerProcess.StartAsync("timeout", Token);
        await using var host = new AgentTestHost(server.Endpoint);
        using var scope = host.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IConhecimentoClient>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SearchAsync("synthetic", "synthetic-type", "synthetic-layout", "correlation", cancellation.Token));
    }
}
