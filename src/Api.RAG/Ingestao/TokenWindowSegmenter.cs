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
        var sections = profile.SegmentationStrategy == StructuralSegmenter.RecordSections;
        string? section = null; // Título do registro corrente, observado no documento e herdado nas páginas seguintes.
        foreach (var page in PageNormalizer.Normalize(pages, profile.NormalizationVersion))
        {
            // Sumário cita títulos de registros sem defini-los: não abre seção e encerra a herança.
            var contents = sections && StructuralSegmenter.IsContentsPage(page.Text);
            if (contents)
            {
                section = null;
            }
            foreach (var span in StructuralSegmenter.Occurrences(page.Text, contents ? "token-window" : profile.SegmentationStrategy))
            {
                if (sections && !contents && StructuralSegmenter.HeadingAt(page.Text, span.Start) is { } heading)
                {
                    section = heading;
                }
                var matches = Tokens().Matches(page.Text.Substring(span.Start, span.Length));
                for (var first = 0; first < matches.Count; first += profile.ChunkTokens - profile.OverlapTokens)
                {
                    var last = Math.Min(matches.Count, first + profile.ChunkTokens) - 1;
                    var start = span.Start + matches[first].Index;
                    var length = span.Start + matches[last].Index + matches[last].Length - start;
                    var text = page.Text.Substring(start, length);
                    var chunk = new Trecho(Guid.NewGuid(), generationId, result.Count, text,
                        ContentHash.Compute(Encoding.UTF8.GetBytes(text)), [new SourceSpan(page.PhysicalPage, start, length)],
                        new Localizacao("available", [page.PhysicalPage], page.PrintedLabel is null ? [] : [page.PrintedLabel], sections ? section : null, null),
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
