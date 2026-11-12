# Api.RAG no Render (Web Service Docker) — feature 004, skill sofia-devops-free-cloud.
# Render: Dockerfile Path = src/Api.RAG/Dockerfile, Docker Build Context = raiz do repositório.
# Inclui os workers de ingestão/embeddings (BackgroundService): exige processo ativo, não serverless.
# global.json não é copiado (fixa o SDK 10.0.3xx local); pacotes vêm do packages.lock.json.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY src/Api.RAG/Api.RAG.csproj src/Api.RAG/packages.lock.json src/Api.RAG/
RUN dotnet restore src/Api.RAG/Api.RAG.csproj --locked-mode
# Contrato MCP versionado na spec 001, embutido como recurso.
COPY specs/001-base-fiscal-versionada/contracts/mcp-tools.schema.json specs/001-base-fiscal-versionada/contracts/
COPY src/Api.RAG/ src/Api.RAG/
RUN dotnet publish src/Api.RAG/Api.RAG.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Npgsql carrega GSSAPI ao conectar; sem a biblioteca registra erro no log.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    APP_DLL=Api.RAG.dll
USER $APP_UID
# PORT vem da plataforma (Render define 10000 por padrão); 8080 só como fallback local.
# Migrações não rodam no boot: usar "dotnet Api.RAG.dll --migrate" com a conexão de migração (ver specs/004).
CMD ["sh", "-c", "export ASPNETCORE_URLS=\"http://0.0.0.0:${PORT:-8080}\"; exec dotnet \"$APP_DLL\""]
