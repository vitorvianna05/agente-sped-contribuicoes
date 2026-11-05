using System.Security.Claims;
using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Infrastructure.Security;

internal sealed record AuthenticatedActor(string PrincipalId, string? OrganizationId)
{
    public static AuthenticatedActor From(ClaimsPrincipal principal)
    {
        return TryFrom(principal) ?? throw new KnowledgeException("AUTHENTICATION_REQUIRED");
    }

    public static AuthenticatedActor? TryFrom(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.FirstOrDefault(identity => identity.IsAuthenticated &&
            !string.IsNullOrWhiteSpace(identity.FindFirst("sub")?.Value));
        return identity is null ? null : new AuthenticatedActor(identity.FindFirst("sub")!.Value, identity.FindFirst("organization_id")?.Value);
    }
}
