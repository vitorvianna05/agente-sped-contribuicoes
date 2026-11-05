using System.Security.Claims;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Security;

internal sealed class IngestionPolicy(IOptions<RagOptions> configured, IAuthorizationService authorization,
    IHostEnvironment environment) : IIngestionPolicy
{
    public async Task<IngestionContext> AuthorizeAsync(ClaimsPrincipal principal, Guid profileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!(await authorization.AuthorizeAsync(principal, null, "knowledge.ingest")).Succeeded)
        {
            throw new KnowledgeException("PERMISSION_DENIED");
        }
        var options = configured.Value;
        var synthetic = environment.IsEnvironment("Testing") && options.ConfigurationPurpose == "SyntheticTestsOnly_NotApprovedForProduction";
        RagOptionsValidator.RequirePersistence(options, synthetic);
        if (options.Retention.OriginalDays is not > 0 || options.Retention.ReferenceDays is not > 0)
        {
            throw new KnowledgeException("RETENTION_NOT_CONFIGURED");
        }
        var profile = options.Processing.Profiles.SingleOrDefault(profile => Guid.TryParse(profile.Id, out var id) && id == profileId)?.ToDomain()
            ?? throw new KnowledgeException("PROFILE_INCOMPATIBLE");
        profile.Validate();
        var actor = AuthenticatedActor.From(principal);
        return new IngestionContext(profile, options.Processing.MaxFileBytes!.Value, actor.PrincipalId,
            actor.OrganizationId, options.Retention.PolicyVersion!);
    }
}
