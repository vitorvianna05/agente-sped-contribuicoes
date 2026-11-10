using System.Globalization;

namespace Api.Agente.Domain;

internal sealed record LayoutApplicability(string Code, DateOnly From, DateOnly Through, string DocumentType,
    string KnowledgeLayoutVersion, string SourceReference, string ApprovalReference)
{
    public bool IsDefined => !string.IsNullOrWhiteSpace(Code) && Code.Length == 3 && Code.All(char.IsAsciiDigit) &&
        From.Year >= 2020 && Through.Year <= 2026 && From <= Through &&
        !new[] { DocumentType, KnowledgeLayoutVersion, SourceReference, ApprovalReference }.Any(string.IsNullOrWhiteSpace);

    public bool Overlaps(LayoutApplicability other) => Code == other.Code && other.From <= Through && From <= other.Through;
}

internal static class SpedFileValidation
{
    private static readonly string[] Pending = ["RECORD_FIELD_SCHEMAS_NOT_VALIDATED", "BLOCK_HIERARCHY_NOT_VALIDATED", "FISCAL_RULES_NOT_EXECUTED"];
    public static ValidationResult Validate(SpedDocument document, LayoutApplicability[] configuredProfiles)
    {
        var checks = document.Issues.Select(issue => new DeterministicCheck(issue.Code, "failed", issue.LineNumber,
            "Guia EFD-Contribuicoes 1.35; pp.33,66,416; SHA256:60eace459169238808e3e745e952fcbbbd2af6b7f933b043e8c9692b68f4d08b")).ToList();
        if (checks.Count > 0) { return Result("failed", null, null, null); }
        if (document.Records.Length == 0 || document.Records[0].Code != "0000")
        {
            checks.Add(new("EFD.0000.REQUIRED", "failed", null, "Guia 1.35 p.66"));
            return Result("failed", null, null, null);
        }
        var header = document.Records[0];
        if (header.Fields.Length < 6 || !DateOnly.TryParseExact(header.GetField(6), "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !DateOnly.TryParseExact(header.GetField(7), "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var through) || from > through)
        {
            checks.Add(new("EFD.0000.PERIOD", "failed", header.LineNumber, "Guia 1.35 p.66, campos 06/07"));
            return Result("failed", null, null, null);
        }
        checks.Add(new("EFD.LEXICAL_AND_ENVELOPE", "passed", null, "Guia 1.35 pp.33,66,416"));
        checks.Add(new("EFD.0000.PERIOD", "passed", header.LineNumber, "Guia 1.35 p.66, campos 06/07"));
        if (from.Year < 2020 || through.Year > 2026) { return Result("unsupported", from, through, null); }
        var profiles = configuredProfiles.Where(profile => profile.Code == document.DeclaredLayoutCode &&
            from >= profile.From && through <= profile.Through).ToArray();
        if (profiles.Length != 1 || !profiles[0].IsDefined) { return Result("unsupported", from, through, null); }
        checks.Add(new("EFD.LAYOUT_CONTEXT", "passed", header.LineNumber, profiles[0].SourceReference + "; approval=" + profiles[0].ApprovalReference));
        return Result("partial", from, through, profiles[0]);

        ValidationResult Result(string state, DateOnly? start, DateOnly? end, LayoutApplicability? profile) =>
            new(state, document.DeclaredLayoutCode, start, end, profile?.DocumentType, profile?.KnowledgeLayoutVersion, checks.ToArray(),
                profile is null ? ["LAYOUT_MAPPING_NOT_APPROVED", .. Pending] : Pending);
    }
}
