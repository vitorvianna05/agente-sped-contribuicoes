namespace Api.RAG.Domain.Knowledge.Values;

internal sealed record Localizacao(string Availability, int[] PhysicalPages, string[] PrintedLabels, string? Section, string? Reason)
{
    public void Validate()
    {
        if (Availability is not ("available" or "unavailable") ||
            PhysicalPages.Any(page => page < 1) || PhysicalPages.Distinct().Count() != PhysicalPages.Length ||
            PrintedLabels.Any(string.IsNullOrWhiteSpace) ||
            (Availability == "available" && (Reason is not null || (PhysicalPages.Length == 0 && PrintedLabels.Length == 0 && string.IsNullOrWhiteSpace(Section)))) ||
            (Availability == "unavailable" && (string.IsNullOrWhiteSpace(Reason) || PhysicalPages.Length != 0 || PrintedLabels.Length != 0 || Section is not null)))
        {
            throw new KnowledgeException("INVALID_STATE");
        }
    }
}
