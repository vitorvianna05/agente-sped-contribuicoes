using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.RAG.Application.Knowledge.Consulta;
using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Tests.Fixtures;
using Api.RAG.Tests.Fixtures.Documents;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Api.RAG.Tests.Contracts;

/// <summary>REST administrativo com JWT, PostgreSQL e processamento real de PDF sintético.</summary>
public sealed class IngestionContractTests(FoundationDatabase database) : IClassFixture<FoundationDatabase>
{
    internal static IngestionMetadata Metadata() => new("1.0", "Synthetic issuer", Guid.NewGuid().ToString(),
        "Synthetic manual", "manual", "https://official.synthetic.invalid/manual", "opaque-edition-1",
        Guid.Parse("10000000-0000-0000-0000-000000000001"), [new("synthetic-type", "synthetic-layout", "Synthetic basis; not fiscal authority")]);

    internal static HttpClient Client(RagTestHost host, string? subject = "central-admin", string scopes = "knowledge.ingest knowledge.audit",
        WebApplicationFactory<Program>? application = null)
    {
        var client = (application ?? host).CreateClient();
        if (subject is not null)
        {
            var token = new JwtSecurityToken("https://identity.synthetic.invalid", "api-rag-synthetic",
                [new Claim("sub", subject), new Claim("scope", scopes)], DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(host.SigningKey, SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }

    internal static async Task<HttpResponseMessage> Upload(HttpClient client, IngestionMetadata metadata, byte[] pdf,
        string mime = "application/pdf", string? overrideJson = null)
    {
        using var body = new MultipartFormDataContent();
        using var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        body.Add(file, "arquivo", "synthetic.pdf");
        body.Add(new StringContent(overrideJson ?? JsonSerializer.Serialize(metadata, IngestionMetadata.JsonOptions), Encoding.UTF8), "metadata");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest") { Content = body };
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Sem Idempotency-Key: envios concorrentes do mesmo documento convergem para uma operação; sem vetores, não publica.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task ConcurrentUploadsConvergeAndTextOnlyRemainsUnpublished()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        using var client = Client(host);
        var metadata = Metadata();
        var pdf = SyntheticPdfFactory.Create("First synthetic evidence", "Second synthetic evidence");
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Upload(client, metadata, pdf)));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
            Assert.Single(responses.Select(response => response.Headers.Location).Distinct());
            var location = responses[0].Headers.Location!;
            JsonElement operation = default;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!deadline.IsCancellationRequested)
            {
                using var status = await client.GetAsync(location, deadline.Token);
                status.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await status.Content.ReadAsStringAsync(deadline.Token));
                operation = document.RootElement.GetProperty("operation").Clone();
                if (operation.GetProperty("preparationState").GetString() == "ready")
                {
                    break;
                }
                await Task.Delay(100, deadline.Token);
            }
            Assert.Equal("queued", operation.GetProperty("state").GetString());
            Assert.Equal("EMBEDDINGS_PENDING", operation.GetProperty("resultCode").GetString());
            var versionId = operation.GetProperty("documentVersionId").GetGuid();
            using var versionResponse = await client.GetAsync($"/admin/v1/document-versions/{versionId}", TestContext.Current.CancellationToken);
            using var versionJson = JsonDocument.Parse(await versionResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var version = versionJson.RootElement.GetProperty("document");
            Assert.Equal("unpublished", version.GetProperty("publicationState").GetString());
            Assert.Equal(JsonValueKind.Null, version.GetProperty("activeGenerationId").ValueKind);
            Assert.False(version.TryGetProperty("original", out _));
            var generationId = operation.GetProperty("generationId").GetGuid();
            await using var connection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("SELECT d.original,c.location,c.source_spans FROM rag.document_versions d JOIN rag.generations g USING(document_version_id) JOIN rag.chunks c USING(generation_id) WHERE g.generation_id=@id ORDER BY c.ordinal", connection);
            command.Parameters.AddWithValue("id", generationId);
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            var count = 0;
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                count++;
                Assert.Equal(pdf, reader.GetFieldValue<byte[]>(0));
                using var locationJson = JsonDocument.Parse(reader.GetString(1));
                Assert.Empty(locationJson.RootElement.GetProperty("printedLabels").EnumerateArray());
                Assert.Equal(count, locationJson.RootElement.GetProperty("physicalPages")[0].GetInt32());
            }
            Assert.Equal(2, count);
            using var duplicate = await Upload(client, metadata, pdf);
            Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
            using var duplicateJson = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(generationId, duplicateJson.RootElement.GetProperty("operation").GetProperty("generationId").GetGuid());
            Assert.Equal(operation.GetProperty("operationId").GetGuid(), duplicateJson.RootElement.GetProperty("operation").GetProperty("operationId").GetGuid());
            using var different = await Upload(client, metadata, SyntheticPdfFactory.Create("Other synthetic bytes"));
            Assert.Equal(HttpStatusCode.Conflict, different.StatusCode);
            // Reenvios não deixam operações órfãs: só a operação original existe para a versão.
            await using var countConnection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
            await using var operations = new NpgsqlCommand("SELECT count(*) FROM rag.operations WHERE document_version_id=@id", countConnection);
            operations.Parameters.AddWithValue("id", versionId);
            Assert.Equal(1L, await operations.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>Escopo isolado, ausência de JWT e extras de metadados são recusados antes do efeito.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData(null, "knowledge.ingest", 401)]
    [InlineData("organization-admin", "knowledge.ingest", 403)]
    [InlineData("central-admin", "knowledge.read", 403)]
    public async Task UploadRequiresCentralPermission(string? subject, string scope, int expected)
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        using var client = Client(host, subject, scope);
        using var response = await Upload(client, Metadata(), SyntheticPdfFactory.Create("Synthetic text"));
        Assert.Equal(expected, (int)response.StatusCode);
    }

    /// <summary>Rejeita formato falso, tamanho, perfil desconhecido, aplicação ausente e propriedades extras.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("signature", 415)]
    [InlineData("mime", 415)]
    [InlineData("size", 413)]
    [InlineData("profile", 409)]
    [InlineData("applicability", 400)]
    [InlineData("extra", 400)]
    public async Task UploadRejectsInvalidInput(string scenario, int expected)
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        using var client = Client(host);
        var metadata = Metadata();
        var bytes = scenario == "signature" ? "not pdf"u8.ToArray() : scenario == "size" ? new byte[1048577] : SyntheticPdfFactory.Create("Synthetic text");
        if (scenario == "profile")
        {
            metadata = metadata with { ProfileId = Guid.NewGuid() };
        }
        if (scenario == "applicability")
        {
            metadata = metadata with { Applicabilities = [] };
        }
        var json = scenario == "extra" ? JsonSerializer.Serialize(metadata, IngestionMetadata.JsonOptions).TrimEnd('}') + ",\"usuario\":\"spoof\"}" : null;
        using var response = await Upload(client, metadata, bytes, scenario == "mime" ? "text/plain" : "application/pdf", json);
        Assert.Equal(expected, (int)response.StatusCode);
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("1.0", error.RootElement.GetProperty("contractVersion").GetString());
    }

    /// <summary>Sem política de original não há efeito; ingestão não concede auditoria.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task RetentionAndAdministrativeReadAreIndependentlyRequired()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        using var client = Client(host, scopes: "knowledge.ingest");
        host.Services.GetRequiredService<IOptions<RagOptions>>().Value.Retention.OriginalDays = null;
        var metadata = Metadata();
        using var denied = await Upload(client, metadata, SyntheticPdfFactory.Create("Synthetic policy test"));
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        using var error = JsonDocument.Parse(await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("RETENTION_NOT_CONFIGURED", error.RootElement.GetProperty("code").GetString());
        using var read = await client.GetAsync($"/admin/v1/operations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        await using var connection = await database.Source.OpenAsync(TestContext.Current.CancellationToken);
        await using var count = new NpgsqlCommand("SELECT count(*) FROM rag.sources WHERE official_identifier=@id", connection);
        count.Parameters.AddWithValue("id", metadata.OfficialIdentifier);
        Assert.Equal(0L, await count.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Worker persiste falha técnica; caso de uso de leitura exige autorização mesmo fora do HTTP.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task WorkerRecordsTechnicalFailureAndReadUseCaseRequiresPermission()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        await using var application = host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPdfTextExtractor>();
            services.AddSingleton<IPdfTextExtractor, TechnicalFailureExtractor>();
        }));
        using var client = Client(host, application: application);
        using var upload = await Upload(client, Metadata(), SyntheticPdfFactory.Create("Synthetic technical failure"));
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        JsonElement operation;
        do
        {
            await Task.Delay(100, deadline.Token);
            using var status = await client.GetAsync(upload.Headers.Location, deadline.Token);
            status.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await status.Content.ReadAsStringAsync(deadline.Token));
            operation = json.RootElement.GetProperty("operation").Clone();
        } while (operation.GetProperty("state").GetString() != "failed");
        Assert.Equal("TECHNICAL_FAILURE", operation.GetProperty("resultCode").GetString());
        using var scope = application.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<ConsultarIngestao>();
        var unauthorized = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "organization-admin"), new Claim("scope", "knowledge.audit")], "synthetic"));
        var denied = await Assert.ThrowsAsync<Api.RAG.Domain.Knowledge.KnowledgeException>(() => queries.OperationAsync(
            operation.GetProperty("operationId").GetGuid(), unauthorized, TestContext.Current.CancellationToken));
        Assert.Equal("PERMISSION_DENIED", denied.Code);
    }

    private sealed class TechnicalFailureExtractor : IPdfTextExtractor
    {
        public Task<IReadOnlyList<ExtractedPage>> ExtractAsync(Stream content, CancellationToken cancellationToken) =>
            throw new IOException("Synthetic internal failure; not malformed PDF");
    }
}
