using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Ports;

internal interface IKnowledgeUnitOfWork : IAsyncDisposable
{
    IOperationRepository Operations { get; }
    IAuditWriter Audit { get; }
    Task CommitAsync(CancellationToken cancellationToken);
}

internal interface IKnowledgeUnitOfWorkFactory
{
    Task<IKnowledgeUnitOfWork> BeginAsync(CancellationToken cancellationToken);
}

internal interface IOperationRepository
{
    Task<(Operacao Operation, bool Created)> RegisterAsync(Operacao operation, CancellationToken cancellationToken);
}
