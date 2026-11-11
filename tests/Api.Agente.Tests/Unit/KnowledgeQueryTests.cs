using Api.Agente.Analise;
using Api.Agente.Domain;

namespace Api.Agente.Tests.Unit;

/// <summary>Consulta à RAG usa o vocabulário do guia e nunca conteúdo do arquivo.</summary>
public sealed class KnowledgeQueryTests
{
    /// <summary>Cada verificação vira o título do registro/seção correspondente; códigos internos não vão à busca.</summary>
    [Fact, Trait("Category", "Unit")]
    public void QueryUsesGuideVocabularyForExecutedChecks()
    {
        var validation = new ValidationResult("partial", "006", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), "EFD-Contribuicoes", "1.35",
            [new("EFD.LEXICAL_AND_ENVELOPE", "passed", null, "ref"), new("EFD.0000.PERIOD", "passed", 1, "ref"), new("EFD.LAYOUT_CONTEXT", "passed", 1, "ref"),
             new("UNKNOWN.RULE", "passed", null, "ref")], []);
        var query = KnowledgeQuery.Build(validation);
        Assert.Contains("Registro 0000: Abertura do Arquivo Digital", query, StringComparison.Ordinal);
        Assert.Contains("Registro 9999: Encerramento do Arquivo Digital", query, StringComparison.Ordinal);
        Assert.Contains("Características do Arquivo Digital", query, StringComparison.Ordinal);
        Assert.Contains("leiaute 006, período 2024-01-01 a 2024-01-31", query, StringComparison.Ordinal);
        Assert.DoesNotContain("EFD.0000.PERIOD", query, StringComparison.Ordinal);
        Assert.DoesNotContain("UNKNOWN.RULE", query, StringComparison.Ordinal);
    }
}
