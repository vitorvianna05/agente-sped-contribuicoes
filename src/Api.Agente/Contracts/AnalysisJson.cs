using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Agente.Contracts;

internal static class AnalysisJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

// JSON gerado pelo modelo não tem campos operacionais; citações são conferidas por ParecerValidator.
internal sealed record ParecerDraft(string Explanation, string[] InterpretedInconsistencies,
    Guid[] ReferenceIds, string[] Limitations, bool HumanReviewRequired);
