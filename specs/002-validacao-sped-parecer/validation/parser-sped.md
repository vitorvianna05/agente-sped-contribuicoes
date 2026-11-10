# Evidências — parser de leitura SPED

Data: 2026-11-10. Escopo: solicitação de leitura estrutural e manual fornecido pelo usuário.
Subconjunto de AG005/AG006 e registro parcial de AG001; suas tasks continuam abertas para
catálogo, semântica/hierarquia, regras fiscais, casos oficiais e política de dados restantes.

## Resultado executado

- 78 testes executados, 78 aprovados, 0 falhas e 0 ignorados na Api.Agente.Tests.
- Inclui as 42 regressões anteriores de contratos, autenticação/segurança e cliente MCP.
- Build isolado da Api.Agente/testes: 0 avisos e 0 erros.
- Formatação verificada separadamente no projeto web e testes.
- Nenhuma alteração em código/testes/configuração da Api.RAG nem execução de seus projetos.
- Não houve chamada OpenAI, ingestão RAG, uso de SPED real, persistência ou escrita de arquivo
  temporário pelo endpoint. Não houve commit/push/deploy.
- TRX local, não versionado: `TestResults/sped-parser-final/INFIS001125_INFISPC0064_2026-11-10_14_23_37.402763.trx`.

## Rastreio

| Entrega | Teste / evidência |
| --- | --- |
| Latin-1, acentos, zeros, campos vazios/finais, espaços, aspas e posição do campo | SpedParserTests.ReadPreservesOriginalFieldsAcrossBufferBoundaries, leituras de 1/2/8192 bytes por chamada. Hash cobre bytes e CRLF; stream permanece aberto. |
| Diagnóstico sem normalização silenciosa | MalformedLineHasExplicitLocation e NonCrLfInputIsReadButMarkedInvalid: número físico, controle/pipe/REG/linha vazia e terminadores LF/EOF. |
| Codificação | UnsupportedEncodingIsExplicit: BOM UTF-8/16/32 dividido entre buffers; HTTP rejeita charset incompatível. Sem BOM, a interpretação é Latin-1 explícita, não detecção garantida. |
| Limites/cancelamento/vazio | LimitsCannotProduceSuccessfulPartialDocument e EmptyAndCancelledReadsAreRejected: bytes, linha, registros, campos e ocorrências; sem resultado truncado anunciado como sucesso. |
| Envelope 0000/9999 e código declarado | FileEnvelopeInconsistenciesAreReported: abertura ausente/fora de posição/duplicada, encerramento ausente/não final, COD_VER malformado e QTD_LIN inválido/divergente. Não é aceite de leiaute. |
| HTTP protegido, sem MCP/IA | SpedReadingTests.FileReadingRequiresPermissionAndReturnsExplicitLimitations: 401/403, leitura com analysis.execute, caracteres preservados, IDs do sistema e no-store; destino MCP propositalmente indisponível não é chamado. |
| Estado de leitura separado do fiscal | StructuralIssuesDoNotBecomeSuccessfulValidation: readingStatus=invalid/status=failed; leitura sem ocorrências é partial e fiscalValidationStatus=not_performed. REG não catalogado é preservado sem afirmar suporte. |
| Upload e memória limitados | InvalidUploadNeverReturnsPartialRecordsAsSuccess e ChunkedUploadCannotBypassFileLimit: codificação, vazio, bytes/linha e corpo sem Content-Length produzem erros técnicos sem registros parciais. |

Fonte consultada localmente: guia 1.35, páginas físicas 33/34/66/416; arquivo e SHA-256 em
[supported-layouts.md](supported-layouts.md). Fixtures são abreviadas e sintéticas; não são
exemplos de escrituração fiscal válida. O PDF não foi copiado ao Git nem enviado a serviços.

## Reprodução

```powershell
dotnet build tests/Api.Agente.Tests/Api.Agente.Tests.csproj --no-restore
dotnet test --project tests/Api.Agente.Tests/Api.Agente.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/sped-parser-final
dotnet format src/Api.Agente/Api.Agente.csproj --verify-no-changes --no-restore
dotnet format tests/Api.Agente.Tests/Api.Agente.Tests.csproj --verify-no-changes --no-restore
```

Uso HTTP: [contrato](../contracts/parser-sped.md) e [quickstart](../quickstart.md).
Decisão: [ADR 0002](../adrs/0002-parser-sped-leitura.md). Nenhuma declaração de conformidade,
compatibilidade integral de COD_VER ou qualidade fiscal foi acrescentada.
