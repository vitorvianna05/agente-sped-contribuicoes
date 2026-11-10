using Api.Agente.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace Api.Agente.Infrastructure.OpenApi;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class OpenApiSpedUploadAttribute : Attribute;

internal static class AgentOpenApi
{
    public static IServiceCollection AddAgentOpenApi(this IServiceCollection services) =>
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer(async (document, context, _) =>
            {
                var bearerAuthentication = await UsesBearer(context.ApplicationServices);
                document.Info = new OpenApiInfo
                {
                    Title = "Api.Agente — SPED Contribuições",
                    Version = "v1",
                    Description = "Leitura estrutural, análise com evidências MCP e consulta de consumo. " +
                        "Envie o arquivo original Latin-1/CRLF pelo seletor de arquivo. " +
                        "A análise depende de política/conexão e mapeamento de leiaute configurados; " +
                        "cobertura parcial e revisão humana não equivalem a conformidade fiscal. " +
                        (bearerAuthentication ? "Use Authorize com o JWT da Api.Agente, não o token da RAG." :
                            "Modo local sem JWT: somente Development/loopback, com HTTPS e hosts permitidos.")
                };
                if (bearerAuthentication)
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "JWT com subject e scope analysis.execute; diagnóstico exige analysis.diagnose e atribuição administrativa."
                    };
                }
            });
            options.AddOperationTransformer(async (operation, context, token) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var authorized = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
                var bearerAuthentication = await UsesBearer(context.ApplicationServices);
                if (bearerAuthentication && authorized)
                {
                    operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] }];
                }
                operation.Responses ??= new OpenApiResponses();
                if (authorized)
                {
                    var problem = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: token);
                    foreach (var (status, description) in new[]
                    {
                        ("401", "Autenticação obrigatória."), ("403", "Transporte, permissão ou atribuição negados."),
                        ("413", "Limite da requisição ou do parser excedido."),
                        ("503", "Configuração, auditoria ou dependência indisponível."), ("504", "Prazo da operação excedido.")
                    })
                    {
                        operation.Responses[status] = new OpenApiResponse
                        {
                            Description = description,
                            Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = problem } }
                        };
                    }
                }
                if (metadata.OfType<OpenApiSpedUploadAttribute>().Any())
                {
                    operation.RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Description = "Arquivo SPED original ISO-8859-1 (Latin-1), com CRLF. Corpo bruto; não enviar JSON ou multipart.",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/octet-stream"] = BinaryFile(),
                            ["text/plain"] = BinaryFile()
                        }
                    };
                    operation.Responses["415"] = new OpenApiResponse { Description = "Tipo de conteúdo ou charset incompatível; use application/octet-stream com o arquivo original." };
                }
                switch (context.Description.RelativePath)
                {
                    case "api/v1/analises":
                        operation.Summary = "Analisar SPED e elaborar rascunho de parecer";
                        operation.Description = "Exige analysis.execute. Sem política/conexão retorna 503/AUDIT_NOT_CONFIGURED; " +
                            "sem mapeamento retorna unsupported. Resultados partial/failed/unsupported são informados no JSON, inclusive em HTTP 200.";
                        operation.Responses["200"] = new OpenApiResponse
                        {
                            Description = "Resultado da execução com validações, evidências, rascunho quando disponível e limitações.",
                            Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = await context.GetOrCreateSchemaAsync(typeof(AnalysisResult), cancellationToken: token) } }
                        };
                        break;
                    case "api/v1/sped/leitura":
                        operation.Summary = "Ler arquivo SPED sem RAG ou IA";
                        operation.Description = "Exige analysis.execute. Preserva registros/campos, verifica estrutura e retorna verificações fiscais não realizadas. Não requer PostgreSQL ou OpenAI.";
                        operation.Responses["422"] = new OpenApiResponse { Description = "Arquivo vazio." };
                        break;
                    case "api/v1/analises/{id}/consumo":
                        operation.Summary = "Consultar tentativas e consumo da própria execução";
                        operation.Description = "Exige analysis.execute e mesmo subject/organização. Custos e tokens não informados permanecem nulos.";
                        operation.Responses["404"] = new OpenApiResponse { Description = "Execução inexistente, expirada ou pertencente a outro contexto." };
                        break;
                    case "admin/v1/knowledge/readiness":
                        operation.Summary = "Diagnosticar descoberta e contratos MCP";
                        operation.Description = "Exige analysis.diagnose e principal administrativo atribuído, ou modo local autorizado. Não executa análise fiscal.";
                        break;
                    case "health/live":
                        operation.Summary = "Consultar disponibilidade do processo";
                        operation.Description = "Não comprova prontidão fiscal, banco, RAG ou OpenAI.";
                        break;
                }
            });
        });

    private static async Task<bool> UsesBearer(IServiceProvider services) =>
        (await services.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync())?.HandlerType == typeof(JwtBearerHandler);

    private static OpenApiMediaType BinaryFile() => new()
    {
        Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
    };

    public static void UseAgentSwagger(this WebApplication app)
    {
        app.MapOpenApi("/openapi/v1.json").AllowAnonymous();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("../openapi/v1.json", "Api.Agente v1");
            options.DocumentTitle = "Api.Agente — Swagger";
            options.DisplayRequestDuration();
        });
    }
}
