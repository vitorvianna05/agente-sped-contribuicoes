using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Api.RAG.Tests.Fixtures.Documents;

internal static class SyntheticPdfFactory
{
    public static byte[] Create(params string[] pages)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
        {
            var page = builder.AddPage(595, 842);
            if (text.Length != 0)
            {
                page.AddText(text, 12, new PdfPoint(40, 780), font);
            }
        }
        return builder.Build();
    }
}
