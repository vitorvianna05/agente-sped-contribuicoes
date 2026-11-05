using Npgsql;
using Testcontainers.PostgreSql;

namespace Api.RAG.Tests.Fixtures;

internal sealed class PostgresFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(
        "pgvector/pgvector@sha256:ac08538c6f8b9904c33c8224c5e5706dbe760aca29db1d096972b4052c22a75d")
        .WithDatabase("rag_synthetic")
        .WithUsername("rag_test")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await container.StartAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
