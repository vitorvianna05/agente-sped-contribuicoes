using Api.RAG.Application.Knowledge.Concurrency;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Endpoints;
using Api.RAG.Endpoints.Middleware;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Knowledge.Observability;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Api.RAG.Infrastructure.Security;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

namespace Api.RAG;

/// <summary>Ponto de entrada da API de conhecimento fiscal.</summary>
public sealed class Program
{
    private Program()
    {
    }

    /// <summary>Inicializa o host da aplicação.</summary>
    /// <param name="args">Argumentos de inicialização.</param>
    public static async Task Main(string[] args)
    {
        var migrate = args.Contains("--migrate", StringComparer.Ordinal);
        var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--migrate").ToArray());
        if (migrate)
        {
            var options = builder.Configuration.Get<RagOptions>() ?? new RagOptions();
            await MigrationRunner.RunAsync(options, CancellationToken.None);
            return;
        }

        AddKnowledgeFoundation(builder);
        var app = builder.Build();
        app.MapKnowledgeFoundation();
        await app.RunAsync();
    }

    private static void AddKnowledgeFoundation(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<RagOptions>().Bind(builder.Configuration).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<RagOptions>>(provider =>
            new RagOptionsValidator(provider.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing")));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(provider => new RagDataSource(provider.GetRequiredService<IOptions<RagOptions>>().Value,
            provider.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing") &&
            provider.GetRequiredService<IOptions<RagOptions>>().Value.ConfigurationPurpose == "SyntheticTestsOnly_NotApprovedForProduction"));
        builder.Services.AddSingleton<IKnowledgeUnitOfWorkFactory>(provider => provider.GetRequiredService<RagDataSource>());
        builder.Services.AddSingleton<ConsumptionRepository>();
        builder.Services.AddScoped<KnowledgeAuditReader>();
        builder.Services.AddScoped<IdempotencyCoordinator>();
        builder.Services.AddRequestTimeouts();
        builder.Services.AddOptions<RequestTimeoutOptions>().Configure<IOptions<RagOptions>>((timeouts, options) =>
        {
            timeouts.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(options.Value.Processing.TimeoutSeconds!.Value),
                WriteTimeoutResponse = context => ProblemDetailsHandler.WriteAsync(context, 504, "OPERATION_TIMEOUT")
            };
        });
        builder.Services.AddKnowledgeAuthorization(builder.Configuration);
        builder.Services.AddMcpServer().WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithListToolsHandler((_, _) => ValueTask.FromResult(new ModelContextProtocol.Protocol.ListToolsResult { Tools = [] }));
    }
}
