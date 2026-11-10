using Microsoft.Extensions.Options;

namespace Api.Agente.Mcp;

internal sealed class BoundedKnowledgeHandler(IOptions<ConhecimentoMcpOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new KnowledgeClientException("KNOWLEDGE_ACCESS_DENIED");
            }
            var limit = options.Value.MaxResponseBytes;
            if (response.Content.Headers.ContentLength > limit)
            {
                throw new KnowledgeClientException("KNOWLEDGE_RESPONSE_TOO_LARGE");
            }
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(bytes, cancellationToken)) > 0)
            {
                if (buffer.Length + read > limit)
                {
                    throw new KnowledgeClientException("KNOWLEDGE_RESPONSE_TOO_LARGE");
                }
                await buffer.WriteAsync(bytes.AsMemory(0, read), cancellationToken);
            }
            var bounded = new ByteArrayContent(buffer.ToArray());
            foreach (var header in response.Content.Headers)
            {
                bounded.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            response.Content.Dispose();
            response.Content = bounded;
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
