using Api.Agente.Domain;

namespace Api.Agente.Validacao;

internal interface ISpedParser
{
    Task<SpedDocument> ReadAsync(Stream stream, CancellationToken cancellationToken);
}

internal sealed class SpedReadException(string code, int? lineNumber = null) : Exception(code)
{
    public string Code { get; } = code;
    public int? LineNumber { get; } = lineNumber;
}

internal sealed class SpedParserOptions
{
    public int MaxLineBytes { get; set; }
    public int MaxRecords { get; set; }
    public int MaxFieldsPerRecord { get; set; }
    public int MaxIssues { get; set; }
}
