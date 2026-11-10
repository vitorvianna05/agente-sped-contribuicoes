namespace Api.Agente.Domain;

internal static class AnalysisOutcome
{
    public static bool IsCoherent(string status, bool hasParecer, bool hasLimitations, bool hasChecksNotPerformed) =>
        status is "completed" or "partial" or "failed" or "unsupported" &&
        (status == "completed" || hasLimitations) &&
        (status is not ("failed" or "unsupported") || !hasParecer) &&
        (status != "completed" || !hasChecksNotPerformed);
}
