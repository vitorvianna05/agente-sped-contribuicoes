using System.Security.Cryptography;

namespace Api.RAG.Domain.Knowledge.Values;

internal sealed record ContentHash
{
    public ContentHash(string value)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
        {
            throw new KnowledgeException("INVALID_ARGUMENT");
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public static ContentHash Compute(ReadOnlySpan<byte> bytes) => new(Convert.ToHexStringLower(SHA256.HashData(bytes)));
}
