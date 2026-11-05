using Npgsql;
using Testcontainers.PostgreSql;

namespace Api.RAG.Tests.Fixtures;

internal sealed class PostgresFixture : IAsyncDisposable
{
    private readonly string applicationPassword = Guid.NewGuid().ToString("N");
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(
        "pgvector/pgvector@sha256:ac08538c6f8b9904c33c8224c5e5706dbe760aca29db1d096972b4052c22a75d")
        .WithDatabase("rag_synthetic")
        .WithUsername("rag_test")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public string ApplicationConnectionString => new NpgsqlConnectionStringBuilder(ConnectionString)
    {
        Username = "rag_application",
        Password = applicationPassword
    }.ConnectionString;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await container.StartAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var formatRole = new NpgsqlCommand("SELECT format('CREATE ROLE rag_application LOGIN PASSWORD %L', @password)", connection);
        formatRole.Parameters.AddWithValue("password", applicationPassword);
        var createRole = (string)(await formatRole.ExecuteScalarAsync(cancellationToken))!;
        await using var role = new NpgsqlCommand(createRole, connection);
        await role.ExecuteNonQueryAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
