using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;

namespace Api.RAG.Application.Knowledge.Ingestao;

internal sealed class IngerirFonte(IDocumentStore store, IIngestionPolicy policy, TimeProvider clock)
{
    public async Task<OperationSnapshot> ExecuteAsync(IngestionMetadata metadata, Stream original, string? contentType,
        string key, ClaimsPrincipal principal, string correlation, CancellationToken cancellationToken)
    {
        var context = await policy.AuthorizeAsync(principal, metadata.ProfileId, cancellationToken);
        metadata.Validate();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200 || key.Any(char.IsControl))
        {
            throw new KnowledgeException("INVALID_ARGUMENT");
        }
        var bytes = await ValidateDocumentInput.ReadAsync(original, contentType, context.MaxFileBytes, cancellationToken);
        var digestPayload = JsonSerializer.Serialize(new { metadata, fileHash = ContentHash.Compute(bytes).Value }, IngestionMetadata.JsonOptions);
        var operation = new Operacao
        {
            OperationId = Guid.NewGuid(),
            DocumentVersionId = Guid.NewGuid(),
            GenerationId = Guid.NewGuid(),
            Type = "ingestion",
            PrincipalId = context.PrincipalId,
            CorrelationId = correlation,
            State = "queued",
            CreatedAt = clock.GetUtcNow(),
            ResultCode = "TEXT_PREPARATION_PENDING",
            IdempotencyKeyDigest = ContentHash.Compute(Encoding.UTF8.GetBytes(key)),
            RequestDigest = ContentHash.Compute(Encoding.UTF8.GetBytes(digestPayload))
        };
        return await store.SubmitAsync(new IngestionSubmission(metadata, context.Profile, bytes, operation,
            context.OrganizationId, context.RetentionPolicyVersion), cancellationToken);
    }
}
