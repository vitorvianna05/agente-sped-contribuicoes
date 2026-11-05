using System.Security.Claims;

namespace Api.RAG.Application.Knowledge.Ports;

internal interface IKnowledgeAuditAccess
{
    Task RequireAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
