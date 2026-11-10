using System.Text.Json;
using Api.Agente.Domain;
using Api.Agente.Mcp.Contracts;

namespace Api.Agente.Contracts;

internal sealed record AnalysisResult(string ContractVersion, Guid ExecutionId, string CorrelationId, string Status,
    ValidationResult Validation, KnowledgeEvidence[] Evidences, ParecerDraft? Parecer, string[] Limitations,
    string[] ChecksNotPerformed, string? ErrorCode, bool HumanReviewRequired, KnowledgeTrace[] KnowledgeCalls)
{
    public void Validate()
    {
        if (ContractVersion != "1.0" || ExecutionId == Guid.Empty || string.IsNullOrWhiteSpace(CorrelationId) || !HumanReviewRequired ||
            !ChecksNotPerformed.SequenceEqual(Validation.ChecksNotPerformed, StringComparer.Ordinal) ||
            !AnalysisOutcome.IsCoherent(Status, Parecer is not null, Limitations.Length > 0, ChecksNotPerformed.Length > 0))
        { throw new JsonException("Invalid analysis envelope."); }
    }
}

internal sealed record KnowledgeTrace(string Operation, string RemoteCorrelationId, string Status, Guid[] ReferenceIds);
