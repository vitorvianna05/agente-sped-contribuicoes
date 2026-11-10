using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Knowledge.Embeddings;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Npgsql;

namespace Api.RAG.Tests.Integration;

public sealed partial class FoundationTests
{
    /// <summary>Resposta vetorial inválida não apaga métricas conhecidas nem dispara nova chamada.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidEmbeddingPreservesReportedUsage(bool nonFinite)
    {
        var token = TestContext.Current.CancellationToken;
        var operation = NewOperation();
        await RegisterAsync(operation, token);
        var inner = new InvalidVectorProvider(nonFinite);
        var provider = new MeasuredEmbeddingProvider(inner, new ConsumptionRepository(database.Source), database.Clock,
            database.Options.Embeddings, new ConsumptionPrice(2m, "TEST", "synthetic-table", "v1"));
        var request = new EmbeddingRequest(operation.OperationId, operation.CorrelationId, "synthetic", "Fake", "synthetic-vector-v1", 3);
        var error = await Assert.ThrowsAsync<KnowledgeException>(() => provider.GenerateAsync(request, token));
        Assert.Equal("PROFILE_INCOMPATIBLE", error.Code);
        Assert.Equal(1, inner.Calls);
        await using var connection = await database.Source.OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            SELECT status,input_tokens,embedding_tokens,provider_reported_cost,currency,estimated_cost,usage_availability
            FROM rag.consumption_attempts WHERE operation_id=@id
            """, connection);
        command.Parameters.AddWithValue("id", operation.OperationId);
        await using var reader = await command.ExecuteReaderAsync(token);
        Assert.True(await reader.ReadAsync(token));
        Assert.Equal("failed", reader.GetString(0));
        Assert.Equal(100L, reader.GetInt64(1));
        Assert.Equal(100L, reader.GetInt64(2));
        Assert.Equal(0.5m, reader.GetDecimal(3));
        Assert.Equal("TEST", reader.GetString(4));
        Assert.Equal(0.0002m, reader.GetDecimal(5));
        Assert.Equal("available", reader.GetString(6));
        Assert.False(await reader.ReadAsync(token));
    }

    private sealed class InvalidVectorProvider(bool nonFinite) : IEmbeddingProvider
    {
        public int Calls { get; private set; }
        public Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new EmbeddingResponse(nonFinite ? [float.NaN, 0, 1] : [1, 0],
                InputTokens: 100, EmbeddingTokens: 100, ProviderReportedCost: 0.5m, Currency: "TEST"));
        }
    }
}
