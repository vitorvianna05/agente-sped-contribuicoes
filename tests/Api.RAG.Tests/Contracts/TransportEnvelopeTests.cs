using System.Text.Json;
using Api.RAG.Endpoints.Contracts;

namespace Api.RAG.Tests.Contracts;

/// <summary>Contrato comum da fundação T007/T009.</summary>
public sealed class TransportEnvelopeTests
{
    private const string Valid = """
        {"contractVersion":"1.0","correlationId":"system-correlation","status":"insufficient_evidence",
         "evidences":[],"limitations":[{"code":"NO_EVIDENCE","message":"Fixture técnica"}],"error":null}
        """;

    /// <summary>Campos desconhecidos, inclusive ator livre, são recusados.</summary>
    [Fact]
    [Trait("Category", "Contract")]
    public void IdentityCannotComeFromBody()
    {
        Assert.Throws<JsonException>(() => ContractValidator.ReadResult(Valid.Replace("\"error\":null", "\"error\":null,\"actor\":\"admin\"", StringComparison.Ordinal)));
    }

    /// <summary>Envelope válido preserva os nulos documentados.</summary>
    [Fact]
    [Trait("Category", "Contract")]
    public void ValidEnvelopePreservesUnknownValues()
    {
        var result = ContractValidator.ReadResult(Valid);
        Assert.Null(result.Error);
        Assert.Empty(result.Evidences);
    }

    /// <summary>Ausência de obrigatório e falha técnica disfarçada são recusadas.</summary>
    [Theory]
    [InlineData("\"error\":null", "\"error\":null,\"unexpected\":1")]
    [InlineData("insufficient_evidence", "technical_failure")]
    [InlineData("\"contractVersion\":\"1.0\",", "")]
    [InlineData("\"evidences\":[]", "\"evidences\":[null]")]
    [Trait("Category", "Contract")]
    public void InvalidEnvelopeIsRejected(string oldValue, string newValue)
    {
        Assert.Throws<JsonException>(() => ContractValidator.ReadResult(Valid.Replace(oldValue, newValue, StringComparison.Ordinal)));
    }
}
