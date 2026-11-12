using System.Net;
using Api.Agente.Analise;
using Api.Agente.Infrastructure;
using Api.Agente.Infrastructure.Consumption;
using Api.Agente.Infrastructure.OpenApi;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.Security;
using Api.Agente.Llm;
using Api.Agente.Mcp;
using Api.Agente.Validacao;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Api.Agente;

/// <summary>Host da API de análise SPED com validação estrutural, MCP e parecer condicionado aos gates.</summary>
public sealed partial class Program
{
    private Program() { }

    /// <summary>Inicializa a aplicação com configuração externa validada.</summary>
    /// <param name="args">Argumentos do host.</param>
    public static async Task Main(string[] args)
    {
        var migrate = args.Contains("--migrate-agent", StringComparer.Ordinal);
        var purge = args.Contains("--purge-agent", StringComparer.Ordinal);
        if (migrate && purge) { throw new AnalysisException("MAINTENANCE_MODE_AMBIGUOUS"); }
        var builder = WebApplication.CreateBuilder(args.Where(argument => argument is not ("--migrate-agent" or "--purge-agent")).ToArray());
        if (purge)
        {
            await AgentRetentionRunner.RunAsync(builder.Configuration.GetSection("AgentData").Get<AgentDataOptions>() ?? new(), TimeProvider.System, CancellationToken.None);
            return;
        }
        if (migrate)
        {
            await AgentMigrationRunner.RunAsync(builder.Configuration.GetSection("AgentData").Get<AgentDataOptions>() ?? new(), CancellationToken.None);
            return;
        }
        builder.Services.AddOptions<LlmOptions>().BindConfiguration("Llm").ValidateOnStart();
        builder.Services.AddOptions<AgentDataOptions>().BindConfiguration("AgentData").ValidateOnStart();
        builder.Services.AddOptions<LayoutPolicyOptions>().BindConfiguration("LayoutPolicy").ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<LlmOptions>, AnalysisOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<AgentDataOptions>, AnalysisOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<LayoutPolicyOptions>, AnalysisOptionsValidator>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IAnalysisLedger, PostgresAnalysisLedger>();
        builder.Services.AddScoped<IParecerLlm, OpenAiParecerLlm>();
        builder.Services.AddScoped<MeasuredParecerLlm>();
        builder.Services.AddScoped<SpedValidator>();
        builder.Services.AddScoped<AnaliseSpedService>();
        builder.Services.AddHttpClient("OpenAIParecer", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        builder.Services.AddOptions<AgenteOptions>().Bind(builder.Configuration).ValidateOnStart();
        builder.Services.AddOptions<ConhecimentoMcpOptions>().BindConfiguration("KnowledgeMcp").ValidateOnStart();
        builder.Services.AddOptions<SpedParserOptions>().BindConfiguration("SpedParser").ValidateOnStart();
        builder.Services.AddSingleton<AgenteOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<AgenteOptions>>(provider => provider.GetRequiredService<AgenteOptionsValidator>());
        builder.Services.AddSingleton<IValidateOptions<ConhecimentoMcpOptions>>(provider => provider.GetRequiredService<AgenteOptionsValidator>());
        builder.Services.AddSingleton<IValidateOptions<SpedParserOptions>>(provider => provider.GetRequiredService<AgenteOptionsValidator>());
        builder.Services.AddScoped<ISpedParser, SpedParser>();
        builder.Services.AddAnalysisAuthorization(builder.Configuration, builder.Environment);
        builder.Services.AddControllers();
        // Borda HTTP lida das opções validadas (appsettings, variáveis de ambiente ou testes), não do builder.
        builder.Services.AddCors();
        builder.Services.AddOptions<CorsOptions>().Configure<IOptions<AgenteOptions>>((cors, edge) =>
            // Somente navegadores das origens pactuadas; sem credenciais de cookie. Lista vazia = nenhuma origem.
            cors.AddPolicy(AgentCorsOptions.Policy, policy => policy
                .WithOrigins(edge.Value.Cors.AllowedOrigins).WithMethods("GET", "POST")
                .WithHeaders("Authorization", "Content-Type").WithExposedHeaders("X-Correlation-Id")));
        builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<AgenteOptions>>((forwarded, edge) =>
        {
            // TLS terminado em proxy confiável: X-Forwarded-Proto/For só dos IPs/redes configurados; sem lista, ignorados.
            var transport = edge.Value.Transport;
            forwarded.KnownProxies.Clear();
            forwarded.KnownIPNetworks.Clear();
            forwarded.ForwardLimit = 1;
            forwarded.ForwardedHeaders = transport.TrustsForwardedHeaders ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto : ForwardedHeaders.None;
            // Valores inválidos são recusados no startup por AgenteOptionsValidator.
            foreach (var proxy in transport.TrustedProxies)
            { if (IPAddress.TryParse(proxy, out var address)) { forwarded.KnownProxies.Add(address); } }
            foreach (var network in transport.TrustedNetworks)
            { if (System.Net.IPNetwork.TryParse(network, out var range) && range.PrefixLength > 0) { forwarded.KnownIPNetworks.Add(range); } }
        });
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddAgentOpenApi();
        }
        builder.Services.AddTransient<BoundedKnowledgeHandler>();
        builder.Services.AddHttpClient("KnowledgeMcp", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            .AddHttpMessageHandler<BoundedKnowledgeHandler>();
        builder.Services.AddScoped<IConhecimentoClient, ConhecimentoMcpClient>();
        var app = builder.Build();
        app.UseForwardedHeaders();
        app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<AgenteOptions>>().Value;
            context.TraceIdentifier = Guid.NewGuid().ToString("N");
            context.Items["ExecutionId"] = Guid.NewGuid();
            context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
            // Cabeçalhos CORS dependem da origem: caches/proxies não podem reutilizar a resposta entre origens.
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.Vary.Any(value => value?.Contains("Origin", StringComparison.OrdinalIgnoreCase) == true))
                { context.Response.Headers.Append("Vary", "Origin"); }
                return Task.CompletedTask;
            });
            var requestToken = context.RequestAborted;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.Limits.TimeoutSeconds));
            context.RequestAborted = deadline.Token;
            try
            {
                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
                {
                    feature.MaxRequestBodySize = options.Limits.MaxRequestBytes;
                }
                if (context.Request.ContentLength > options.Limits.MaxRequestBytes)
                {
                    await Problem(context, 413, "REQUEST_TOO_LARGE");
                    return;
                }
                var ip = context.Connection.RemoteIpAddress;
                var testLoopback = app.Environment.IsEnvironment("Testing") && ip is not null &&
                    IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
                if ((!context.Request.IsHttps && !testLoopback) ||
                    !options.Authentication.AllowedHosts.Contains(context.Request.Host.Host, StringComparer.OrdinalIgnoreCase))
                {
                    await Problem(context, 403, "TRANSPORT_DENIED");
                    return;
                }
                await next(context);
            }
            catch (OperationCanceledException) when (requestToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) { await Problem(context, 504, "OPERATION_TIMEOUT"); }
            catch (BadHttpRequestException exception) when (exception.StatusCode == 413)
            {
                await Problem(context, 413, "REQUEST_TOO_LARGE");
            }
            catch (AnalysisException exception)
            {
                await Problem(context, exception.Code == "EXECUTION_NOT_FOUND" ? 404 : exception.Code == "ENCODING_NOT_SUPPORTED" ? 415 : 503, exception.Code);
            }
            catch (SpedReadException exception)
            {
                var status = exception.Code switch
                {
                    "SPED_ENCODING_NOT_SUPPORTED" => 415,
                    "PARSER_NOT_CONFIGURED" => 503,
                    "SPED_FILE_EMPTY" => 422,
                    _ => 413
                };
                await Problem(context, status, exception.Code, exception.LineNumber);
            }
            catch (KnowledgeClientException exception) { await Problem(context, exception.Code == "KNOWLEDGE_TIMEOUT" ? 504 : 503, exception.Code); }
            catch (Exception exception) when (exception is not OutOfMemoryException) { await Problem(context, 503, "DEPENDENCY_UNAVAILABLE"); }
            finally
            {
                context.RequestAborted = requestToken;
                RequestCompleted(app.Logger, context.User.FindFirst("sub")?.Value ?? "anonymous", context.TraceIdentifier, context.Response.StatusCode);
            }
        });
        app.UseStatusCodePages(async status => await Problem(status.HttpContext, status.HttpContext.Response.StatusCode,
            status.HttpContext.Response.StatusCode == 401 ? "AUTHENTICATION_REQUIRED" : "REQUEST_REJECTED"));
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        if (app.Environment.IsDevelopment()) { app.UseAgentSwagger(); }
        // CORS só nas rotas da API; health e Swagger não recebem permissão de origem.
        app.MapControllers().RequireCors(AgentCorsOptions.Policy);
        app.MapGet("/health/live", () => Results.Ok(new { status = "alive", analysisAvailable = false })).AllowAnonymous();
        await app.RunAsync();
    }

    private static Task Problem(HttpContext context, int status, string code, int? lineNumber = null) => Results.Problem(
        statusCode: status, title: code, extensions: new Dictionary<string, object?>
        {
            ["contractVersion"] = "1.0",
            ["correlationId"] = context.TraceIdentifier,
            ["code"] = code,
            ["executionId"] = context.Items["ExecutionId"],
            ["lineNumber"] = lineNumber
        }).ExecuteAsync(context);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent request actor={Actor} correlation={Correlation} status={Status}")]
    private static partial void RequestCompleted(ILogger logger, string actor, string correlation, int status);
}
