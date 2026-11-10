using System.Security.Claims;
using Api.RAG.Endpoints.Contracts;
using Api.RAG.Rag;

namespace Api.RAG.Tools;

internal sealed class ResolverReferenciaTool(ConhecimentoService service)
{
    public Task<KnowledgeResult> ExecuteAsync(Guid referenceId, ClaimsPrincipal principal, string correlation, CancellationToken token) =>
        service.ResolveAsync(referenceId, principal, correlation, token);
}
