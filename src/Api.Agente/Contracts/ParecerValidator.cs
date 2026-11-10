using System.Text.Json;
using Api.Agente.Analise;

namespace Api.Agente.Contracts;

internal static class ParecerValidator
{
    public static ParecerDraft Read(string json, ParecerContext context, int maxCharacters)
    {
        try
        {
            if (json.Length > maxCharacters) { throw new JsonException(); }
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != document.RootElement.EnumerateObject().Count())
            { throw new JsonException(); }
            var result = JsonSerializer.Deserialize<ParecerDraft>(json, AnalysisJson.Options) ?? throw new JsonException();
            var expected = context.Validation.Checks.Where(check => check.State == "failed").Select(check => check.RuleId).Order(StringComparer.Ordinal).ToArray();
            if (string.IsNullOrWhiteSpace(result.Explanation) || !result.HumanReviewRequired || result.ReferenceIds.Length == 0 ||
                result.ReferenceIds.Distinct().Count() != result.ReferenceIds.Length ||
                result.ReferenceIds.Any(id => id == Guid.Empty || !context.Evidences.Any(evidence => evidence.ReferenceId == id)) ||
                result.InterpretedInconsistencies.Any(string.IsNullOrWhiteSpace) ||
                !result.InterpretedInconsistencies.Order(StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal) ||
                context.Validation.ChecksNotPerformed.Except(result.Limitations, StringComparer.Ordinal).Any() ||
                result.Limitations.Any(string.IsNullOrWhiteSpace))
            { throw new JsonException(); }
            return result;
        }
        catch (JsonException) { throw new AnalysisException("PARECER_INVALID"); }
    }
}
