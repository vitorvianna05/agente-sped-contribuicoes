namespace Api.RAG.Domain.Knowledge.Values;

internal sealed record VersaoOficial
{
    public VersaoOficial(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
