using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Agente.Mcp.Contracts;

internal static class KnowledgeContract
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    public static KnowledgeResult ReadResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        RejectDuplicates(document.RootElement);
        var result = JsonSerializer.Deserialize<KnowledgeResult>(json, JsonOptions) ?? throw new JsonException("Envelope ausente.");
        if (result.ContractVersion != "1.0" || string.IsNullOrWhiteSpace(result.CorrelationId) || result.CorrelationId.Length > 128 || result.CorrelationId.Any(char.IsControl) ||
            result.Status is not ("evidence_found" or "insufficient_evidence" or "invalid_context" or "reference_unavailable" or "technical_failure") ||
            result.Evidences.Any(item => item is null) ||
            result.Limitations.Length == 0 || result.Limitations.Any(item => item is null || string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Message)) ||
            (result.Status == "technical_failure") != (result.Error is not null) ||
            (result.Status == "evidence_found" && result.Evidences.Length == 0) ||
            (result.Status is "technical_failure" or "invalid_context" or "reference_unavailable" && result.Evidences.Length != 0) ||
            (result.Error is { } error && (string.IsNullOrWhiteSpace(error.Code) || string.IsNullOrWhiteSpace(error.Message))))
        {
            throw new JsonException("Envelope incompatível com o contrato 1.0.");
        }

        foreach (var evidence in result.Evidences)
        {
            if (evidence.Location is null || evidence.Applicability is null)
            {
                throw new JsonException("Localização e aplicabilidade são obrigatórias.");
            }

            var location = evidence.Location;
            if (new[] { evidence.ReferenceId, evidence.SourceId, evidence.DocumentVersionId, evidence.GenerationId, evidence.ChunkId }.Any(id => id == Guid.Empty) ||
                new[] { evidence.Issuer, evidence.OfficialIdentifier, evidence.OfficialVersion, evidence.Text,
                    evidence.Applicability.DocumentType, evidence.Applicability.LayoutVersion, evidence.Applicability.OfficialBasis }.Any(string.IsNullOrWhiteSpace) ||
                !evidence.OriginUri.IsAbsoluteUri || evidence.ReferenceState is not ("active" or "superseded" or "withdrawn") ||
                (evidence.Score is { } score && !double.IsFinite(score)) ||
                location.PhysicalPages.Any(page => page < 1) || location.PhysicalPages.Distinct().Count() != location.PhysicalPages.Length ||
                location.PrintedLabels.Any(string.IsNullOrWhiteSpace) ||
                location.Availability is not ("available" or "unavailable") ||
                (location.Availability == "unavailable" && (string.IsNullOrWhiteSpace(location.Reason) || location.PhysicalPages.Length != 0 || location.PrintedLabels.Length != 0 || location.Section is not null)) ||
                (location.Availability == "available" && (location.Reason is not null || (location.PhysicalPages.Length == 0 && location.PrintedLabels.Length == 0 && string.IsNullOrWhiteSpace(location.Section)))))
            {
                throw new JsonException("Evidência incompatível com o contrato 1.0.");
            }
        }

        return result;
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("Propriedade duplicada.");
                }

                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicates(item);
            }
        }
    }
}
