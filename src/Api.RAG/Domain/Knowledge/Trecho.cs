using System.Text;
using Api.RAG.Domain.Knowledge.Values;

namespace Api.RAG.Domain.Knowledge;

internal sealed record SourceSpan(int? PhysicalPage, int Start, int Length);

internal sealed record Trecho(Guid ChunkId, Guid GenerationId, int Ordinal, string Text, ContentHash TextHash,
    SourceSpan[] SourceSpans, Localizacao Location, Guid ReferenceId, DateTimeOffset CreatedAt)
{
    public void Validate()
    {
        Location.Validate();
        if (ChunkId == Guid.Empty || GenerationId == Guid.Empty || ReferenceId == Guid.Empty || Ordinal < 0 ||
            string.IsNullOrWhiteSpace(Text) || TextHash != ContentHash.Compute(Encoding.UTF8.GetBytes(Text)) ||
            SourceSpans.Length == 0 || SourceSpans.Any(span => span.PhysicalPage < 1 || span.Start < 0 || span.Length <= 0) ||
            !SourceSpans.Where(span => span.PhysicalPage.HasValue).Select(span => span.PhysicalPage!.Value).Distinct().SequenceEqual(Location.PhysicalPages) || CreatedAt.Offset != TimeSpan.Zero)
        {
            throw new KnowledgeException("INVALID_STATE");
        }
    }
}
