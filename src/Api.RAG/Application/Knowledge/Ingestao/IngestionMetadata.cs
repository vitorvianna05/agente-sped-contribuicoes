using System.Text.Json;
using System.Text.Json.Serialization;
using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Ingestao;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record IngestionMetadata(string ContractVersion, string Issuer, string OfficialIdentifier,
    string Title, string Kind, string OriginUri, string OfficialVersion, Guid ProfileId, ApplicabilityInput[] Applicabilities)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        MaxDepth = 16
    };

    public void Validate()
    {
        if (ContractVersion != "1.0" || ProfileId == Guid.Empty || string.IsNullOrWhiteSpace(OfficialVersion) ||
            OfficialVersion.Length > 1000 || OriginUri is not { Length: > 0 and <= 4000 } || !Uri.TryCreate(OriginUri, UriKind.Absolute, out var uri) ||
            Applicabilities is not { Length: > 0 and <= 100 } || Applicabilities.Any(item => item is null ||
                new[] { item.DocumentType, item.LayoutVersion, item.OfficialBasis }.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1000)) ||
            Applicabilities.Select(item => (item.DocumentType, item.LayoutVersion)).Distinct().Count() != Applicabilities.Length)
        {
            throw new KnowledgeException("INVALID_METADATA");
        }
        new FonteOficial(Guid.NewGuid(), Issuer, OfficialIdentifier, Title, Kind, uri, DateTimeOffset.UtcNow).Validate();
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ApplicabilityInput(string DocumentType, string LayoutVersion, string OfficialBasis);
