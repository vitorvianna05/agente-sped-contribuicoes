<#
.SYNOPSIS
    Mede a relevância da busca da Api.RAG (BuscarConhecimento via MCP) contra um conjunto de
    consultas com páginas esperadas. Requer a Api.RAG em execução (Development, modo local).
.DESCRIPTION
    Cada consulta gera um embedding na OpenAI (custo desprezível). Métricas: hit@k (alguma página
    esperada entre os resultados) e MRR (inverso da posição do primeiro acerto).
.EXAMPLE
    ./scripts/local/avaliar-busca.ps1
    ./scripts/local/avaliar-busca.ps1 -Detalhar
#>
[CmdletBinding()]
param(
    [string]$Conjunto = (Join-Path $PSScriptRoot '..\..\specs\001-base-fiscal-versionada\evaluation\busca-guia-1.35.json'),
    [string]$Endpoint = 'https://localhost:60994/mcp',
    [switch]$Detalhar
)
$ErrorActionPreference = 'Stop'
$set = Get-Content -Raw -LiteralPath $Conjunto | ConvertFrom-Json
$client = [System.Net.Http.HttpClient]::new()
$id = 0

function Invoke-Search([string]$query) {
    $script:id++
    $body = @{ jsonrpc = '2.0'; id = $script:id; method = 'tools/call'; params = @{ name = 'BuscarConhecimento'; arguments = @{
        consulta = $query; tipoDocumento = $set.tipoDocumento; versaoLayout = $set.versaoLayout } } } | ConvertTo-Json -Depth 6 -Compress
    $request = [System.Net.Http.HttpRequestMessage]::new('Post', $Endpoint)
    $request.Content = [System.Net.Http.StringContent]::new($body, [Text.Encoding]::UTF8, 'application/json')
    $request.Headers.Accept.ParseAdd('application/json')
    $request.Headers.Accept.ParseAdd('text/event-stream')
    $response = $client.Send($request)
    $text = $response.Content.ReadAsStringAsync().Result
    if (-not $response.IsSuccessStatusCode) { throw "HTTP $([int]$response.StatusCode): $text" }
    # Streamable HTTP pode responder em SSE; a mensagem JSON-RPC fica nas linhas data:.
    $json = if ($text.TrimStart().StartsWith('{')) { $text } else { (($text -split "`n") | Where-Object { $_ -like 'data:*' } | ForEach-Object { $_.Substring(5).Trim() }) -join '' }
    $rpc = $json | ConvertFrom-Json
    if ($rpc.error) { throw "MCP: $($rpc.error.message)" }
    $rpc.result.structuredContent
}

$rows = foreach ($case in $set.cases) {
    $result = Invoke-Search $case.query
    $pages = @($result.evidences | ForEach-Object { [int]($_.location.physicalPages | Select-Object -First 1) })
    $rank = 0
    for ($i = 0; $i -lt $pages.Count; $i++) { if ($case.expectedPages -contains $pages[$i]) { $rank = $i + 1; break } }
    [pscustomobject]@{
        Caso = $case.id
        Acerto = $rank -gt 0
        Posicao = if ($rank) { $rank } else { '-' }
        Paginas = ($pages -join ',')
        Scores = (($result.evidences | ForEach-Object { [math]::Round([double]$_.score, 3) }) -join ',')
        RR = if ($rank) { 1.0 / $rank } else { 0.0 }
    }
}
$client.Dispose()
if ($Detalhar) { $rows | Format-Table -AutoSize | Out-String -Width 220 | Write-Output }
$hits = @($rows | Where-Object Acerto).Count
"hit@k: $hits/$($rows.Count) ({0:P0})  MRR: {1:N3}" -f ($hits / $rows.Count), (($rows | Measure-Object RR -Average).Average)
