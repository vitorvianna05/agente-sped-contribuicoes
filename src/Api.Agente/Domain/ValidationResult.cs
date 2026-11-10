namespace Api.Agente.Domain;

internal sealed record DeterministicCheck(string RuleId, string State, int? LineNumber, string SourceReference);
internal sealed record ValidationResult(string State, string? DeclaredLayoutCode, DateOnly? PeriodFrom, DateOnly? PeriodThrough,
    string? DocumentType, string? KnowledgeLayoutVersion, DeterministicCheck[] Checks, string[] ChecksNotPerformed);
