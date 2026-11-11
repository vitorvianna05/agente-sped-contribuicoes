using Api.Agente.Domain;

namespace Api.Agente.Analise;

/// <summary>
/// Monta a consulta à base de conhecimento com o vocabulário do guia (títulos de registros e seções)
/// para cada verificação executada. Usa apenas texto do sistema, código de leiaute e período: nada do
/// conteúdo do arquivo. Códigos de regra internos (ex.: EFD.0000.PERIOD) não são enviados, pois não
/// existem no guia e prejudicam a busca textual.
/// </summary>
internal static class KnowledgeQuery
{
    private static readonly Dictionary<string, string> Topics = new(StringComparer.Ordinal)
    {
        ["EFD.LEXICAL_AND_ENVELOPE"] = "Características do Arquivo Digital: formato texto ISO 8859-1, delimitador de campos e fim de linha. " +
            "Registro 0000: Abertura do Arquivo Digital. Registro 9999: Encerramento do Arquivo Digital e quantidade total de linhas (QTD_LIN).",
        ["EFD.0000.PERIOD"] = "Registro 0000: Abertura do Arquivo Digital e Identificação da Pessoa Jurídica, data inicial (DT_INI) e data final (DT_FIN) da escrituração.",
        ["EFD.0000.REQUIRED"] = "Registro 0000: Abertura do Arquivo Digital e Identificação da Pessoa Jurídica.",
        ["EFD.LAYOUT_CONTEXT"] = "Registro 0000: código da versão do leiaute (COD_VER)."
    };

    public static string Build(ValidationResult validation)
    {
        var topics = validation.Checks.Select(check => Topics.GetValueOrDefault(check.RuleId)).OfType<string>().Distinct(StringComparer.Ordinal);
        return $"EFD-Contribuições, leiaute {validation.DeclaredLayoutCode}, período {validation.PeriodFrom:yyyy-MM-dd} a {validation.PeriodThrough:yyyy-MM-dd}. " +
            string.Join(' ', topics);
    }
}
