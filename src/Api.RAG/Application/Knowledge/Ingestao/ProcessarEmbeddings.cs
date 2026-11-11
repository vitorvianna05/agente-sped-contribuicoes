using Api.RAG.Application.Knowledge.Ports;

namespace Api.RAG.Application.Knowledge.Ingestao;

internal sealed class ProcessarEmbeddings(IKnowledgeStore store, IProfileEmbeddingProviderFactory embeddings, TimeProvider clock)
{
    public async Task ExecuteAsync(EmbeddingJob job, CancellationToken cancellationToken)
    {
        var provider = embeddings.Create(job.Profile);
        foreach (var chunk in job.Chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (chunk.Vector is not null)
            {
                continue;
            }

            var result = await provider.GenerateAsync(new EmbeddingRequest(job.OperationId, job.CorrelationId, chunk.EmbeddingInput,
                job.Profile.EmbeddingProvider, job.Profile.EmbeddingModel, job.Profile.EmbeddingDimensions), cancellationToken);
            await store.SaveVectorAsync(job, chunk.ChunkId, result.Vector, clock.GetUtcNow(), cancellationToken);
        }
        await store.CompleteEmbeddingAsync(job, null, clock.GetUtcNow(), cancellationToken);
    }
}
