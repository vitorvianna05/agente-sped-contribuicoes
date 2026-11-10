using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;

namespace Api.RAG.Tests.Unit;

/// <summary>Publicação automática e vetores independem de HTTP, banco e IA real.</summary>
public sealed class VectorDomainTests
{
    /// <summary>Publicação exige geração íntegra da própria versão; aceita perfil candidato e substitui a geração ativa.</summary>
    [Fact, Trait("Category", "Unit")]
    public void PublicationRequiresCompletedGenerationOfSameVersion()
    {
        var at = DateTimeOffset.UtcNow;
        var profile = DocumentInvariantsTests.Profile;
        var hash = ContentHash.Compute("synthetic"u8);
        var document = new VersaoDocumental(Guid.NewGuid(), Guid.NewGuid(), "opaque", hash, Guid.NewGuid(), "unpublished", null, null, null, null, Guid.NewGuid(), at);
        var generation = new GeracaoProcessamento(Guid.NewGuid(), document.DocumentVersionId, Guid.NewGuid(), profile.ProfileId, hash, "queued", at);
        Assert.Throws<KnowledgeException>(() => document.PublishGeneration(generation, "actor", at));
        generation = generation.Start().Finish("succeeded", at, hash, null);
        var published = document.PublishGeneration(generation, "actor", at);
        Assert.Equal("published", published.PublicationState);
        Assert.Equal(generation.GenerationId, published.ActiveGenerationId);
        Assert.Equal("actor", published.ApprovedBy);

        var replacement = new GeracaoProcessamento(Guid.NewGuid(), document.DocumentVersionId, Guid.NewGuid(), profile.ProfileId, hash, "queued", at)
            .Start().Finish("succeeded", at.AddMinutes(1), hash, null);
        var replaced = published.PublishGeneration(replacement, "other", at.AddMinutes(1));
        Assert.Equal(replacement.GenerationId, replaced.ActiveGenerationId);
        Assert.Equal("actor", replaced.ApprovedBy);

        var foreign = new GeracaoProcessamento(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), profile.ProfileId, hash, "queued", at)
            .Start().Finish("succeeded", at, hash, null);
        Assert.Throws<KnowledgeException>(() => document.PublishGeneration(foreign, "actor", at));
        var withdrawn = published.Withdraw(at);
        Assert.Equal("withdrawn", withdrawn.PublicationState);
        Assert.Null(withdrawn.ActiveGenerationId);
    }

    /// <summary>Vetor zero, dimensão incorreta e número não finito são recusados.</summary>
    [Fact, Trait("Category", "Unit")]
    public void InvalidVectorsAndExpiredLeasesAreRejected()
    {
        Assert.Throws<KnowledgeException>(() => VectorProcessing.ValidateVector([0, 0, 0], 3));
        Assert.Throws<KnowledgeException>(() => VectorProcessing.ValidateVector([1, 0], 3));
        Assert.Throws<KnowledgeException>(() => VectorProcessing.ValidateVector([float.NaN, 0, 1], 3));
        var at = DateTimeOffset.UtcNow;
        var owner = Guid.NewGuid();
        var state = new VectorProcessing("queued", 0, null, null).Claim(owner, at, TimeSpan.FromSeconds(2), 1);
        Assert.Throws<KnowledgeException>(() => state.RequireOwner(owner, at.AddSeconds(2)));
        Assert.Throws<KnowledgeException>(() => state.Claim(Guid.NewGuid(), at.AddSeconds(3), TimeSpan.FromSeconds(2), 1));
    }
}
