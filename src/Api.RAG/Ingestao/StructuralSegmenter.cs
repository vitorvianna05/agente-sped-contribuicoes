using System.Text.RegularExpressions;

namespace Api.RAG.Ingestao;

internal static partial class StructuralSegmenter
{
    internal const string RecordSections = "record-sections";

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreak();

    // Título de registro no início da linha ("Registro 0000: Abertura..."). Entradas de sumário têm
    // pontilhado até o número da página — na mesma linha ou em até duas linhas seguintes, quando o
    // título quebra — e não abrem seção.
    [GeneratedRegex(@"(?m)^[ \t]*Registro [0-9A-Z]{4}[ \t]*:(?![^\n]*\.{4})(?![^\n]*\n[^\n]*\.{4})(?![^\n]*\n[^\n]*\n[^\n]*\.{4})[^\n]*")]
    private static partial Regex RecordHeading();

    public static IEnumerable<(int Start, int Length)> Occurrences(string text, string strategy)
    {
        var start = 0;
        var separators = strategy switch
        {
            "structural" => ParagraphBreak().Matches(text).Select(match => (match.Index, match.Length)),
            RecordSections => RecordHeading().Matches(text).Select(match => (match.Index, 0)),
            _ => []
        };
        foreach (var (index, length) in separators)
        {
            if (index > start && text[start..index].Any(char.IsLetterOrDigit))
            {
                yield return (start, index - start);
            }
            start = index + length;
        }
        if (start < text.Length)
        {
            yield return (start, text.Length - start);
        }
    }

    // Linha de sumário: pontilhado seguido do número da página no fim da linha.
    [GeneratedRegex(@"(?m)\.{4,}[ \t]*\d+[ \t]*$")]
    private static partial Regex ContentsLine();

    /// <summary>Página de sumário (3+ linhas com pontilhado e número de página): não abre nem herda seção.</summary>
    public static bool IsContentsPage(string text) => ContentsLine().Count(text) >= 3;

    /// <summary>Título do registro que abre o trecho, quando o trecho começa por um.</summary>
    public static string? HeadingAt(string text, int start)
    {
        var match = RecordHeading().Match(text, start);
        return match.Success && text[start..match.Index].Trim().Length == 0 ? match.Value.Trim() : null;
    }
}
