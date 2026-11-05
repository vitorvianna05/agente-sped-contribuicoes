using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Knowledge.Observability;
using Npgsql;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

internal sealed class RagDataSource(RagOptions options, bool synthetic) : IKnowledgeUnitOfWorkFactory, IAsyncDisposable
{
    private readonly Lazy<NpgsqlDataSource> source = new(() =>
    {
        RagOptionsValidator.RequirePersistence(options, synthetic);
        var builder = new NpgsqlDataSourceBuilder(options.ConnectionStrings.Rag);
        builder.UseVector();
        return builder.Build();
    });

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        RagOptionsValidator.RequirePersistence(options, synthetic);
        return await source.Value.OpenConnectionAsync(cancellationToken);
    }

    public async Task<IKnowledgeUnitOfWork> BeginAsync(CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);
        try
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken);
            return new KnowledgeUnitOfWork(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => source.IsValueCreated ? source.Value.DisposeAsync() : ValueTask.CompletedTask;

    private sealed class KnowledgeUnitOfWork(NpgsqlConnection connection, NpgsqlTransaction transaction) : IKnowledgeUnitOfWork
    {
        public IOperationRepository Operations { get; } = new OperationRepository(connection, transaction);
        public IAuditWriter Audit { get; } = new KnowledgeAuditWriter(connection, transaction);
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
