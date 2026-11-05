using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Api.RAG.Endpoints.Contracts;
using Api.RAG.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Api.RAG.Tests.Unit;

/// <summary>Domínio, políticas e revisões independem de HTTP e PostgreSQL.</summary>
public sealed class DomainFoundationTests
{
    /// <summary>Operação não admite estados sem seus marcos nem datas locais.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void OperationStateRequiresItsLifecycleFields()
    {
        var queued = new Operacao
        {
            OperationId = Guid.NewGuid(),
            Type = "ingestion",
            PrincipalId = "synthetic-principal",
            CorrelationId = "synthetic-correlation",
            State = "queued",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IdempotencyKeyDigest = ContentHash.Compute("key"u8),
            RequestDigest = ContentHash.Compute("request"u8)
        };
        queued.Validate();
        foreach (var state in new[] { "processing", "succeeded", "failed", "cancelled", "unsupported" })
        {
            Assert.Equal("INVALID_STATE", Assert.Throws<KnowledgeException>(() => (queued with { State = state }).Validate()).Code);
        }

        Assert.Throws<KnowledgeException>(() => (queued with { CreatedAt = queued.CreatedAt.ToOffset(TimeSpan.FromHours(-3)) }).Validate());
        Assert.Throws<KnowledgeException>(() => (queued with { LeaseUntil = queued.CreatedAt.AddMinutes(1) }).Validate());
    }

    /// <summary>Versões permanecem opacas e o hash é calculado dos bytes.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void OfficialValuesPreserveTheirMeaning()
    {
        Assert.Equal("01/2026-A", new VersaoOficial("01/2026-A").Value);
        Assert.NotEqual(new VersaoOficial("01"), new VersaoOficial("1"));
        Assert.Throws<ArgumentException>(() => new IdentidadeOficial("", "manual"));
        Assert.Throws<KnowledgeException>(() => new ContentHash("not-a-sha256"));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ContentHash.Compute("abc"u8).Value);
    }

    /// <summary>If-Match exige exatamente a revisão forte atual.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void PreconditionsDistinguishMissingFromStaleRevision()
    {
        var revision = Guid.NewGuid();
        Assert.Equal("PRECONDITION_REQUIRED", Assert.Throws<KnowledgeException>(() => RevisionPreconditions.Require(null, revision)).Code);
        Assert.Equal("REVISION_MISMATCH", Assert.Throws<KnowledgeException>(() => RevisionPreconditions.Require("*", revision)).Code);
        Assert.Equal("REVISION_MISMATCH", Assert.Throws<KnowledgeException>(() => RevisionPreconditions.Require("W/" + RevisionPreconditions.ToETag(revision), revision)).Code);
        RevisionPreconditions.Require(RevisionPreconditions.ToETag(revision), revision);
    }

    /// <summary>Política sintética habilita apenas o ambiente de testes; perfil candidato não habilita busca.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SyntheticPolicyDoesNotApproveOperationalUse()
    {
        var options = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-settings.json"))
            .Build().Get<RagOptions>()!;
        options.ConnectionStrings.Rag = "Host=localhost;Database=synthetic;Username=app";
        Assert.True(new RagOptionsValidator(allowSynthetic: true).Validate(null, options).Succeeded);
        Assert.True(new RagOptionsValidator().Validate(null, options).Failed);
        Assert.Throws<KnowledgeException>(() => RagOptionsValidator.RequireExpurge(options));
        RagOptionsValidator.RequireExpurge(options, synthetic: true);
        RagOptionsValidator.RequirePersistence(options, synthetic: true);
        Assert.Equal("RETENTION_NOT_CONFIGURED", Assert.Throws<KnowledgeException>(() => RagOptionsValidator.RequirePersistence(options, synthetic: false)).Code);
        Assert.Equal("PROFILE_INCOMPATIBLE", Assert.Throws<KnowledgeException>(() => RagOptionsValidator.RequireOperationalProfile(options)).Code);
        options.Retention.QueryDays = null;
        Assert.Throws<KnowledgeException>(() => RagOptionsValidator.RequirePersistence(options, synthetic: true));
        options.Embeddings.SdkAutomaticRetriesEnabled = true;
        Assert.True(new RagOptionsValidator().Validate(null, options).Failed);
    }
}
