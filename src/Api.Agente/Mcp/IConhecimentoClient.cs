using Api.Agente.Mcp.Contracts;

namespace Api.Agente.Mcp;

internal interface IConhecimentoClient
{
    Task<string[]> DiscoverAsync(string correlationId, CancellationToken cancellationToken);
    Task<KnowledgeResult> SearchAsync(string query, string documentType, string layoutVersion, string correlationId, CancellationToken cancellationToken);
    Task<KnowledgeResult> ResolveAsync(Guid referenceId, string correlationId, CancellationToken cancellationToken);
}

internal sealed class KnowledgeClientException(string code, string? remoteCorrelationId = null) : Exception(code)
{
    public string Code { get; } = code;
    public string? RemoteCorrelationId { get; } = remoteCorrelationId;
}
