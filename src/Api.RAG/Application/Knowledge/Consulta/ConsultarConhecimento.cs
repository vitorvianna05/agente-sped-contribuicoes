using System.Security.Claims;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Consulta;

internal sealed class ConsultarConhecimento(IKnowledgeStore store, IKnowledgeAccess access, IProfileEmbeddingProviderFactory embeddings, TimeProvider clock)
{
    public Task<EvidenceOutcome> SearchAsync(string query, string type, string layout, ClaimsPrincipal principal, string correlation, CancellationToken cancellationToken) =>
        ExecuteAsync(principal, correlation, async (operation, token) =>
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(layout))
            {
                return new EvidenceOutcome("invalid_context", [], "CONTEXT_REQUIRED");
            }

            if (string.IsNullOrWhiteSpace(query) || query.Length > access.QueryLimit || type.Length > access.QueryLimit || layout.Length > access.QueryLimit)
            {
                return new EvidenceOutcome("invalid_context", [], "INVALID_CONTEXT");
            }

            var profile = access.OperationalProfile();
            await store.RequireProfileAsync(profile, token);
            var response = await embeddings.Create(profile).GenerateAsync(new EmbeddingRequest(operation, correlation, query.ReplaceLineEndings("\n"),
                profile.EmbeddingProvider, profile.EmbeddingModel, profile.EmbeddingDimensions), token);
            var found = await store.SearchAsync(response.Vector, query.ReplaceLineEndings("\n"), profile, type, layout, token);
            return new EvidenceOutcome(found.Length == 0 ? "insufficient_evidence" : "evidence_found", found,
                found.Length == 0 ? "NO_ELIGIBLE_EVIDENCE" : "FISCAL_SUFFICIENCY_NOT_ASSESSED");
        }, cancellationToken);

    public Task<EvidenceOutcome> ResolveAsync(Guid reference, ClaimsPrincipal principal, string correlation, CancellationToken cancellationToken) =>
        ExecuteAsync(principal, correlation, async (_, token) =>
        {
            var (evidence, purged) = await store.ResolveAsync(reference, token);
            return evidence is null ? new EvidenceOutcome("reference_unavailable", [], purged ? "REFERENCE_PURGED" : "REFERENCE_NOT_FOUND")
                : new EvidenceOutcome("evidence_found", [evidence], "HISTORICAL_REFERENCE_NOT_CURRENT_AUTHORITY");
        }, cancellationToken);

    private async Task<EvidenceOutcome> ExecuteAsync(ClaimsPrincipal principal, string correlation,
        Func<Guid, CancellationToken, Task<EvidenceOutcome>> action, CancellationToken cancellationToken)
    {
        var actor = await access.RequireAsync(principal, "knowledge.read", cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(access.QueryTimeout);
        Guid? operation = null;
        EvidenceOutcome result;
        try
        {
            operation = await store.BeginQueryAsync(actor.Principal, actor.Organization, correlation, clock.GetUtcNow(), deadline.Token);
            result = await action(operation.Value, deadline.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (operation is { } cancelledId)
            {
                using var recording = new CancellationTokenSource(access.QueryTimeout);
                await store.AuditQueryAsync(cancelledId, actor.Principal, actor.Organization, correlation, "cancelled", clock.GetUtcNow(), recording.Token);
            }
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var code = exception is OperationCanceledException ? "OPERATION_TIMEOUT" : exception is KnowledgeException domain ? domain.Code : "DEPENDENCY_UNAVAILABLE";
            result = new EvidenceOutcome("technical_failure", [], "KNOWLEDGE_NOT_RETRIEVED", code);
        }
        if (operation is { } id)
        {
            using var completion = new CancellationTokenSource(access.QueryTimeout);
            try { await store.AuditQueryAsync(id, actor.Principal, actor.Organization, correlation, result.Status, clock.GetUtcNow(), completion.Token); }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            { return new EvidenceOutcome("technical_failure", [], "KNOWLEDGE_NOT_RETRIEVED", "DEPENDENCY_UNAVAILABLE"); }
        }
        return result;
    }
}
