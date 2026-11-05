using System.Security.Claims;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Microsoft.AspNetCore.Authorization;

namespace Api.RAG.Infrastructure.Security;

internal sealed class KnowledgeAuditAccess(IAuthorizationService authorization) : IKnowledgeAuditAccess
{
    public async Task RequireAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!(await authorization.AuthorizeAsync(principal, null, "knowledge.audit")).Succeeded)
        {
            throw new KnowledgeException("PERMISSION_DENIED");
        }
    }
}
