using System.Security.Claims;
using Api.RAG.Application.Knowledge.Ports;

namespace Api.RAG.Application.Knowledge.Consulta;

internal sealed class ConsultarIngestao(IDocumentStore documents, IKnowledgeAuditAccess access)
{
    public async Task<OperationSnapshot> OperationAsync(Guid id, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        await access.RequireAsync(principal, cancellationToken);
        return await documents.GetOperationAsync(id, cancellationToken);
    }

    public async Task<DocumentSnapshot> DocumentAsync(Guid id, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        await access.RequireAsync(principal, cancellationToken);
        return await documents.GetDocumentAsync(id, cancellationToken);
    }
}
