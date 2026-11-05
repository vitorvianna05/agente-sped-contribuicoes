using System.Security.Claims;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Api.RAG.Infrastructure.Security;

namespace Api.RAG.Tests.Unit;

/// <summary>Regressões da revisão de cronologia e identidade da Phase 2.</summary>
public sealed class ReviewRegressionTests
{
    /// <summary>Uma operação pode ser cancelada antes de iniciar, mas não antes de existir.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CancellationWithoutStartCannotFinishBeforeCreation()
    {
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var operation = new Operacao
        {
            OperationId = Guid.NewGuid(),
            Type = "ingestion",
            PrincipalId = "fixture",
            CorrelationId = "fixture",
            State = "cancelled",
            CreatedAt = created,
            FinishedAt = created.AddTicks(-1),
            IdempotencyKeyDigest = ContentHash.Compute("key"u8),
            RequestDigest = ContentHash.Compute("request"u8)
        };
        Assert.Equal("INVALID_STATE", Assert.Throws<KnowledgeException>(operation.Validate).Code);
        (operation with { FinishedAt = created }).Validate();
    }

    /// <summary>Claims soltos numa identidade não autenticada não concedem identidade confiável.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ActorRequiresAuthenticatedSubject()
    {
        var forged = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "forged")]));
        Assert.Throws<KnowledgeException>(() => AuthenticatedActor.From(forged));
        Assert.Null(AuthenticatedActor.TryFrom(forged));
        var valid = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "fixture"), new Claim("organization_id", "org-fixture")], "synthetic"));
        Assert.Equal(new AuthenticatedActor("fixture", "org-fixture"), AuthenticatedActor.From(valid));
        var mixed = new ClaimsPrincipal(forged.Identities.Concat(valid.Identities));
        Assert.Equal(new AuthenticatedActor("fixture", "org-fixture"), AuthenticatedActor.From(mixed));
        Assert.Throws<KnowledgeException>(() => AuthenticatedActor.From(new ClaimsPrincipal(new ClaimsIdentity([], "synthetic"))));
    }
}
