using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Ingestao;

namespace Api.RAG.Tests.Unit;

/// <summary>Normalização de cabeçalhos repetidos e segmentação por registro (perfil de busca v2).</summary>
public sealed class SegmentationQualityTests
{
    private static readonly PerfilProcessamento RecordProfile = DocumentInvariantsTests.Profile with
    {
        NormalizationVersion = "repeated-lines-v1",
        SegmentationStrategy = "record-sections",
        ChunkTokens = 50,
        OverlapTokens = 5
    };

    private static ExtractedPage Page(int number, string body) =>
        new(number, $"Guia Sintetico - Versao 1.0: Atualizacao em 0{number}/01/2021\n{body}", null);

    /// <summary>Linha presente em quase todas as páginas sai do texto; conteúdo frequente, mas não universal, fica.</summary>
    [Fact, Trait("Category", "Unit")]
    public void RepeatedHeaderIsRemovedButFrequentContentStays()
    {
        var pages = new[]
        {
            Page(1, "Nº Campo Descrição\nprimeira pagina"), Page(2, "Nº Campo Descrição\nsegunda pagina"),
            Page(3, "terceira pagina"), Page(4, "quarta pagina"), Page(5, "quinta pagina")
        };
        var normalized = PageNormalizer.Normalize(pages, "repeated-lines-v1");
        Assert.All(normalized, page => Assert.DoesNotContain("Guia Sintetico", page.Text, StringComparison.Ordinal));
        Assert.Contains("Nº Campo Descrição", normalized[0].Text, StringComparison.Ordinal);
        Assert.Same(pages, PageNormalizer.Normalize(pages, "line-endings-v1"));
    }

    /// <summary>Cada registro abre seção; continuação herda o título; sumário com pontilhado não abre seção.</summary>
    [Fact, Trait("Category", "Unit")]
    public void RecordHeadingsDefineSectionsAcrossPages()
    {
        var pages = new[]
        {
            Page(1, "Registro 0000: Abertura do Arquivo Digital ............ 66\nRegistro 1900: Consolidação dos Documentos Emitidos\nno Período ........ 410\n" +
                "Registro F560: Consolidação das Operações\nda Pessoa Jurídica Submetida ao Regime\nde Tributação com Base no Lucro\nPresumido ........ 260\nRegistro 9999: Encerramento ........ 416"),
            Page(2, "texto anterior\nRegistro 0000: Abertura do Arquivo Digital\nCampo 06 DT_INI data inicial"),
            Page(3, "continuação do registro 0000 com DT_FIN"),
            Page(4, "Registro 9999: Encerramento do Arquivo Digital\nQTD_LIN quantidade total de linhas"),
            Page(5, "fim")
        };
        var chunks = new TokenWindowSegmenter().Segment(pages, RecordProfile, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.All(chunks, chunk => Assert.DoesNotContain("Guia Sintetico", chunk.Text, StringComparison.Ordinal));
        Assert.All(chunks.Where(chunk => chunk.Location.PhysicalPages[0] == 1), chunk => Assert.Null(chunk.Location.Section));
        Assert.Equal("Registro 0000: Abertura do Arquivo Digital",
            Assert.Single(chunks, chunk => chunk.Location.PhysicalPages[0] == 2 && chunk.Text.StartsWith("Registro 0000", StringComparison.Ordinal)).Location.Section);
        Assert.Equal("Registro 0000: Abertura do Arquivo Digital",
            Assert.Single(chunks, chunk => chunk.Location.PhysicalPages[0] == 3).Location.Section);
        Assert.Equal("Registro 9999: Encerramento do Arquivo Digital",
            Assert.Single(chunks, chunk => chunk.Location.PhysicalPages[0] == 4).Location.Section);
        Assert.Null(Assert.Single(chunks, chunk => chunk.Text == "texto anterior").Location.Section);
    }

    /// <summary>Embedding recebe o título da seção; o texto citado continua exato.</summary>
    [Fact, Trait("Category", "Unit")]
    public void EmbeddingInputPrefixesSectionWithoutChangingText()
    {
        Assert.Equal("Registro 0000: Abertura\ncorpo", new VectorChunk(Guid.NewGuid(), "corpo", null, "Registro 0000: Abertura").EmbeddingInput);
        Assert.Equal("corpo", new VectorChunk(Guid.NewGuid(), "corpo", null).EmbeddingInput);
    }
}
