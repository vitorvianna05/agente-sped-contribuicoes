using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace Api.RAG.Ingestao;

internal sealed class PdfTextExtractor(IOptions<RagOptions> options) : IPdfTextExtractor
{
    public Task<IReadOnlyList<ExtractedPage>> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        try
        {
            using var pdf = PdfDocument.Open(content, new ParsingOptions { UseLenientParsing = false });
            if (pdf.NumberOfPages < 1 || pdf.NumberOfPages > options.Value.Processing.MaxPages)
            {
                throw new KnowledgeException("PDF_PAGE_LIMIT");
            }
            var pages = new List<ExtractedPage>();
            for (var number = 1; number <= pdf.NumberOfPages; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = pdf.GetPage(number);
                var text = ContentOrderTextExtractor.GetText(page).ReplaceLineEndings("\n");
                // Sem OCR ou inferência: uma página ilegível impede aceitar extração parcial.
                if (!text.Any(char.IsLetterOrDigit))
                {
                    throw new KnowledgeException("PDF_TEXT_UNAVAILABLE");
                }
                pages.Add(new ExtractedPage(number, text, null));
            }
            return Task.FromResult<IReadOnlyList<ExtractedPage>>(pages);
        }
        catch (Exception exception) when (exception is PdfDocumentFormatException or PdfDocumentEncryptedException)
        {
            throw new KnowledgeException("PDF_TEXT_UNAVAILABLE");
        }
    }
}
