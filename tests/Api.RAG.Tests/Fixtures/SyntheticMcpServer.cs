using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

namespace Api.RAG.Tests.Fixtures;

internal sealed class SyntheticMcpServer : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly SyntheticTools tools;

    private SyntheticMcpServer(WebApplication app, SyntheticTools tools)
    {
        this.app = app;
        this.tools = tools;
    }

    public Task BlockedCallStarted => tools.Started.Task;

    public void ReleaseBlockedCall() => tools.Release.TrySetResult();

    public Uri Endpoint
    {
        get
        {
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new Uri(new Uri(address), "/mcp");
        }
    }

    public static async Task<SyntheticMcpServer> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var tools = new SyntheticTools();
        builder.Services.AddMcpServer().WithHttpTransport(options =>
            options.SessionMode = HttpServerSessionMode.Stateless).WithTools(tools);
        var app = builder.Build();
        app.MapMcp("/mcp");
        try
        {
            await app.StartAsync(cancellationToken);
            return new SyntheticMcpServer(app, tools);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        ReleaseBlockedCall();
        return app.DisposeAsync();
    }

    [McpServerToolType]
    private sealed class SyntheticTools
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        [McpServerTool(Name = "synthetic_echo"), Description("Eco técnico sintético sem conhecimento fiscal.")]
        public static string Echo(string value) => value;

        [McpServerTool(Name = "synthetic_wait"), Description("Espera controlada para testar encerramento do consumidor.")]
        public async Task<string> WaitAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return "released";
        }
    }
}
