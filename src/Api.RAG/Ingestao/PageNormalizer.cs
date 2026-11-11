using System.Text.RegularExpressions;
using Api.RAG.Application.Knowledge.Ports;

namespace Api.RAG.Ingestao;

/// <summary>
/// Normalização textual versionada pelo perfil. "repeated-lines-v1" remove linhas que se repetem em
/// quase todas as páginas (cabeçalho/rodapé), comparando-as com dígitos normalizados; elas dominavam
/// os embeddings e aproximavam trechos sem relação. Conteúdo frequente, mas não universal, é mantido.
/// </summary>
internal static partial class PageNormalizer
{
    internal const string RepeatedLines = "repeated-lines-v1";
    private const double RepeatedShare = 0.8;
    private const int MinimumPages = 3;

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    public static IReadOnlyList<ExtractedPage> Normalize(IReadOnlyList<ExtractedPage> pages, string version)
    {
        if (version != RepeatedLines || pages.Count < MinimumPages)
        {
            return pages;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            foreach (var key in page.Text.Split('\n').Select(Key).Where(key => key.Length > 0).Distinct(StringComparer.Ordinal))
            {
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }
        var repeated = counts.Where(pair => pair.Value >= pages.Count * RepeatedShare).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        if (repeated.Count == 0)
        {
            return pages;
        }

        return pages.Select(page =>
        {
            var kept = page.Text.Split('\n').Where(line => !repeated.Contains(Key(line)));
            var text = string.Join('\n', kept).Trim('\n');
            // Página só com cabeçalho não fica vazia: mantém o original para não fabricar ausência de texto.
            return text.Any(char.IsLetterOrDigit) ? page with { Text = text } : page;
        }).ToArray();
    }

    private static string Key(string line) => Digits().Replace(line.Trim(), "#");
}
