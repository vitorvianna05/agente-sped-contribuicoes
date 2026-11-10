using Api.Agente.Domain;
using Microsoft.Extensions.Options;

namespace Api.Agente.Validacao;

internal sealed class LayoutPolicyOptions
{
    public LayoutProfile[] Profiles { get; set; } = [];
}

internal sealed class LayoutProfile
{
    public string Code { get; set; } = "";
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public string DocumentType { get; set; } = "";
    public string KnowledgeLayoutVersion { get; set; } = "";
    public string SourceReference { get; set; } = "";
    public string ApprovalReference { get; set; } = "";

    public LayoutApplicability ToDomain() => new(Code, From, Through, DocumentType, KnowledgeLayoutVersion, SourceReference, ApprovalReference);
}

internal sealed class SpedValidator(IOptions<LayoutPolicyOptions> options)
{
    public ValidationResult Validate(SpedDocument document) =>
        SpedFileValidation.Validate(document, options.Value.Profiles.Select(profile => profile.ToDomain()).ToArray());
}
