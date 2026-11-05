using System.Text;
using System.Text.RegularExpressions;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;

namespace Api.RAG.Ingestao;

internal sealed partial class TokenWindowSegmenter : ITextSegmenter
{
    // Tokens lexicais experimentais, não tokens BPE nem consumo do provedor.
    [GeneratedRegex(@"\S+")]
    private static partial Regex Tokens();

    public IReadOnlyList<Trecho> Segment(IReadOnlyList<ExtractedPage> pages, PerfilProcessamento profile, Guid generationId, DateTimeOffset at)
    {
        profile.Validate();
        var result = new List<Trecho>();
        foreach (var page in pages)
        {
            foreach (var span in StructuralSegmenter.Occurrences(page.Text, profile.SegmentationStrategy))
            {
                var matches = Tokens().Matches(page.Text.Substring(span.Start, span.Length));
                for (var first = 0; first < matches.Count; first += profile.ChunkTokens - profile.OverlapTokens)
                {
                    var last = Math.Min(matches.Count, first + profile.ChunkTokens) - 1;
                    var start = span.Start + matches[first].Index;
                    var length = span.Start + matches[last].Index + matches[last].Length - start;
                    var text = page.Text.Substring(start, length);
                    var chunk = new Trecho(Guid.NewGuid(), generationId, result.Count, text,
                        ContentHash.Compute(Encoding.UTF8.GetBytes(text)), [new SourceSpan(page.PhysicalPage, start, length)],
                        new Localizacao("available", [page.PhysicalPage], page.PrintedLabel is null ? [] : [page.PrintedLabel], null, null),
                        Guid.NewGuid(), at);
                    chunk.Validate();
                    result.Add(chunk);
                    if (last == matches.Count - 1)
                    {
                        break;
                    }
                }
            }
        }
        if (result.Count == 0)
        {
            throw new KnowledgeException("PDF_TEXT_UNAVAILABLE");
        }
        return result;
    }
}
