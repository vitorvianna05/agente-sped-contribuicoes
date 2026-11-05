namespace Api.RAG.Domain.Knowledge;

internal sealed class KnowledgeException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
