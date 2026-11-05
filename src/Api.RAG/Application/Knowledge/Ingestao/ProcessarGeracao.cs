using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Ingestao;

internal sealed class ProcessarGeracao(IDocumentStore store, IPdfTextExtractor extractor, ITextSegmenter segmenter, TimeProvider clock)
{
    public async Task ExecuteAsync(PreparationJob job, CancellationToken cancellationToken)
    {
        IReadOnlyList<Trecho> chunks;
        try
        {
            using var original = new MemoryStream(job.Original, writable: false);
            var pages = await extractor.ExtractAsync(original, cancellationToken);
            chunks = segmenter.Segment(pages, job.Profile, job.GenerationId, clock.GetUtcNow());
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (KnowledgeException exception)
        {
            await store.FinishPreparationAsync(job, [], exception.Code, clock.GetUtcNow(), cancellationToken);
            return;
        }
        await store.FinishPreparationAsync(job, chunks, null, clock.GetUtcNow(), cancellationToken);
    }
}
