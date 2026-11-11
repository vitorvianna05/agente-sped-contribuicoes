using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Api.RAG.Endpoints.OpenApi;

/// <summary>Upload multipart de ingestão: campo de arquivo "arquivo" e campo texto "metadata".</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class OpenApiIngestUploadAttribute : Attribute;

/// <summary>Documento OpenAPI e Swagger UI, expostos apenas em Development para testes manuais.</summary>
internal static class KnowledgeOpenApi
{
    private const string DocumentPath = "/openapi/v1.json";

    // Perfil de desenvolvimento de appsettings.Development.Example.json.
    private const string MetadataExample = """
        {
          "contractVersion": "1.0",
          "issuer": "RFB",
          "officialIdentifier": "guia-pratico-efd-contribuicoes",
          "title": "Guia Prático da EFD-Contribuições",
          "kind": "guide",
          "originUri": "https://example.com/guia-pratico.pdf",
          "officialVersion": "1.35",
          "profileId": "d0c0a1e0-0000-4000-8000-000000000005",
          "applicabilities": [
            { "documentType": "EFD-Contribuicoes", "layoutVersion": "1.35", "officialBasis": "Desenvolvimento local" }
          ]
        }
        """;

    public static IServiceCollection AddKnowledgeOpenApi(this IServiceCollection services, bool bearerAuthentication) =>
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Api.RAG",
                    Version = "v1",
                    Description = "Ingestão da base fiscal. Concluídos texto e vetores, o documento é publicado automaticamente; " +
                        "o conteúdo é consultado via MCP (/mcp: BuscarConhecimento e ResolverReferencia)."
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
                        Description = "Token JWT do emissor configurado em Authentication:Authority."
                    };
                    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
                }
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<OpenApiIngestUploadAttribute>().Any())
                {
                    operation.RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["multipart/form-data"] = new()
                            {
                                Schema = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.Object,
                                    Required = new HashSet<string> { "arquivo", "metadata" },
                                    Properties = new Dictionary<string, IOpenApiSchema>
                                    {
                                        ["arquivo"] = new OpenApiSchema
                                        {
                                            Type = JsonSchemaType.String,
                                            Format = "binary",
                                            Description = "PDF (application/pdf) dentro de Processing:MaxFileBytes e Processing:MaxPages."
                                        },
                                        ["metadata"] = new OpenApiSchema
                                        {
                                            Type = JsonSchemaType.String,
                                            Description = "JSON de metadados (contractVersion 1.0). profileId deve existir em Processing:Profiles.",
                                            // Default preenche o campo no Swagger UI; ajustar officialVersion a cada envio.
                                            Default = JsonValue.Create(MetadataExample)
                                        }
                                    }
                                }
                            }
                        }
                    };
                }
                return Task.CompletedTask;
            });
        });

    public static void UseKnowledgeSwaggerUi(this WebApplication app)
    {
        app.MapOpenApi(DocumentPath).AllowAnonymous();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint(DocumentPath, "Api.RAG v1");
            options.DocumentTitle = "Api.RAG - Swagger";
            options.DisplayRequestDuration();
        });
    }
}
