using Api.RAG.Application.Knowledge.Consulta;
using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Controllers;
using Api.RAG.Endpoints;
using Api.RAG.Endpoints.Middleware;
using Api.RAG.Endpoints.OpenApi;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Knowledge.Embeddings;
using Api.RAG.Infrastructure.Knowledge.Observability;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Api.RAG.Infrastructure.Knowledge.Processing;
using Api.RAG.Infrastructure.Security;
using Api.RAG.Ingestao;
using Api.RAG.Rag;
using Api.RAG.Tools;
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
        builder.Services.AddOptions<RagOptions>().Bind(builder.Configuration)
            .PostConfigure(options => options.ApplyOpenAiSection()).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<RagOptions>>(provider =>
            new RagOptionsValidator(provider.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"),
                provider.GetRequiredService<IHostEnvironment>().IsDevelopment()));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(provider => new RagDataSource(provider.GetRequiredService<IOptions<RagOptions>>().Value,
            provider.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing") &&
            provider.GetRequiredService<IOptions<RagOptions>>().Value.ConfigurationPurpose == "SyntheticTestsOnly_NotApprovedForProduction"));
        builder.Services.AddSingleton<IKnowledgeUnitOfWorkFactory>(provider => provider.GetRequiredService<RagDataSource>());
        builder.Services.AddSingleton<ConsumptionRepository>();
        builder.Services.AddScoped<KnowledgeAuditReader>();
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
            manager.FeatureProviders.Add(new KnowledgeControllerFeatureProvider()));
        builder.Services.AddOptions<Microsoft.AspNetCore.Http.Features.FormOptions>().Configure<IOptions<RagOptions>>((options, configured) =>
        {
            options.ValueLengthLimit = 65536;
            options.ValueCountLimit = 2; // Uma seção de arquivo e uma seção de metadados.
            options.MultipartHeadersLengthLimit = 16384;
            options.MultipartBodyLengthLimit = checked(configured.Value.Processing.MaxFileBytes!.Value + 131072);
            options.BufferBody = false;
        });
        builder.Services.AddSingleton<IDocumentStore, DocumentRepository>();
        builder.Services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
        builder.Services.AddSingleton<ITextSegmenter, TokenWindowSegmenter>();
        builder.Services.AddScoped<IngerirFonte>();
        builder.Services.AddScoped<IIngestionPolicy, IngestionPolicy>();
        builder.Services.AddScoped<ProcessarGeracao>();
        builder.Services.AddScoped<ConsultarIngestao>();
        builder.Services.AddScoped<IKnowledgeAuditAccess, KnowledgeAuditAccess>();
        builder.Services.AddHostedService<IngestionWorker>();
        builder.Services.AddSingleton<ProcessingAdmission>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient("OpenAIEmbeddings");
        builder.Services.AddScoped<IKnowledgeStore, KnowledgeRepository>();
        builder.Services.AddScoped<IKnowledgeAccess, KnowledgeAccess>();
        builder.Services.AddSingleton<IEmbeddingProvider, OpenAiEmbeddingService>();
        builder.Services.AddScoped<IProfileEmbeddingProviderFactory, ProfileEmbeddingProviderFactory>();
        builder.Services.AddScoped<ProcessarEmbeddings>();
        builder.Services.AddScoped<ConsultarConhecimento>();
        builder.Services.AddScoped<ConhecimentoService>();
        builder.Services.AddScoped<BuscarConhecimentoTool>();
        builder.Services.AddScoped<ResolverReferenciaTool>();
        builder.Services.AddHostedService<EmbeddingWorker>();
        builder.Services.AddRequestTimeouts();
        builder.Services.AddOptions<RequestTimeoutOptions>().Configure<IOptions<RagOptions>>((timeouts, options) =>
        {
            timeouts.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(options.Value.Processing.TimeoutSeconds!.Value),
                WriteTimeoutResponse = context => ProblemDetailsHandler.WriteAsync(context, 504, "OPERATION_TIMEOUT")
            };
        });
        builder.Services.AddKnowledgeAuthorization(builder.Configuration, builder.Environment.IsDevelopment());
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddKnowledgeOpenApi(!builder.Configuration.GetValue<bool>("Authentication:DisabledForDevelopment"));
        }
        builder.Services.AddMcpServer().WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithListToolsHandler(KnowledgeMcpTools.ListAsync).WithCallToolHandler(KnowledgeMcpTools.CallAsync);
    }
}
