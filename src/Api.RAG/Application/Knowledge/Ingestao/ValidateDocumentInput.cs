using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Ingestao;

internal static class ValidateDocumentInput
{
    public static async Task<byte[]> ReadAsync(Stream stream, string? contentType, long maximum, CancellationToken cancellationToken)
    {
        if (!string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new KnowledgeException("UNSUPPORTED_FORMAT");
        }
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + read > maximum)
            {
                throw new KnowledgeException("FILE_TOO_LARGE");
            }
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var bytes = output.ToArray();
        if (bytes.Length < 8 || !bytes.AsSpan().StartsWith("%PDF-"u8))
        {
            throw new KnowledgeException("UNSUPPORTED_FORMAT");
        }
        return bytes;
    }
}
