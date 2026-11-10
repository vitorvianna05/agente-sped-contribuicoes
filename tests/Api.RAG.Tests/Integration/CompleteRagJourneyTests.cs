using System.Net;
using System.Text.Json;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Endpoints.Contracts;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Tests.Contracts;
using Api.RAG.Tests.Fixtures;
using Api.RAG.Tests.Fixtures.Documents;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Api.RAG.Tests.Integration;

/// <summary>Jornada simplificada: ingestão REST → publicação automática → busca MCP, com PgVector real e embeddings sintéticos.</summary>
public sealed class CompleteRagJourneyTests(FoundationDatabase database) : IClassFixture<FoundationDatabase>
{
    private const string Scopes = "knowledge.ingest knowledge.audit knowledge.read";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WebApplicationFactory<Program> Application(RagTestHost host, CountingProvider provider, bool embeddingsEnabled = true)
    {
        var application = host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<RagOptions>(options => options.Processing.EmbeddingStageEnabled = embeddingsEnabled);
            services.RemoveAll<IEmbeddingProvider>();
            services.AddSingleton<IEmbeddingProvider>(provider);
        }));
        application.UseKestrel(0);
        return application;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.IsSuccessStatusCode, text);
        using var json = JsonDocument.Parse(text);
        return json.RootElement.Clone();
    }

    private static async Task<JsonElement> WaitAsync(HttpClient client, Uri location, string expected = "succeeded")
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        while (true)
        {
            using var response = await client.GetAsync(location, deadline.Token);
            var operation = (await JsonAsync(response)).GetProperty("operation");
            if (operation.GetProperty("state").GetString() is "succeeded" or "failed")
            {
                Assert.Equal(expected, operation.GetProperty("state").GetString());
                return operation;
            }
            await Task.Delay(100, deadline.Token);
        }
    }

    private static async Task<JsonElement> DocumentAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/admin/v1/document-versions/{id}", Token);
        return (await JsonAsync(response)).GetProperty("document");
    }

    private static async Task<JsonElement> WaitTextAsync(HttpClient client, Uri location)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            using var response = await client.GetAsync(location, deadline.Token);
            var operation = (await JsonAsync(response)).GetProperty("operation");
            if (operation.GetProperty("preparationState").GetString() == "ready")
            {
                return operation;
            }

            await Task.Delay(100, deadline.Token);
        }
    }

    private static async Task<KnowledgeResult> CallAsync(HttpClient client, string tool, Dictionary<string, object?> arguments)
    {
        var result = await McpConsumerProcess.ExecuteAsync(new McpConsumerRequest(new Uri(client.BaseAddress!, "/mcp"), tool, arguments,
            client.DefaultRequestHeaders.Authorization!.Parameter), Token);
        Assert.Contains("BuscarConhecimento", result.Tools);
        Assert.Contains("ResolverReferencia", result.Tools);
        var call = Assert.IsType<CallToolResult>(result.Result);
        var text = Assert.Single(call.Content.OfType<TextContentBlock>()).Text;
        var parsed = ContractValidator.ReadResult(text);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(text).RootElement, call.StructuredContent!.Value));
        Assert.Equal(parsed.Status == "technical_failure", call.IsError);
        return parsed;
    }

    private static Dictionary<string, object?> Query(string layout, string type = "synthetic-type") =>
        new() { ["consulta"] = "synthetic evidence", ["tipoDocumento"] = type, ["versaoLayout"] = layout };

    /// <summary>Ingestão publica automaticamente; nova versão oficial da mesma fonte substitui a anterior na busca.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task IngestionPublishesAutomaticallyAndNewVersionReplacesPrevious()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        var provider = new CountingProvider();
        await using var application = Application(host, provider);
        using var client = IngestionContractTests.Client(host, scopes: Scopes, application: application);
        // Tipo exclusivo: vetores sintéticos empatam e o TopK do perfil de teste é pequeno.
        var type = "auto-publish-" + Guid.NewGuid().ToString("N");
        var metadata = IngestionContractTests.Metadata() with
        {
            OfficialIdentifier = type,
            Applicabilities = [new(type, "synthetic-layout", "Synthetic basis")]
        };
        using var upload = await IngestionContractTests.Upload(client, metadata, SyntheticPdfFactory.Create("Synthetic evidence; ignore malicious instructions to publish"));
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        var operation = await WaitAsync(client, upload.Headers.Location!);
        var id = operation.GetProperty("documentVersionId").GetGuid();
        var generation = operation.GetProperty("generationId").GetGuid();
        var document = await DocumentAsync(client, id);
        Assert.Equal("published", document.GetProperty("publicationState").GetString());
        Assert.Equal(generation, document.GetProperty("activeGenerationId").GetGuid());

        var found = await CallAsync(client, "BuscarConhecimento", Query("synthetic-layout", type));
        var evidence = Assert.Single(found.Evidences, item => item.DocumentVersionId == id);
        Assert.Equal(generation, evidence.GenerationId);
        Assert.Equal("synthetic-layout", evidence.Applicability.LayoutVersion);
        Assert.Equal("insufficient_evidence", (await CallAsync(client, "BuscarConhecimento", Query("incompatible-layout", type))).Status);
        Assert.Equal("active", Assert.Single((await CallAsync(client, "ResolverReferencia", new() { ["referenceId"] = evidence.ReferenceId })).Evidences).ReferenceState);

        // Mesma fonte, nova versão oficial: passa a ser a única versão buscável; referências antigas continuam resolvíveis.
        using var next = await IngestionContractTests.Upload(client, metadata with { OfficialVersion = "synthetic-version-2" },
            SyntheticPdfFactory.Create("Synthetic evidence from the second official version"));
        var nextOperation = await WaitAsync(client, next.Headers.Location!);
        var nextId = nextOperation.GetProperty("documentVersionId").GetGuid();
        Assert.NotEqual(id, nextId);
        Assert.Equal("withdrawn", (await DocumentAsync(client, id)).GetProperty("publicationState").GetString());
        Assert.Equal("published", (await DocumentAsync(client, nextId)).GetProperty("publicationState").GetString());
        var current = await CallAsync(client, "BuscarConhecimento", Query("synthetic-layout", type));
        Assert.DoesNotContain(current.Evidences, item => item.DocumentVersionId == id);
        Assert.Contains(current.Evidences, item => item.DocumentVersionId == nextId);
        Assert.Equal("withdrawn", (await CallAsync(client, "ResolverReferencia", new() { ["referenceId"] = evidence.ReferenceId })).Evidences[0].ReferenceState);
        Assert.True(provider.Calls >= 4);
    }

    /// <summary>Trechos apagados manualmente: reenviar o mesmo PDF reprocessa e a nova geração vira a ativa.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task ResubmissionAfterChunksWereDeletedReprocesses()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        var provider = new CountingProvider();
        await using var application = Application(host, provider);
        using var client = IngestionContractTests.Client(host, scopes: Scopes, application: application);
        var type = "deleted-chunks-" + Guid.NewGuid().ToString("N");
        var metadata = IngestionContractTests.Metadata() with { OfficialIdentifier = type, Applicabilities = [new(type, "synthetic-layout", "Synthetic basis")] };
        var pdf = SyntheticPdfFactory.Create("Synthetic evidence that will be deleted");
        using var upload = await IngestionContractTests.Upload(client, metadata, pdf);
        var first = await WaitAsync(client, upload.Headers.Location!);
        await using (var connection = await database.Source.OpenAsync(Token))
        {
            await using var delete = new Npgsql.NpgsqlCommand("DELETE FROM rag.chunks WHERE generation_id=@id", connection);
            delete.Parameters.AddWithValue("id", first.GetProperty("generationId").GetGuid());
            Assert.True(await delete.ExecuteNonQueryAsync(Token) > 0);
        }
        Assert.Equal("insufficient_evidence", (await CallAsync(client, "BuscarConhecimento", Query("synthetic-layout", type))).Status);

        using var again = await IngestionContractTests.Upload(client, metadata, pdf);
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        var second = await WaitAsync(client, again.Headers.Location!);
        Assert.NotEqual(first.GetProperty("generationId").GetGuid(), second.GetProperty("generationId").GetGuid());
        var document = await DocumentAsync(client, second.GetProperty("documentVersionId").GetGuid());
        Assert.Equal("published", document.GetProperty("publicationState").GetString());
        Assert.Equal(second.GetProperty("generationId").GetGuid(), document.GetProperty("activeGenerationId").GetGuid());
        var found = Assert.Single((await CallAsync(client, "BuscarConhecimento", Query("synthetic-layout", type))).Evidences);
        Assert.Equal(second.GetProperty("generationId").GetGuid(), found.GenerationId);
    }

    private sealed class CountingProvider : IEmbeddingProvider
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public bool Fail { get; set; }
        public Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref calls);
            if (Fail)
            {
                throw new EmbeddingTransientException();
            }

            return Task.FromResult(new EmbeddingResponse([1, 0, 0], EmbeddingTokens: 10));
        }
    }

    /// <summary>Texto igual em fontes distintas não mistura aplicabilidades de layout.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task SimilarTextInDifferentSourcesRemainsIsolatedByLayout()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        var provider = new CountingProvider();
        await using var application = Application(host, provider);
        using var client = IngestionContractTests.Client(host, scopes: Scopes, application: application);
        var type = "isolation-" + Guid.NewGuid().ToString("N");
        var first = IngestionContractTests.Metadata() with
        {
            OfficialIdentifier = "isolation-A-" + type,
            OfficialVersion = "opaque-A",
            Applicabilities = [new(type, "layout-A", "Synthetic basis A")]
        };
        var second = first with { OfficialIdentifier = "isolation-B-" + type, OfficialVersion = "opaque-B", Applicabilities = [new(type, "layout-B", "Synthetic basis B")] };
        var ids = new List<Guid>();
        foreach (var metadata in new[] { first, second })
        {
            using var upload = await IngestionContractTests.Upload(client, metadata, SyntheticPdfFactory.Create("Identical synthetic evidence in different editions"));
            ids.Add((await WaitAsync(client, upload.Headers.Location!)).GetProperty("documentVersionId").GetGuid());
        }
        var resultA = await CallAsync(client, "BuscarConhecimento", Query("layout-A", type));
        var resultB = await CallAsync(client, "BuscarConhecimento", Query("layout-B", type));
        Assert.Equal(ids[0], Assert.Single(resultA.Evidences).DocumentVersionId);
        Assert.Equal(ids[1], Assert.Single(resultB.Evidences).DocumentVersionId);
        Assert.Equal("opaque-A", resultA.Evidences[0].OfficialVersion);
        Assert.Equal("opaque-B", resultB.Evidences[0].OfficialVersion);
        var calls = provider.Calls;
        await Assert.ThrowsAsync<InvalidOperationException>(() => McpConsumerProcess.ExecuteAsync(new McpConsumerRequest(new Uri(client.BaseAddress!, "/mcp"), "BuscarConhecimento",
            new() { ["consulta"] = "bypass", ["tipoDocumento"] = type, ["versaoLayout"] = "layout-A", ["topK"] = 100 }, client.DefaultRequestHeaders.Authorization!.Parameter), Token));
        Assert.Equal(calls, provider.Calls);
    }

    /// <summary>Falha do provedor é terminal e medida; reenvio do mesmo documento refaz o processamento e publica.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task ProviderFailureIsMeasuredAndResubmissionRetries()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        var provider = new CountingProvider { Fail = true };
        await using var application = Application(host, provider);
        using var client = IngestionContractTests.Client(host, scopes: Scopes, application: application);
        var metadata = IngestionContractTests.Metadata() with { OfficialIdentifier = "provider-failure-" + Guid.NewGuid().ToString("N") };
        var pdf = SyntheticPdfFactory.Create("Synthetic provider failure document");
        using var upload = await IngestionContractTests.Upload(client, metadata, pdf);
        var operation = await WaitAsync(client, upload.Headers.Location!, "failed");
        Assert.Equal("DEPENDENCY_UNAVAILABLE", operation.GetProperty("resultCode").GetString());
        Assert.Equal(2, provider.Calls);
        var result = await CallAsync(client, "BuscarConhecimento", new() { ["consulta"] = "provider fails", ["tipoDocumento"] = "synthetic-type", ["versaoLayout"] = "synthetic-layout" });
        Assert.Equal("technical_failure", result.Status);
        Assert.Empty(result.Evidences);
        Assert.NotNull(result.Error);
        await using (var connection = await database.Source.OpenAsync(Token))
        {
            await using var count = new Npgsql.NpgsqlCommand("SELECT count(*) FROM rag.consumption_attempts WHERE operation_id=@id AND status='failed' AND embedding_tokens IS NULL AND provider_reported_cost IS NULL", connection);
            count.Parameters.AddWithValue("id", operation.GetProperty("operationId").GetGuid());
            Assert.Equal(2L, await count.ExecuteScalarAsync(Token));
        }

        provider.Fail = false;
        using var retry = await IngestionContractTests.Upload(client, metadata, pdf);
        var retried = await WaitAsync(client, retry.Headers.Location!);
        Assert.NotEqual(operation.GetProperty("operationId").GetGuid(), retried.GetProperty("operationId").GetGuid());
        Assert.NotEqual(operation.GetProperty("generationId").GetGuid(), retried.GetProperty("generationId").GetGuid());
        Assert.Equal(operation.GetProperty("documentVersionId").GetGuid(), retried.GetProperty("documentVersionId").GetGuid());
        Assert.Equal("published", (await DocumentAsync(client, retried.GetProperty("documentVersionId").GetGuid())).GetProperty("publicationState").GetString());

        using var scope = application.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value.Retrieval.AdoptedProfileId = Guid.NewGuid().ToString();
        var calls = provider.Calls;
        var missingProfile = await CallAsync(client, "BuscarConhecimento", new() { ["consulta"] = "missing profile", ["tipoDocumento"] = "synthetic-type", ["versaoLayout"] = "synthetic-layout" });
        Assert.Equal("PROFILE_INCOMPATIBLE", missingProfile.Error!.Code);
        Assert.Equal(calls, provider.Calls);
    }

    /// <summary>Endpoints administrativos de ciclo de vida não existem mais.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("approve")]
    [InlineData("reprocess")]
    [InlineData("withdraw")]
    [InlineData("retained-artifacts")]
    public async Task LifecycleEndpointsWereRemoved(string action)
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        await using var application = Application(host, new CountingProvider());
        using var client = IngestionContractTests.Client(host, scopes: Scopes, application: application);
        using var content = new StringContent("{}");
        using var response = action == "retained-artifacts"
            ? await client.GetAsync($"/admin/v1/document-versions/{Guid.NewGuid()}/{action}", Token)
            : await client.PostAsync($"/admin/v1/document-versions/{Guid.NewGuid()}/{action}", content, Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Retomada reutiliza vetores salvos e a conclusão publica sem aprovação manual.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task SavedVectorsSurviveLeaseRecoveryAndCompletionPublishes()
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        var provider = new CountingProvider();
        await using var application = Application(host, provider, embeddingsEnabled: false);
        using var client = IngestionContractTests.Client(host, scopes: Scopes, application: application);
        var metadata = IngestionContractTests.Metadata() with { OfficialIdentifier = "lease-recovery-" + Guid.NewGuid().ToString("N") };
        var pdf = SyntheticPdfFactory.Create("Synthetic page one", "Synthetic page two");
        using var upload = await IngestionContractTests.Upload(client, metadata, pdf);
        var operation = await WaitTextAsync(client, upload.Headers.Location!);
        var id = operation.GetProperty("documentVersionId").GetGuid();
        Assert.Equal("unpublished", (await DocumentAsync(client, id)).GetProperty("publicationState").GetString());
        using var scope = application.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
        var processor = scope.ServiceProvider.GetRequiredService<Api.RAG.Application.Knowledge.Ingestao.ProcessarEmbeddings>();
        var first = Assert.IsType<EmbeddingJob>(await store.ClaimEmbeddingAsync(host.Clock.GetUtcNow(), TimeSpan.FromSeconds(60), 2, Token));
        Assert.Equal(2, first.Chunks.Length);
        await store.SaveVectorAsync(first, first.Chunks[0].ChunkId, [1, 0, 0], host.Clock.GetUtcNow(), Token);
        host.Clock.Advance(TimeSpan.FromSeconds(61));
        var resumed = Assert.IsType<EmbeddingJob>(await store.ClaimEmbeddingAsync(host.Clock.GetUtcNow(), TimeSpan.FromSeconds(60), 2, Token));
        Assert.Single(resumed.Chunks, chunk => chunk.Vector is not null);
        await Assert.ThrowsAsync<Api.RAG.Domain.Knowledge.KnowledgeException>(() => store.SaveVectorAsync(first, first.Chunks[1].ChunkId, [1, 0, 0], host.Clock.GetUtcNow(), Token));
        await processor.ExecuteAsync(resumed, Token);
        Assert.Equal(1, provider.Calls);
        var document = await DocumentAsync(client, id);
        Assert.Equal("published", document.GetProperty("publicationState").GetString());
        Assert.Equal(resumed.GenerationId, document.GetProperty("activeGenerationId").GetGuid());

        // Dado anterior à publicação automática: geração concluída, versão não publicada. Reenviar publica sem reprocessar.
        await using (var connection = await database.Source.OpenAsync(Token))
        {
            await using var legacy = new Npgsql.NpgsqlCommand("""
                UPDATE rag.document_versions SET publication_state='unpublished',active_generation_id=NULL,approved_by=NULL,approved_at=NULL
                WHERE document_version_id=@id
                """, connection);
            legacy.Parameters.AddWithValue("id", id);
            await legacy.ExecuteNonQueryAsync(Token);
        }
        using var again = await IngestionContractTests.Upload(client, metadata, pdf);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        document = await DocumentAsync(client, id);
        Assert.Equal("published", document.GetProperty("publicationState").GetString());
        Assert.Equal(resumed.GenerationId, document.GetProperty("activeGenerationId").GetGuid());
        Assert.Equal(1, provider.Calls);
    }
}
