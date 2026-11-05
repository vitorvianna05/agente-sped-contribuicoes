using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Ingestao;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Api.RAG.Tests.Unit;

/// <summary>Regressões dos achados de configuração, ciclo de vida e classificação de falhas.</summary>
public sealed class IngestionReviewTests
{
    private static RagOptions Configuration() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-settings.json")).Build().Get<RagOptions>()!;

    /// <summary>Perfil incompleto ou sem UUID não é aceito no startup.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("extractor")]
    [InlineData("uuid")]
    [InlineData("attempts")]
    public void StartupRejectsIncompleteProcessingConfiguration(string field)
    {
        var options = Configuration();
        Assert.True(new RagOptionsValidator(true).Validate(null, options).Succeeded);
        switch (field)
        {
            case "extractor": options.Processing.Profiles[0].ExtractorVersion = null; break;
            case "uuid": options.Processing.Profiles[0].Id = "not-a-uuid"; break;
            case "attempts": options.Processing.MaxPreparationAttempts = null; break;
        }
        Assert.True(new RagOptionsValidator(true).Validate(null, options).Failed);
    }

    /// <summary>A identidade do perfil usa UUID, independentemente de caixa/formatação.</summary>
    [Fact, Trait("Category", "Unit")]
    public void StartupRejectsEquivalentProfileIdentifiers()
    {
        var options = Configuration();
        var first = options.Processing.Profiles[0];
        first.Id = "abcdefab-0000-0000-0000-000000000001";
        var second = Configuration().Processing.Profiles[0];
        second.Id = "{ABCDEFAB-0000-0000-0000-000000000001}";
        options.Processing.Profiles = [first, second];
        Assert.True(new RagOptionsValidator(true).Validate(null, options).Failed);
    }

    /// <summary>Domínio controla retomada, limite, estado terminal e ausência de publicação.</summary>
    [Fact, Trait("Category", "Unit")]
    public void DomainBoundsPreparationAttemptsAndRejectsExpiredOwners()
    {
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var owner = Guid.NewGuid();
        var generation = new GeracaoProcessamento(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ContentHash.Compute("synthetic"u8), "queued", at);
        var first = generation.ClaimPreparation(owner, at, TimeSpan.FromSeconds(60), 1);
        Assert.Equal(1, first.PreparationAttempts);
        Assert.Throws<KnowledgeException>(() => first.ClaimPreparation(Guid.NewGuid(), at.AddSeconds(1), TimeSpan.FromSeconds(60), 1));
        Assert.Throws<KnowledgeException>(() => first.CompletePreparation(owner, at.AddSeconds(60), null, "failure"));
        var exhausted = first.ClaimPreparation(Guid.NewGuid(), at.AddSeconds(61), TimeSpan.FromSeconds(60), 1);
        Assert.Equal("failed", exhausted.State);
        Assert.Equal("PROCESSING_ATTEMPTS_EXHAUSTED", exhausted.FailureCode);
        Assert.Equal(1, exhausted.PreparationAttempts);
        Assert.Null(exhausted.PreparationLeaseOwner);
        Assert.Throws<KnowledgeException>(() => exhausted.ClaimPreparation(Guid.NewGuid(), at.AddSeconds(122), TimeSpan.FromSeconds(60), 1));
        var prepared = first.CompletePreparation(owner, at.AddSeconds(1), ContentHash.Compute("manifest"u8), null);
        Assert.Equal("processing", prepared.State);
        Assert.Equal("ready", prepared.PreparationState);
        Assert.Null(prepared.CompletedAt);
        Assert.Throws<KnowledgeException>(() => prepared.CompletePreparation(owner, at.AddSeconds(2), null, "overwrite"));
    }

    /// <summary>Erros de I/O não são convertidos em falha documental pelo adaptador PDF.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task PdfAdapterPreservesTechnicalFailures()
    {
        var extractor = new PdfTextExtractor(Options.Create(Configuration()));
        using var content = new UnreadableStream();
        await Assert.ThrowsAsync<IOException>(() => extractor.ExtractAsync(content, TestContext.Current.CancellationToken));
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Synthetic storage failure");
        public override int Read(Span<byte> buffer) => throw new IOException("Synthetic storage failure");
        public override int ReadByte() => throw new IOException("Synthetic storage failure");
        public override long Length => throw new IOException("Synthetic storage failure");
    }
}
