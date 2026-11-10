using System.Net;
using System.Text;
using System.Text.Json;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Rag;
using Microsoft.Extensions.Options;

namespace Api.RAG.Tests.Unit;

/// <summary>SDK OpenAI real com transporte HTTP local controlado; nenhuma chamada faturável.</summary>
public sealed class OpenAiEmbeddingTests
{
    /// <summary>SDK envia modelo/dimensão externos e preserva usage informado.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task AdapterUsesConfiguredModelDimensionsAndReportedUsage()
    {
        using var handler = new EmbeddingHandler(HttpStatusCode.OK);
        var provider = new OpenAiEmbeddingService(Options.Create(new RagOptions { Embeddings = new EmbeddingOptions { ApiKey = "synthetic-test-only" } }), new Factory(handler));
        var result = await provider.GenerateAsync(new EmbeddingRequest(Guid.NewGuid(), "synthetic-correlation", "synthetic text", "OpenAI", "synthetic-model", 3), TestContext.Current.CancellationToken);
        Assert.Equal<float>([1, 0, 0], result.Vector);
        Assert.Equal(12L, result.EmbeddingTokens);
        Assert.Null(result.ProviderReportedCost);
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("synthetic-model", json.RootElement.GetProperty("model").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("dimensions").GetInt32());
    }

    /// <summary>Retries internos do SDK são desativados; decorator controla novas tentativas.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task AdapterDoesNotPerformHiddenRetries()
    {
        using var handler = new EmbeddingHandler(HttpStatusCode.TooManyRequests);
        var provider = new OpenAiEmbeddingService(Options.Create(new RagOptions { Embeddings = new EmbeddingOptions { ApiKey = "synthetic-test-only" } }), new Factory(handler));
        await Assert.ThrowsAsync<EmbeddingTransientException>(() => provider.GenerateAsync(new EmbeddingRequest(Guid.NewGuid(), "correlation", "synthetic", "OpenAI", "synthetic-model", 3), TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Calls);
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class EmbeddingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Body = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.OK
                ? """{"object":"list","data":[{"object":"embedding","index":0,"embedding":[1,0,0]}],"model":"synthetic-model","usage":{"prompt_tokens":12,"total_tokens":12}}"""
                : """{"error":{"message":"synthetic rate limit","type":"rate_limit_error","code":"rate_limit_exceeded"}}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
