using System.Text;
using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Ingestao;
using Api.RAG.Tests.Fixtures.Documents;
using Microsoft.Extensions.Options;

namespace Api.RAG.Tests.Unit;

/// <summary>Modelos e segmentação sintéticos; nenhum conteúdo é regra fiscal oficial.</summary>
public sealed class DocumentInvariantsTests
{
    internal static PerfilProcessamento Profile => new(Guid.Parse("10000000-0000-0000-0000-000000000001"),
        "pdfpig-0.1.16-reading-order-v1", "line-endings-v1", "token-window", 3, 1, "whitespace-v1",
        "Fake", "synthetic-vector-v1", 3, "cosine", "line-endings-v1", 3, 0.5, null, "candidate");

    /// <summary>Janela preserva posição, repetição e página sem fabricar rótulo impresso.</summary>
    [Fact, Trait("Category", "Unit")]
    public void WindowsPreserveOccurrencesAndObservedSpans()
    {
        const string text = "alpha beta gamma delta epsilon";
        var pages = new[] { new ExtractedPage(1, text, null), new ExtractedPage(2, text, null) };
        var chunks = new TokenWindowSegmenter().Segment(pages, Profile, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(4, chunks.Count);
        Assert.Equal("alpha beta gamma", chunks[0].Text);
        Assert.Equal("gamma delta epsilon", chunks[1].Text);
        Assert.Equal(chunks[0].TextHash, chunks[2].TextHash);
        Assert.NotEqual(chunks[0].ReferenceId, chunks[2].ReferenceId);
        Assert.Equal<int>([2], chunks[2].Location.PhysicalPages);
        Assert.All(chunks, chunk =>
        {
            Assert.Empty(chunk.Location.PrintedLabels);
            var span = Assert.Single(chunk.SourceSpans);
            Assert.Equal(chunk.Text, text.Substring(span.Start, span.Length));
        });
    }

    /// <summary>Estrutura limita janelas às ocorrências de parágrafos observadas.</summary>
    [Fact, Trait("Category", "Unit")]
    public void StructuralWindowsDoNotJoinParagraphs()
    {
        var chunks = new TokenWindowSegmenter().Segment([new(1, "alpha beta\n\ngamma delta", null)],
            Profile with { SegmentationStrategy = "structural" }, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal<string>(["alpha beta", "gamma delta"], chunks.Select(chunk => chunk.Text));
    }

    /// <summary>Configuração inválida e mudança de conteúdo não viram geração publicada.</summary>
    [Fact, Trait("Category", "Unit")]
    public void DomainRejectsInvalidProfileLocationContentAndTransitions()
    {
        Assert.Throws<KnowledgeException>(() => (Profile with { OverlapTokens = 3 }).Validate());
        Assert.Throws<KnowledgeException>(() => (Profile with { AdoptionState = "adopted" }).Validate());
        Assert.Throws<KnowledgeException>(() => new Localizacao("unavailable", [1], [], null, "unknown").Validate());
        Assert.Throws<KnowledgeException>(() => new Localizacao("available", [], [], null, null).Validate());
        new Trecho(Guid.NewGuid(), Guid.NewGuid(), 0, "synthetic", ContentHash.Compute("synthetic"u8),
            [new SourceSpan(null, 0, 9)], new Localizacao("unavailable", [], [], null, "not observed"),
            Guid.NewGuid(), DateTimeOffset.UtcNow).Validate();
        var hash = ContentHash.Compute("original"u8);
        var version = new VersaoDocumental(Guid.NewGuid(), Guid.NewGuid(), "opaque-version", hash, Guid.NewGuid(),
            "unpublished", null, null, null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        version.AssertSameContent(hash);
        Assert.Equal("VERSION_CONTENT_CONFLICT", Assert.Throws<KnowledgeException>(() => version.AssertSameContent(ContentHash.Compute("other"u8))).Code);
        var generation = new GeracaoProcessamento(Guid.NewGuid(), version.DocumentVersionId, Guid.NewGuid(), Profile.ProfileId,
            hash, "queued", DateTimeOffset.UtcNow);
        Assert.Throws<KnowledgeException>(() => generation.Finish("succeeded", DateTimeOffset.UtcNow, hash, null));
        var completed = generation.Start().Finish("succeeded", DateTimeOffset.UtcNow, hash, null);
        Assert.Throws<KnowledgeException>(() => completed.Start());
        Assert.Throws<KnowledgeException>(() => completed.Finish("failed", DateTimeOffset.UtcNow, null, "error"));
    }

    /// <summary>Extração usa páginas físicas; recusa PDF parcial, vazio e limite excedido.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task PdfExtractionRejectsPartialUnreadableAndPageLimit()
    {
        var extractor = new PdfTextExtractor(Options.Create(new RagOptions { Processing = new ProcessingOptions { MaxPages = 2 } }));
        using var valid = new MemoryStream(SyntheticPdfFactory.Create("First synthetic page", "Second synthetic page"));
        var pages = await extractor.ExtractAsync(valid, TestContext.Current.CancellationToken);
        Assert.Equal<int>([1, 2], pages.Select(page => page.PhysicalPage));
        Assert.All(pages, page => Assert.Null(page.PrintedLabel));
        using var partial = new MemoryStream(SyntheticPdfFactory.Create("Extractable text", ""));
        Assert.Equal("PDF_TEXT_UNAVAILABLE", (await Assert.ThrowsAsync<KnowledgeException>(() => extractor.ExtractAsync(partial, TestContext.Current.CancellationToken))).Code);
        using var excessive = new MemoryStream(SyntheticPdfFactory.Create("One", "Two", "Three"));
        Assert.Equal("PDF_PAGE_LIMIT", (await Assert.ThrowsAsync<KnowledgeException>(() => extractor.ExtractAsync(excessive, TestContext.Current.CancellationToken))).Code);
        using var invalid = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 broken"));
        Assert.Equal("PDF_TEXT_UNAVAILABLE", (await Assert.ThrowsAsync<KnowledgeException>(() => extractor.ExtractAsync(invalid, TestContext.Current.CancellationToken))).Code);
    }

    /// <summary>Entrada limita bytes mesmo quando tamanho não é conhecido e verifica MIME/assinatura.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("application/pdf", 5, "FILE_TOO_LARGE")]
    [InlineData("text/plain", 100, "UNSUPPORTED_FORMAT")]
    public async Task InputChecksMimeAndActualLength(string mime, long maximum, string code)
    {
        using var content = new MemoryStream("%PDF-1.7 synthetic"u8.ToArray());
        Assert.Equal(code, (await Assert.ThrowsAsync<KnowledgeException>(() => ValidateDocumentInput.ReadAsync(content, mime, maximum, TestContext.Current.CancellationToken))).Code);
    }
}
