namespace Api.RAG.Domain.Knowledge;

internal sealed record PerfilProcessamento(Guid ProfileId, string ExtractorVersion, string NormalizationVersion,
    string SegmentationStrategy, int ChunkTokens, int OverlapTokens, string TokenizerVersion,
    string EmbeddingProvider, string EmbeddingModel, int EmbeddingDimensions, string EmbeddingMetric,
    string QueryNormalizationVersion, int? TopK, double? Threshold, Guid? EvaluationId, string AdoptionState)
{
    public void Validate()
    {
        if (ProfileId == Guid.Empty || ExtractorVersion != "pdfpig-0.1.16-reading-order-v1" ||
            NormalizationVersion != "line-endings-v1" || QueryNormalizationVersion != "line-endings-v1" ||
            TokenizerVersion != "whitespace-v1" || SegmentationStrategy is not ("structural" or "token-window") ||
            ChunkTokens <= 0 || OverlapTokens < 0 || OverlapTokens >= ChunkTokens ||
            EmbeddingDimensions <= 0 || EmbeddingMetric != "cosine" ||
            string.IsNullOrWhiteSpace(EmbeddingProvider) || string.IsNullOrWhiteSpace(EmbeddingModel) ||
            AdoptionState is not ("candidate" or "adopted") || TopK is <= 0 || Threshold is < -1 or > 1 ||
            (Threshold is { } threshold && !double.IsFinite(threshold)) ||
            (AdoptionState == "adopted" && (EvaluationId is null || TopK is null || Threshold is null)))
        {
            throw new KnowledgeException("PROFILE_INCOMPATIBLE");
        }
    }
}
