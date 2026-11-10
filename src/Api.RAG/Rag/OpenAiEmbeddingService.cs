using System.ClientModel;
using System.ClientModel.Primitives;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace Api.RAG.Rag;

internal sealed class OpenAiEmbeddingService(IOptions<RagOptions> options, IHttpClientFactory clients) : IEmbeddingProvider
{
    public async Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(options.Value.Embeddings.ApiKey) ? options.Value.OpenAI.ApiKey : options.Value.Embeddings.ApiKey;
        if (request.Provider != "OpenAI" || string.IsNullOrWhiteSpace(key))
        {
            throw new KnowledgeException("DEPENDENCY_UNAVAILABLE");
        }
        using var http = clients.CreateClient("OpenAIEmbeddings");
        var client = new EmbeddingClient(request.Model, new ApiKeyCredential(key), new OpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(0),
            Transport = new HttpClientPipelineTransport(http)
        });
        try
        {
            var result = await client.GenerateEmbeddingsAsync([request.Text], new EmbeddingGenerationOptions { Dimensions = request.Dimensions }, cancellationToken);
            var embeddings = result.Value;
            if (embeddings.Count != 1)
            {
                throw new KnowledgeException("PROFILE_INCOMPATIBLE");
            }
            return new EmbeddingResponse(embeddings[0].ToFloats().ToArray(), InputTokens: embeddings.Usage?.InputTokenCount,
                EmbeddingTokens: embeddings.Usage?.InputTokenCount);
        }
        catch (ClientResultException exception) when (exception.Status is 408 or 429 or >= 500)
        {
            throw new EmbeddingTransientException();
        }
    }
}
