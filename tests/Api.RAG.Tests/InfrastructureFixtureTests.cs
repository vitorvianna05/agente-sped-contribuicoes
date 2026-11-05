using System.Diagnostics;
using System.Net;
using Api.RAG.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using Npgsql;

namespace Api.RAG.Tests;

/// <summary>Verifica as fronteiras e o ciclo de vida da infraestrutura T005.</summary>
public sealed class InfrastructureFixtureTests
{
    /// <summary>Confirma configuração isolada, relógio controlável e bloqueio de HTTP externo.</summary>
    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task HostUsesOnlySyntheticConfigurationAndBlocksOutboundHttp()
    {
        await using var host = new RagTestHost();
        using var client = host.CreateClient();
        var settings = host.Services.GetRequiredService<IConfiguration>();
        Assert.Null(settings.GetConnectionString("Rag"));
        Assert.Equal("Fake", settings["Embeddings:Provider"]);
        var clock = host.Services.GetRequiredService<TimeProvider>();
        var before = clock.GetUtcNow();
        host.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(before.AddDays(1), clock.GetUtcNow());
        using var outbound = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => outbound.GetAsync(
            new Uri("https://provider.synthetic.invalid"), TestContext.Current.CancellationToken));
        using var response = await client.GetAsync("/mcp", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>O provider fake é determinístico e respeita cancelamento sem chamar a rede.</summary>
    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task SyntheticEmbeddingsAreRepeatableAndCancellable()
    {
        var provider = new SyntheticEmbeddingProvider();
        var first = await provider.EmbedAsync("fixture A", TestContext.Current.CancellationToken);
        Assert.Equal(first, await provider.EmbedAsync("fixture A", TestContext.Current.CancellationToken));
        Assert.NotEqual(first, await provider.EmbedAsync("fixture B", TestContext.Current.CancellationToken));
        Assert.Equal(3, first.Length);
        Assert.Equal(3, provider.Calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.EmbedAsync(
            "fixture A", new CancellationToken(canceled: true)));
    }

    /// <summary>Cliente SDK real descobre e chama tool sintética em outro processo via HTTP.</summary>
    [Fact]
    [Trait("Category", TestCategories.Contract)]
    public async Task McpConsumerDiscoversAndCallsAcrossProcessBoundary()
    {
        await using var server = await SyntheticMcpServer.StartAsync(TestContext.Current.CancellationToken);
        var result = await McpConsumerProcess.ExecuteAsync(new McpConsumerRequest(
            server.Endpoint, "synthetic_echo", new() { ["value"] = "fixture-only" }),
            TestContext.Current.CancellationToken);
        Assert.NotEqual(Environment.ProcessId, result.ProcessId);
        Assert.Contains("synthetic_echo", result.Tools);
        Assert.NotNull(result.Result);
        Assert.NotEqual(true, result.Result.IsError);
        Assert.Contains(result.Result.Content, block => block is TextContentBlock text && text.Text == "fixture-only");
    }

    /// <summary>Falha de transporte não é convertida em descoberta vazia ou sucesso.</summary>
    [Fact]
    [Trait("Category", TestCategories.Contract)]
    public async Task McpConsumerRejectsNonLoopbackTarget()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => McpConsumerProcess.ExecuteAsync(
            new McpConsumerRequest(new Uri("https://provider.synthetic.invalid/mcp"), null, null),
            TestContext.Current.CancellationToken));
    }

    /// <summary>Cancelamento e timeout encerram o processo com uma chamada MCP ainda pendente.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", TestCategories.Contract)]
    public async Task McpConsumerTerminatesPendingProcess(bool cancelByCaller)
    {
        await using var server = await SyntheticMcpServer.StartAsync(TestContext.Current.CancellationToken);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Process? child = null;
        var execution = McpConsumerProcess.ExecuteAsync(
            new McpConsumerRequest(server.Endpoint, "synthetic_wait", null), caller.Token,
            timeout: TimeSpan.FromSeconds(15),
            processStarted: id => child = Process.GetProcessById(id));
        try
        {
            // Sincronização pela entrada na tool: não depende de sleeps nem de supor que o filho iniciou.
            await server.BlockedCallStarted.WaitAsync(TimeSpan.FromSeconds(12), TestContext.Current.CancellationToken);
            Assert.NotNull(child);
            Assert.False(child.HasExited);
            if (cancelByCaller)
            {
                await caller.CancelAsync();
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            Assert.Equal(cancelByCaller, caller.IsCancellationRequested);
            Assert.True(child.HasExited);
        }
        finally
        {
            await caller.CancelAsync();
            server.ReleaseBlockedCall();
            try
            {
                await execution;
            }
            catch (OperationCanceledException) when (caller.IsCancellationRequested)
            {
                // O cancelamento esperado já foi verificado; aguardar garante descarte também em falha de asserção.
            }

            child?.Dispose();
        }
    }

    /// <summary>Cria banco novo, habilita vector e exercita ida e volta sem dados externos.</summary>
    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task PostgresContainerSupportsVectorRoundTrip()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        await using var postgres = new PostgresFixture();
        await postgres.StartAsync(deadline.Token);
        var builder = new NpgsqlDataSourceBuilder(postgres.ConnectionString);
        builder.UseVector();
        await using var source = builder.Build();
        await using var command = source.CreateCommand("SELECT '[1,2,3]'::vector");
        var vector = Assert.IsType<Pgvector.Vector>(await command.ExecuteScalarAsync(deadline.Token));
        Assert.Equal(new float[] { 1, 2, 3 }, vector.ToArray());
    }
}
