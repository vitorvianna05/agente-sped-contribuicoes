<#
.SYNOPSIS
    Envia um PDF para POST /api/v1/ingest da Api.RAG local e acompanha a operação.
.DESCRIPTION
    Requer a API em execução (Development, autenticação local por loopback). Sem -PdfPath,
    gera um PDF sintético de duas páginas. Os metadados usam o perfil de desenvolvimento
    configurado em appsettings.Development.json.
.EXAMPLE
    ./scripts/local/ingest-sample.ps1
    ./scripts/local/ingest-sample.ps1 -PdfPath C:\docs\guia-pratico.pdf -OfficialIdentifier guia-efd-contribuicoes -OfficialVersion 1.35
#>
[CmdletBinding()]
param(
    [string]$PdfPath,
    [string]$BaseUrl = 'https://localhost:60994',
    [string]$ProfileId = 'd0c0a1e0-0000-4000-8000-000000000005',
    [string]$Issuer = 'RFB',
    [string]$OfficialIdentifier = 'documento-sintetico-local',
    [string]$Title = 'Documento sintético de desenvolvimento',
    [ValidateSet('manual', 'guide')][string]$Kind = 'guide',
    [string]$OriginUri = 'https://example.com/documento-sintetico-local.pdf',
    [string]$OfficialVersion = (Get-Date -Format 'yyyyMMddHHmmss'),
    [int]$PollSeconds = 60,
    # Use se o certificado de desenvolvimento não for confiável (dotnet dev-certs https --trust).
    [switch]$SkipCertificateCheck
)
$ErrorActionPreference = 'Stop'

function New-SyntheticPdf([string[]]$Pages) {
    # PDF mínimo válido (Helvetica, uma linha de texto por página) com tabela xref correta.
    $objects = [System.Collections.Generic.List[string]]::new()
    $kids = (0..($Pages.Count - 1) | ForEach-Object { "$(4 + 2 * $_) 0 R" }) -join ' '
    $objects.Add('<< /Type /Catalog /Pages 2 0 R >>')
    $objects.Add("<< /Type /Pages /Kids [$kids] /Count $($Pages.Count) >>")
    $objects.Add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>')
    for ($i = 0; $i -lt $Pages.Count; $i++) {
        $text = $Pages[$i] -replace '([\\()])', '\$1'
        $stream = "BT /F1 12 Tf 40 780 Td ($text) Tj ET"
        $objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents $(5 + 2 * $i) 0 R >>")
        $objects.Add("<< /Length $($stream.Length) >>`nstream`n$stream`nendstream")
    }
    $builder = [System.Text.StringBuilder]::new("%PDF-1.4`n")
    $offsets = foreach ($n in 1..$objects.Count) {
        $builder.Length
        [void]$builder.Append("$n 0 obj`n$($objects[$n - 1])`nendobj`n")
    }
    $xref = $builder.Length
    [void]$builder.Append("xref`n0 $($objects.Count + 1)`n0000000000 65535 f `n")
    foreach ($offset in $offsets) { [void]$builder.Append(('{0:D10} 00000 n `n' -f $offset)) }
    [void]$builder.Append("trailer`n<< /Size $($objects.Count + 1) /Root 1 0 R >>`nstartxref`n$xref`n%%EOF`n")
    [System.Text.Encoding]::ASCII.GetBytes($builder.ToString())
}

$bytes = if ($PdfPath) { [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $PdfPath)) } else {
    New-SyntheticPdf @(
        'Registro 0000 - Abertura do arquivo digital e identificacao da pessoa juridica.',
        'Registro M200 - Consolidacao da contribuicao para o PIS/Pasep do periodo.'
    )
}
$fileName = if ($PdfPath) { Split-Path -Leaf $PdfPath } else { 'sintetico.pdf' }

$metadata = [ordered]@{
    contractVersion    = '1.0'
    issuer             = $Issuer
    officialIdentifier = $OfficialIdentifier
    title              = $Title
    kind               = $Kind
    originUri          = $OriginUri
    officialVersion    = $OfficialVersion
    profileId          = $ProfileId
    applicabilities    = @([ordered]@{ documentType = 'EFD-Contribuicoes'; layoutVersion = '1.35'; officialBasis = 'Desenvolvimento local' })
} | ConvertTo-Json -Depth 5 -Compress

$handler = [System.Net.Http.HttpClientHandler]::new()
if ($SkipCertificateCheck) {
    # Somente para o certificado de desenvolvimento não confiável em localhost.
    if (([Uri]$BaseUrl).Host -notin 'localhost', '127.0.0.1', '[::1]') { throw '-SkipCertificateCheck só é permitido em localhost.' }
    $handler.ServerCertificateCustomValidationCallback = [System.Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
}
$client = [System.Net.Http.HttpClient]::new($handler)
try {
    $form = [System.Net.Http.MultipartFormDataContent]::new()
    $file = [System.Net.Http.ByteArrayContent]::new($bytes)
    $file.Headers.ContentType = 'application/pdf'
    $form.Add($file, 'arquivo', $fileName)
    $form.Add([System.Net.Http.StringContent]::new($metadata, [Text.Encoding]::UTF8, 'application/json'), 'metadata')
    $request = [System.Net.Http.HttpRequestMessage]::new('Post', "$BaseUrl/api/v1/ingest")
    $request.Headers.Add('Idempotency-Key', [guid]::NewGuid().ToString())
    $request.Content = $form
    $response = $client.Send($request)
    $body = $response.Content.ReadAsStringAsync().Result
    Write-Host "POST /api/v1/ingest -> $([int]$response.StatusCode)"
    if (-not $response.IsSuccessStatusCode) { Write-Host $body; exit 1 }
    $result = $body | ConvertFrom-Json
    $location = $result.location
    Write-Host "Operação $($result.operation.operationId) ($($result.operation.state)); acompanhando $location"

    $deadline = (Get-Date).AddSeconds($PollSeconds)
    do {
        Start-Sleep -Seconds 2
        $status = $client.GetStringAsync("$BaseUrl$location").Result | ConvertFrom-Json
        $operation = $status.operation
        Write-Host "  estado=$($operation.state) resultado=$($operation.resultCode)"
        # Sem etapa vetorial ativa, a preparação textual termina em ready/EMBEDDINGS_PENDING.
        $textOnlyDone = $operation.preparationState -eq 'ready' -and $operation.resultCode -eq 'EMBEDDINGS_PENDING'
    } while ($operation.state -in 'queued', 'running', 'processing' -and -not $textOnlyDone -and (Get-Date) -lt $deadline)

    $operation | ConvertTo-Json -Depth 6
    if ($operation.documentVersionId) {
        $document = $client.GetStringAsync("$BaseUrl/admin/v1/document-versions/$($operation.documentVersionId)").Result | ConvertFrom-Json
        $document.document | ConvertTo-Json -Depth 6
    }
}
finally {
    $client.Dispose()
}
