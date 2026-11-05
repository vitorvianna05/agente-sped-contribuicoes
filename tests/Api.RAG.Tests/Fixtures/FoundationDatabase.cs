using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Microsoft.Extensions.Configuration;

namespace Api.RAG.Tests.Fixtures;

/// <summary>Banco sintético da fundação com aplicação e migrador separados.</summary>
public sealed class FoundationDatabase : IAsyncLifetime
{
    private readonly PostgresFixture postgres = new();
    internal RagOptions Options { get; private set; } = null!;
    internal RagDataSource Source { get; private set; } = null!;
    internal ControlledTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>Prepara schema com identidade própria e executa migrações duas vezes.</summary>
    public async ValueTask InitializeAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await postgres.StartAsync(deadline.Token);
        Options = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-settings.json"))
            .Build().Get<RagOptions>()!;
        Options.ConnectionStrings.Rag = postgres.ApplicationConnectionString;
        Options.ConnectionStrings.RagMigration = postgres.ConnectionString;
        await MigrationRunner.RunAsync(Options, deadline.Token);
        await MigrationRunner.RunAsync(Options, deadline.Token);
        Source = new RagDataSource(Options, synthetic: true);
    }

    /// <summary>Descarta pool e contêiner.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Source is not null)
        {
            await Source.DisposeAsync();
        }

        await postgres.DisposeAsync();
    }
}
