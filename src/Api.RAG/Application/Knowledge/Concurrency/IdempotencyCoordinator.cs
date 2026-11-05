using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Concurrency;

internal sealed class IdempotencyCoordinator(IKnowledgeUnitOfWorkFactory factory, TimeProvider clock)
{
    public async Task<(Operacao Operation, bool Created)> RegisterAsync(Operacao operation, CancellationToken cancellationToken)
    {
        operation.Validate();
        await using var unit = await factory.BeginAsync(cancellationToken);
        var result = await unit.Operations.RegisterAsync(operation, cancellationToken);
        if (result.Created)
        {
            await unit.Audit.WriteAsync(new AuditEntry(Guid.NewGuid(), operation.PrincipalId, null,
                operation.Type, operation.OperationId, "accepted", clock.GetUtcNow(), operation.CorrelationId), cancellationToken);
        }

        await unit.CommitAsync(cancellationToken);
        return result;
    }
}
