using System.Text.RegularExpressions;

namespace Api.RAG.Ingestao;

internal static partial class StructuralSegmenter
{
    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreak();

    public static IEnumerable<(int Start, int Length)> Occurrences(string text, string strategy)
    {
        var start = 0;
        if (strategy == "structural")
        {
            foreach (Match separator in ParagraphBreak().Matches(text))
            {
                if (separator.Index > start)
                {
                    yield return (start, separator.Index - start);
                }
                start = separator.Index + separator.Length;
            }
        }
        if (start < text.Length)
        {
            yield return (start, text.Length - start);
        }
    }
}
