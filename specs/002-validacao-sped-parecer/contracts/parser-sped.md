# Leitura estrutural SPED — contrato v1

POST `/api/v1/sped/leitura` na Api.Agente. Permissão analysis.execute; HTTPS e controles do
serviço. Recebe o arquivo texto diretamente no corpo, sem JSON, caminho local ou multipart.
Tipos: text/plain (charset omitido ou ISO-8859-1/latin1/latin-1) e application/octet-stream.
Charset UTF-8 ou BOM incompatível não é convertido silenciosamente. Campos não são CSV.

## Resultado HTTP 200

- contractVersion=1.0, executionId/correlationId do sistema.
- status=partial quando a leitura estrutural não tem ocorrências; status=failed se há ocorrências.
- readingStatus=completed ou invalid, independente do parecer/validação fiscal.
- fiscalValidationStatus=not_performed em todos os casos deste incremento.
- document: encoding=iso-8859-1, sha256 dos bytes originais, bytesRead, physicalLineCount,
  parsedRecordCount, rejectedLineCount, declaredLayoutCode nullable, records e issues.
- Cada record: lineNumber, code, fields (exclui REG), rawLine (sem terminador) e lineEnding
  (CRLF/LF/EOF). Ordem e vazios são preservados; nomes/tipos fiscais de campos não são inferidos.
- Cada issue: lineNumber e code sem reproduzir valores. Linhas sintaticamente ilegíveis
  não aparecem como registros; rejectedLineCount explicita a quantidade descartada.
- checksNotPerformed: compatibilidade do leiaute, schemas dos campos/registros, hierarquia
  dos blocos e regras fiscais. Código declarado não é comprovação de suporte.
- Cache-Control:no-store; sem persistência, MCP ou IA. A resposta contém dados fiscais do
  próprio upload e não deve ser registrada por clientes em logs públicos.

Ocorrências incluem CRLF_REQUIRED, EMPTY_LINE, INVALID_RECORD_DELIMITERS, INVALID_RECORD_CODE,
CONTROL_CHARACTER_NOT_ALLOWED, OPENING_RECORD_MISSING/DUPLICATED/NOT_FIRST, CLOSING_RECORD_MISSING/
NOT_LAST, DECLARED_LAYOUT_CODE_INVALID e DECLARED_LINE_COUNT_INVALID/MISMATCH.

## Erros HTTP (ProblemDetails)

- 401/403: autenticação/permissão/transporte.
- 413: REQUEST_TOO_LARGE, SPED_FILE_TOO_LARGE, SPED_LINE_TOO_LONG,
  SPED_RECORD_LIMIT_EXCEEDED, SPED_FIELD_LIMIT_EXCEEDED ou SPED_ISSUE_LIMIT_EXCEEDED.
- 415: tipo não aceito ou SPED_ENCODING_NOT_SUPPORTED.
- 422: SPED_FILE_EMPTY.
- 503: PARSER_NOT_CONFIGURED; normalmente configuração inválida já impede startup.
- 504: OPERATION_TIMEOUT; cancelamento pelo chamador é propagado.

Problemas têm contractVersion, executionId/correlationId e lineNumber quando conhecido;
não retornam campos, conteúdo ou registros parcialmente lidos. Nenhuma exclusão/normalização
silenciosa transforma arquivo com erro em sucesso.

## Exemplo de envio (arquivo sintético/local)

```powershell
curl.exe --request POST "https://localhost:60996/api/v1/sped/leitura" --header "Content-Type: application/octet-stream" --data-binary "@C:\caminho\arquivo.txt"
```

Modo normal exige Authorization:Bearer com analysis.execute; não colocar token em arquivos
versionados. Development mantém a exceção loopback autorizada e validação de TLS do cliente.
Não usar o exemplo como autorização para envio de dados reais sem política aprovada.

Referência: [fonte recebida](../validation/supported-layouts.md) e
[decisão](../adrs/0002-parser-sped-leitura.md).
