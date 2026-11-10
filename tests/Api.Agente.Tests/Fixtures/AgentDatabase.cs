using Api.Agente.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Api.Agente.Tests.Fixtures;

internal sealed class AgentDatabase : IAsyncDisposable
{
    private readonly string applicationPassword = Guid.NewGuid().ToString("N");
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(
        "pgvector/pgvector@sha256:ac08538c6f8b9904c33c8224c5e5706dbe760aca29db1d096972b4052c22a75d")
        .WithDatabase("agente_synthetic").WithUsername("agente_migration").WithPassword(Guid.NewGuid().ToString("N"))
        .WithCleanUp(true).Build();

    public AgentDataOptions Options => new()
    {
        ConnectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        { Username = "agente_application", Password = applicationPassword }.ConnectionString,
        MigrationConnectionString = container.GetConnectionString(),
        TimeoutSeconds = 10,
        RetentionBatchSize = 1,
        PolicyVersion = "synthetic-only-v1",
        ApprovalReference = "synthetic-not-approved-for-production",
        AuditDays = 3,
        ConsumptionDays = 2
    };

    public async Task StartAsync(CancellationToken token)
    {
        await container.StartAsync(token);
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(token);
        await using var format = new NpgsqlCommand("SELECT format('CREATE ROLE agente_application LOGIN PASSWORD %L', @password)", connection);
        format.Parameters.AddWithValue("password", applicationPassword);
        var sql = (string)(await format.ExecuteScalarAsync(token))!;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(token);
        await AgentMigrationRunner.RunAsync(Options, token);
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
