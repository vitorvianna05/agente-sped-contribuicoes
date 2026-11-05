using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Endpoints.Contracts;

internal static class RevisionPreconditions
{
    public static string ToETag(Guid revision) => $"\"{revision:N}\"";

    public static void Require(string? ifMatch, Guid currentRevision)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new KnowledgeException("PRECONDITION_REQUIRED");
        }

        if (!string.Equals(ifMatch, ToETag(currentRevision), StringComparison.Ordinal))
        {
            throw new KnowledgeException("REVISION_MISMATCH");
        }
    }
}
