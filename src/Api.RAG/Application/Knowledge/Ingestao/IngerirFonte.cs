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
        ClaimsPrincipal principal, string correlation, CancellationToken cancellationToken)
    {
        var context = await policy.AuthorizeAsync(principal, metadata.ProfileId, cancellationToken);
        metadata.Validate();
        var bytes = await ValidateDocumentInput.ReadAsync(original, contentType, context.MaxFileBytes, cancellationToken);
        var digestPayload = JsonSerializer.Serialize(new { metadata, fileHash = ContentHash.Compute(bytes).Value }, IngestionMetadata.JsonOptions);
        var operationId = Guid.NewGuid();
        var operation = new Operacao
        {
            OperationId = operationId,
            DocumentVersionId = Guid.NewGuid(),
            GenerationId = Guid.NewGuid(),
            Type = "ingestion",
            PrincipalId = context.PrincipalId,
            CorrelationId = correlation,
            State = "queued",
            CreatedAt = clock.GetUtcNow(),
            ResultCode = "TEXT_PREPARATION_PENDING",
            // Sem chave do cliente: cada envio é uma operação; reenvio do mesmo documento é resolvido pela versão/geração.
            IdempotencyKeyDigest = ContentHash.Compute(operationId.ToByteArray()),
            RequestDigest = ContentHash.Compute(Encoding.UTF8.GetBytes(digestPayload))
        };
        return await store.SubmitAsync(new IngestionSubmission(metadata, context.Profile, bytes, operation,
            context.OrganizationId, context.RetentionPolicyVersion), cancellationToken);
    }
}
