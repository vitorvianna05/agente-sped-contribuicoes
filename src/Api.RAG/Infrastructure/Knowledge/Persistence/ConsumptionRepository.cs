using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Npgsql;
using NpgsqlTypes;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

internal sealed class ConsumptionRepository(RagDataSource source)
{
    public async Task<Guid> BeginAsync(EmbeddingRequest request, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO rag.consumption_attempts(attempt_id,operation_id,correlation_id,service,provider,model,started_at,status,usage_availability)
            VALUES (@id,@operation,@correlation,'Api.RAG',@provider,@model,@started,'pending','unknown')
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("operation", request.OperationId);
        command.Parameters.AddWithValue("correlation", request.CorrelationId);
        command.Parameters.AddWithValue("provider", request.Provider);
        command.Parameters.AddWithValue("model", request.Model);
        command.Parameters.AddWithValue("started", startedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task FinishAsync(Guid id, DateTimeOffset finishedAt, string status, ConsumptionMeasurement measurement,
        CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE rag.consumption_attempts SET finished_at=@finished,status=@status,input_tokens=@input,output_tokens=@output,
                embedding_tokens=@embedding,usage_availability=@availability,estimated_cost=@estimate,provider_reported_cost=@reported,
                currency=@currency,estimated_currency=@estimatedCurrency,price_reference=@reference,price_version=@version WHERE attempt_id=@id AND status='pending'
            """, connection);
        SqlParameters.Add(command, "id", NpgsqlDbType.Uuid, id);
        SqlParameters.Add(command, "finished", NpgsqlDbType.TimestampTz, finishedAt);
        SqlParameters.Add(command, "status", NpgsqlDbType.Text, status);
        SqlParameters.Add(command, "input", NpgsqlDbType.Bigint, measurement.InputTokens);
        SqlParameters.Add(command, "output", NpgsqlDbType.Bigint, measurement.OutputTokens);
        SqlParameters.Add(command, "embedding", NpgsqlDbType.Bigint, measurement.EmbeddingTokens);
        SqlParameters.Add(command, "availability", NpgsqlDbType.Text, measurement.UsageAvailability);
        SqlParameters.Add(command, "estimate", NpgsqlDbType.Numeric, measurement.EstimatedCost);
        SqlParameters.Add(command, "reported", NpgsqlDbType.Numeric, measurement.ProviderReportedCost);
        SqlParameters.Add(command, "currency", NpgsqlDbType.Text, measurement.Currency);
        SqlParameters.Add(command, "estimatedCurrency", NpgsqlDbType.Text, measurement.EstimatedCurrency);
        SqlParameters.Add(command, "reference", NpgsqlDbType.Text, measurement.PriceReference);
        SqlParameters.Add(command, "version", NpgsqlDbType.Text, measurement.PriceVersion);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("Tentativa ausente ou já finalizada.");
        }
    }
}
