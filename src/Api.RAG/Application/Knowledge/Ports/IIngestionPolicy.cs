using System.Security.Claims;
using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Ports;

internal sealed record IngestionContext(PerfilProcessamento Profile, long MaxFileBytes, string PrincipalId,
    string? OrganizationId, string RetentionPolicyVersion);

internal interface IIngestionPolicy
{
    Task<IngestionContext> AuthorizeAsync(ClaimsPrincipal principal, Guid profileId, CancellationToken cancellationToken);
}
