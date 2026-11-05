using Npgsql;
using NpgsqlTypes;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

internal static class SqlParameters
{
    public static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
}
