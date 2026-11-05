namespace Api.RAG.Application.Knowledge.Ports;

internal sealed record ExtractedPage(int PhysicalPage, string Text, string? PrintedLabel);

internal interface IPdfTextExtractor
{
    Task<IReadOnlyList<ExtractedPage>> ExtractAsync(Stream content, CancellationToken cancellationToken);
}
