<#
.SYNOPSIS
    Prepara o ambiente local da Api.RAG: credenciais, containers (pgvector + pgAdmin),
    User Secrets e migrações. Idempotente; não sobrescreve infra/local/.env existente.
#>
[CmdletBinding()]
param(
    [switch]$SkipMigration
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$infra = Join-Path $root 'infra\local'
$envFile = Join-Path $infra '.env'
$project = Join-Path $root 'src\Api.RAG'

function New-Secret { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)).ToLowerInvariant() }

if (-not (Test-Path -LiteralPath $envFile)) {
    @(
        'RAG_DB_PORT=5433'
        "RAG_MIGRATION_PASSWORD=$(New-Secret)"
        "RAG_APPLICATION_PASSWORD=$(New-Secret)"
        'PGADMIN_PORT=5050'
        'PGADMIN_EMAIL=admin@local.test'
        "PGADMIN_PASSWORD=$(New-Secret)"
    ) | Set-Content -LiteralPath $envFile -Encoding utf8NoBOM
    Write-Host "Credenciais locais geradas em $envFile"
}
$settings = @{}
Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^\s*([A-Z_]+)=(.*)$' } |
    ForEach-Object { $settings[$Matches[1]] = $Matches[2].Trim() }

$appSettings = Join-Path $project 'appsettings.Development.json'
if (-not (Test-Path -LiteralPath $appSettings)) {
    Copy-Item -LiteralPath (Join-Path $project 'appsettings.Development.Example.json') -Destination $appSettings
}

docker compose --project-directory $infra -f (Join-Path $infra 'docker-compose.yml') up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar os containers.' }

$port = $settings['RAG_DB_PORT']
$base = "Host=127.0.0.1;Port=$port;Database=rag"
dotnet user-secrets set 'ConnectionStrings:Rag' "$base;Username=rag_application;Password=$($settings['RAG_APPLICATION_PASSWORD'])" --project $project | Out-Null
dotnet user-secrets set 'ConnectionStrings:RagMigration' "$base;Username=rag_migration;Password=$($settings['RAG_MIGRATION_PASSWORD'])" --project $project | Out-Null
Write-Host 'User Secrets ConnectionStrings:Rag e ConnectionStrings:RagMigration configurados.'

if (-not $SkipMigration) {
    dotnet run --project $project --no-launch-profile -- --environment Development --migrate
    if ($LASTEXITCODE -ne 0) { throw 'Falha na migração.' }
    Write-Host 'Migrações aplicadas.'
}

Write-Host "pgAdmin: http://localhost:$($settings['PGADMIN_PORT'])  (servidor 'Api.RAG (local)'; senha do banco = RAG_MIGRATION_PASSWORD em infra/local/.env)"
