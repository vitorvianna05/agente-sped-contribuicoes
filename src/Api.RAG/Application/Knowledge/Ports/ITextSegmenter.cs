using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Ports;

internal interface ITextSegmenter
{
    IReadOnlyList<Trecho> Segment(IReadOnlyList<ExtractedPage> pages, PerfilProcessamento profile, Guid generationId, DateTimeOffset at);
}
