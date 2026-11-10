using System.Security.Claims;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Security;

internal sealed class KnowledgeAccess(IAuthorizationService authorization, IOptions<RagOptions> options) : IKnowledgeAccess
{
    public int QueryLimit => options.Value.Retrieval.MaxQueryCharacters!.Value;
    public TimeSpan QueryTimeout => TimeSpan.FromSeconds(options.Value.Retrieval.TimeoutSeconds!.Value);
    public async Task<(string Principal, string? Organization)> RequireAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!(await authorization.AuthorizeAsync(principal, null, permission)).Succeeded)
        {
            throw new KnowledgeException("PERMISSION_DENIED");
        }

        var actor = AuthenticatedActor.From(principal);
        return (actor.PrincipalId, actor.OrganizationId);
    }

    public PerfilProcessamento OperationalProfile() => RagOptionsValidator.OperationalProfile(options.Value);
}
