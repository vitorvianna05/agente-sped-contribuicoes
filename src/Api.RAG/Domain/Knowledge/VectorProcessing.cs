namespace Api.RAG.Domain.Knowledge;

internal sealed record VectorProcessing(string State, int Attempts, Guid? Owner, DateTimeOffset? Until)
{
    public VectorProcessing Claim(Guid owner, DateTimeOffset at, TimeSpan lease, int maximum)
    {
        if (State is not ("queued" or "processing") || (State == "processing" && Until > at) || maximum <= 0 || owner == Guid.Empty || lease <= TimeSpan.Zero)
        {
            throw new KnowledgeException("INVALID_STATE");
        }

        if (Attempts >= maximum)
        {
            throw new KnowledgeException("PROCESSING_ATTEMPTS_EXHAUSTED");
        }

        return new("processing", Attempts + 1, owner, at + lease);
    }
    public void RequireOwner(Guid owner, DateTimeOffset at)
    {
        if (State != "processing" || Owner != owner || Until is null || Until <= at)
        {
            throw new KnowledgeException("REVISION_MISMATCH");
        }
    }
    public static void ValidateVector(float[] vector, int dimensions)
    {
        if (vector.Length != dimensions || vector.Any(value => !float.IsFinite(value)) || vector.All(value => value == 0))
        {
            throw new KnowledgeException("PROFILE_INCOMPATIBLE");
        }
    }
}
