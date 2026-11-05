using System.Text;
using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Api.RAG.Ingestao;
using Api.RAG.Tests.Contracts;
using Api.RAG.Tests.Fixtures;
using Api.RAG.Tests.Fixtures.Documents;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Api.RAG.Tests.Integration;

/// <summary>Fila durável, retomada e atomicidade dos trechos preparados.</summary>
public sealed class IngestionJourneyTests(FoundationDatabase database) : IClassFixture<FoundationDatabase>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private IngestionSubmission Submission(byte[] pdf)
    {
        var metadata = IngestionContractTests.Metadata();
        var profile = database.Options.Processing.Profiles[0].ToDomain();
        return new IngestionSubmission(metadata, profile, pdf, new Operacao
        {
            OperationId = Guid.NewGuid(),
            DocumentVersionId = Guid.NewGuid(),
            GenerationId = Guid.NewGuid(),
            Type = "ingestion",
            PrincipalId = "central-admin",
            CorrelationId = Guid.NewGuid().ToString("N"),
            State = "queued",
            CreatedAt = database.Clock.GetUtcNow(),
            IdempotencyKeyDigest = ContentHash.Compute(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
            RequestDigest = ContentHash.Compute(pdf)
        }, null, "synthetic-only-v1");
    }

    /// <summary>Reiniciar o adaptador não perde a fila; lease antigo não pode confirmar efeitos.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task LeaseReclaimsInterruptedWorkAndRejectsOldOwner()
    {
        var repository = new DocumentRepository(database.Source);
        var operation = await repository.SubmitAsync(Submission(SyntheticPdfFactory.Create("Recoverable synthetic evidence")), Cancellation);
        var now = database.Clock.GetUtcNow();
        var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => repository.ClaimAsync(now, TimeSpan.FromSeconds(60), 3, Cancellation)));
        var first = Assert.Single(claims.OfType<PreparationJob>());
        // Nova instância simula outro processo usando a mesma fila persistida.
        var restarted = new DocumentRepository(database.Source);
        Assert.Null(await restarted.ClaimAsync(now.AddSeconds(59), TimeSpan.FromSeconds(60), 3, Cancellation));
        var second = Assert.IsType<PreparationJob>(await restarted.ClaimAsync(now.AddSeconds(61), TimeSpan.FromSeconds(60), 3, Cancellation));
        Assert.Equal(first.GenerationId, second.GenerationId);
        Assert.NotEqual(first.LeaseOwner, second.LeaseOwner);
        var stale = await Assert.ThrowsAsync<KnowledgeException>(() => restarted.FinishPreparationAsync(first, [], "OLD_OWNER", now.AddSeconds(61), Cancellation));
        Assert.Equal("REVISION_MISMATCH", stale.Code);
        var processor = new ProcessarGeracao(restarted, new PdfTextExtractor(Options.Create(database.Options)), new TokenWindowSegmenter(),
            new ControlledTimeProvider(now.AddSeconds(62)));
        await processor.ExecuteAsync(second, Cancellation);
        var status = await restarted.GetOperationAsync(operation.OperationId, Cancellation);
        Assert.Equal("ready", status.PreparationState);
        Assert.Equal("EMBEDDINGS_PENDING", status.ResultCode);
        Assert.Null(status.FinishedAt);
        Assert.Null(await restarted.ClaimAsync(now.AddSeconds(122), TimeSpan.FromSeconds(60), 3, Cancellation));
        // Confirmação repetida também é rejeitada, preservando referências originais.
        await Assert.ThrowsAsync<KnowledgeException>(() => restarted.FinishPreparationAsync(second, [], "REPLAY", now.AddSeconds(63), Cancellation));
    }

    /// <summary>PDF parcialmente legível falha sem persistir trechos parciais ou publicar.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task PartialPdfFailsAtomicallyAndMetadataCannotBeOverwritten()
    {
        var repository = new DocumentRepository(database.Source);
        var submission = Submission(SyntheticPdfFactory.Create("Readable synthetic page", ""));
        var operation = await repository.SubmitAsync(submission, Cancellation);
        var job = Assert.IsType<PreparationJob>(await repository.ClaimAsync(database.Clock.GetUtcNow(), TimeSpan.FromSeconds(60), 3, Cancellation));
        await new ProcessarGeracao(repository, new PdfTextExtractor(Options.Create(database.Options)), new TokenWindowSegmenter(), database.Clock).ExecuteAsync(job, Cancellation);
        var status = await repository.GetOperationAsync(operation.OperationId, Cancellation);
        Assert.Equal("failed", status.State);
        Assert.Equal("PDF_TEXT_UNAVAILABLE", status.ResultCode);
        Assert.NotNull(status.FinishedAt);
        Assert.Equal("unpublished", (await repository.GetDocumentAsync(operation.DocumentVersionId!.Value, Cancellation)).PublicationState);
        await using var connection = await database.Source.OpenAsync(Cancellation);
        await using var count = new NpgsqlCommand("SELECT count(*) FROM rag.chunks WHERE generation_id=@id", connection);
        count.Parameters.AddWithValue("id", job.GenerationId);
        Assert.Equal(0L, await count.ExecuteScalarAsync(Cancellation));
        var altered = submission with
        {
            Metadata = submission.Metadata with { OriginUri = "https://other.synthetic.invalid/manual" },
            Operation = submission.Operation with
            {
                OperationId = Guid.NewGuid(),
                IdempotencyKeyDigest = ContentHash.Compute("another-key"u8),
                RequestDigest = ContentHash.Compute("another-request"u8)
            }
        };
        Assert.Equal("SOURCE_METADATA_CONFLICT", (await Assert.ThrowsAsync<KnowledgeException>(() => repository.SubmitAsync(altered, Cancellation))).Code);
        var changedProfile = submission with
        {
            Profile = submission.Profile with { ChunkTokens = 256 },
            Operation = altered.Operation with { IdempotencyKeyDigest = ContentHash.Compute("another-profile"u8) }
        };
        Assert.Equal("PROFILE_INCOMPATIBLE", (await Assert.ThrowsAsync<KnowledgeException>(() => repository.SubmitAsync(changedProfile, Cancellation))).Code);
    }

    /// <summary>Interrupções repetidas têm limite durável e nova chave preserva a operação canônica completa.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task ExhaustionIsDurableAndReingestionHasCanonicalChronology()
    {
        var repository = new DocumentRepository(database.Source);
        var submission = Submission(SyntheticPdfFactory.Create("Synthetic interruption test"));
        var original = await repository.SubmitAsync(submission, Cancellation);
        var now = database.Clock.GetUtcNow();
        Assert.NotNull(await repository.ClaimAsync(now, TimeSpan.FromSeconds(60), 2, Cancellation));
        Assert.NotNull(await repository.ClaimAsync(now.AddSeconds(61), TimeSpan.FromSeconds(60), 2, Cancellation));
        Assert.Null(await repository.ClaimAsync(now.AddSeconds(122), TimeSpan.FromSeconds(60), 2, Cancellation));
        var failed = await repository.GetOperationAsync(original.OperationId, Cancellation);
        Assert.Equal("failed", failed.State);
        Assert.Equal("PROCESSING_ATTEMPTS_EXHAUSTED", failed.ResultCode);
        Assert.NotNull(failed.FinishedAt);
        Assert.Null(await repository.ClaimAsync(now.AddSeconds(183), TimeSpan.FromSeconds(60), 2, Cancellation));
        var duplicate = submission with
        {
            Operation = submission.Operation with
            {
                OperationId = Guid.NewGuid(),
                DocumentVersionId = Guid.NewGuid(),
                GenerationId = Guid.NewGuid(),
                CreatedAt = now.AddSeconds(200),
                IdempotencyKeyDigest = ContentHash.Compute("new-key-after-failure"u8)
            }
        };
        var reused = await repository.SubmitAsync(duplicate, Cancellation);
        Assert.Equal(failed, reused);
        Assert.True(reused.CreatedAt <= reused.StartedAt);
        Assert.True(reused.StartedAt <= reused.FinishedAt);
        Assert.Equal(failed, await repository.GetOperationAsync(duplicate.Operation.OperationId, Cancellation));
        await using var connection = await database.Source.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand("SELECT attempt_number FROM rag.operations WHERE operation_id=@id", connection);
        command.Parameters.AddWithValue("id", original.OperationId);
        Assert.Equal(2, await command.ExecuteScalarAsync(Cancellation));
    }
}
