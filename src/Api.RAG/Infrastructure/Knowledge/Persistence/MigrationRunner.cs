using System.Security.Cryptography;
using System.Text;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Npgsql;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

internal static class MigrationRunner
{
    public static async Task RunAsync(RagOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionStrings.Rag) || string.IsNullOrWhiteSpace(options.ConnectionStrings.RagMigration))
        {
            throw new KnowledgeException("MIGRATION_IDENTITY_REQUIRED");
        }

        var application = new NpgsqlConnectionStringBuilder(options.ConnectionStrings.Rag);
        var migration = new NpgsqlConnectionStringBuilder(options.ConnectionStrings.RagMigration);
        if (string.IsNullOrWhiteSpace(application.Username) || application.Username == migration.Username ||
            application.Database != migration.Database || application.Host != migration.Host || application.Port != migration.Port)
        {
            throw new KnowledgeException("MIGRATION_IDENTITY_REQUIRED");
        }

        await using var connection = new NpgsqlConnection(migration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var setup = new NpgsqlCommand("""
            SELECT pg_advisory_xact_lock(732192041);
            CREATE SCHEMA IF NOT EXISTS rag;
            CREATE TABLE IF NOT EXISTS rag.schema_migrations (name text PRIMARY KEY, checksum text NOT NULL);
            """, connection, transaction);
        await setup.ExecuteNonQueryAsync(cancellationToken);
        var assembly = typeof(MigrationRunner).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".sql", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            await using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);
            var checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            await using var lookup = new NpgsqlCommand("SELECT checksum FROM rag.schema_migrations WHERE name = @name", connection, transaction);
            lookup.Parameters.AddWithValue("name", resource);
            var existing = await lookup.ExecuteScalarAsync(cancellationToken);
            if (existing is string previous)
            {
                if (previous != checksum)
                {
                    throw new KnowledgeException("MIGRATION_CHECKSUM_MISMATCH");
                }

                continue;
            }

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await using var record = new NpgsqlCommand("INSERT INTO rag.schema_migrations VALUES (@name, @checksum)", connection, transaction);
            record.Parameters.AddWithValue("name", resource);
            record.Parameters.AddWithValue("checksum", checksum);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        using var quoting = new NpgsqlCommandBuilder();
        var role = quoting.QuoteIdentifier(application.Username);
        await using var grants = new NpgsqlCommand($"""
            GRANT USAGE ON SCHEMA rag TO {role};
            GRANT SELECT, INSERT, UPDATE, DELETE ON rag.operations, rag.consumption_attempts, rag.audit_entries TO {role};
            """, connection, transaction);
        await grants.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
