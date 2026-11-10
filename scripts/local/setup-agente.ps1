<#
.SYNOPSIS
    Prepara o ambiente local da Api.Agente no mesmo PostgreSQL do Docker usado pela Api.RAG:
    banco `agente`, papéis distintos de migração/aplicação, User Secrets e migração.
    Idempotente; executar depois de scripts/local/setup-rag.ps1.
#>
[CmdletBinding()]
param(
    [switch]$SkipMigration
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$infra = Join-Path $root 'infra\local'
$envFile = Join-Path $infra '.env'
$project = Join-Path $root 'src\Api.Agente'
if (-not (Test-Path -LiteralPath $envFile)) { throw 'infra/local/.env não existe: execute scripts/local/setup-rag.ps1 primeiro.' }

function New-Secret { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)).ToLowerInvariant() }
$settings = @{}
Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^\s*([A-Z_]+)=(.*)$' } |
    ForEach-Object { $settings[$Matches[1]] = $Matches[2].Trim() }
foreach ($name in 'AGENTE_MIGRATION_PASSWORD', 'AGENTE_APPLICATION_PASSWORD') {
    if (-not $settings.ContainsKey($name)) {
        $settings[$name] = New-Secret
        Add-Content -LiteralPath $envFile -Value "$name=$($settings[$name])" -Encoding utf8NoBOM
        Write-Host "Senha $name gerada em $envFile"
    }
}

$appSettings = Join-Path $project 'appsettings.Development.json'
if (-not (Test-Path -LiteralPath $appSettings)) {
    Copy-Item -LiteralPath (Join-Path $project 'appsettings.Development.Example.json') -Destination $appSettings
}

docker compose --project-directory $infra -f (Join-Path $infra 'docker-compose.yml') up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar os containers.' }

# Papéis e banco próprios da Api.Agente; senhas passam como variáveis do psql, sem interpolação no SQL.
$sql = @'
SELECT format('CREATE ROLE agente_migration LOGIN PASSWORD %L', :'mig') WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='agente_migration') \gexec
SELECT format('ALTER ROLE agente_migration PASSWORD %L', :'mig') \gexec
SELECT format('CREATE ROLE agente_application LOGIN PASSWORD %L', :'app') WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='agente_application') \gexec
SELECT format('ALTER ROLE agente_application PASSWORD %L', :'app') \gexec
SELECT 'CREATE DATABASE agente OWNER agente_migration' WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname='agente') \gexec
'@
$sql | docker exec -i agente-sped-rag-rag-db-1 psql -v ON_ERROR_STOP=1 -q -U rag_migration -d postgres `
    -v "mig=$($settings['AGENTE_MIGRATION_PASSWORD'])" -v "app=$($settings['AGENTE_APPLICATION_PASSWORD'])" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar banco/papéis da Api.Agente.' }
Write-Host 'Banco agente e papéis agente_migration/agente_application prontos.'

$base = "Host=127.0.0.1;Port=$($settings['RAG_DB_PORT']);Database=agente"
dotnet user-secrets set 'AgentData:ConnectionString' "$base;Username=agente_application;Password=$($settings['AGENTE_APPLICATION_PASSWORD'])" --project $project | Out-Null
dotnet user-secrets set 'AgentData:MigrationConnectionString' "$base;Username=agente_migration;Password=$($settings['AGENTE_MIGRATION_PASSWORD'])" --project $project | Out-Null
Write-Host 'User Secrets AgentData:ConnectionString e AgentData:MigrationConnectionString configurados.'

if (-not $SkipMigration) {
    dotnet run --project $project --no-launch-profile -- --environment Development --migrate-agent
    if ($LASTEXITCODE -ne 0) { throw 'Falha na migração da Api.Agente.' }
    Write-Host 'Migração da Api.Agente aplicada.'
}
Write-Host 'Pendentes manuais: Llm:ApiKey (User Secrets da Api.Agente) e certificado de desenvolvimento confiável.'
