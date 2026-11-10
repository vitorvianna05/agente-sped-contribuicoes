using System.Security.Cryptography;
using System.Text;
using Api.Agente.Analise;
using Npgsql;

namespace Api.Agente.Infrastructure.Persistence;

internal static class AgentMigrationRunner
{
    public static async Task RunAsync(AgentDataOptions options, CancellationToken token)
    {
        var migration = ValidateIdentities(options);
        var app = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        await using var connection = new NpgsqlConnection(migration.ConnectionString);
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var setup = new NpgsqlCommand("SELECT pg_advisory_xact_lock(176441257); CREATE SCHEMA IF NOT EXISTS agente; CREATE TABLE IF NOT EXISTS agente.schema_migrations(name text PRIMARY KEY,checksum text NOT NULL)", connection, transaction);
        await setup.ExecuteNonQueryAsync(token);
        var assembly = typeof(AgentMigrationRunner).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".sql", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            var sql = await reader.ReadToEndAsync(token);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            await using var lookup = new NpgsqlCommand("SELECT checksum FROM agente.schema_migrations WHERE name=@name", connection, transaction);
            lookup.Parameters.AddWithValue("name", name);
            var existing = await lookup.ExecuteScalarAsync(token);
            if (existing is string previous)
            {
                if (previous != hash) { throw new AnalysisException("MIGRATION_CHECKSUM_MISMATCH"); }
                continue;
            }
            await using var apply = new NpgsqlCommand(sql, connection, transaction);
            await apply.ExecuteNonQueryAsync(token);
            await using var record = new NpgsqlCommand("INSERT INTO agente.schema_migrations VALUES (@name,@hash)", connection, transaction);
            record.Parameters.AddWithValue("name", name); record.Parameters.AddWithValue("hash", hash);
            await record.ExecuteNonQueryAsync(token);
        }
        using var quoting = new NpgsqlCommandBuilder();
        var role = quoting.QuoteIdentifier(app.Username!);
        await using var grants = new NpgsqlCommand($"GRANT USAGE ON SCHEMA agente TO {role}; GRANT SELECT, INSERT, UPDATE ON agente.executions, agente.consumption_attempts TO {role}", connection, transaction);
        await grants.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
    }

    internal static NpgsqlConnectionStringBuilder ValidateIdentities(AgentDataOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString) || string.IsNullOrWhiteSpace(options.MigrationConnectionString))
        { throw new AnalysisException("MIGRATION_IDENTITY_REQUIRED"); }
        if (options.TimeoutSeconds <= 0) { throw new AnalysisException("MIGRATION_LIMITS_REQUIRED"); }
        var app = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        var migration = new NpgsqlConnectionStringBuilder(options.MigrationConnectionString)
        { Timeout = options.TimeoutSeconds, CommandTimeout = options.TimeoutSeconds };
        if (string.IsNullOrWhiteSpace(app.Username) || app.Username == migration.Username || app.Database != migration.Database || app.Host != migration.Host || app.Port != migration.Port)
        { throw new AnalysisException("MIGRATION_IDENTITY_REQUIRED"); }
        return migration;
    }
}
