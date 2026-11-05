# Evidência — ajustes 1–4 da API RAG

Data: 2026-11-05. Escopo autorizado: organização inspirada na Api.Conhecimento,
ingestão administrativa, modelos/persistência e extração/segmentação de PDF.

## Resultado

POST /api/v1/ingest recebe PDF/metadata com JWT e knowledge.ingest central.
Operação, original, fonte/versão, aplicabilidade, snapshot do perfil e auditoria são atômicos.
GET /admin/v1/operations/{id} e /document-versions/{id} exigem knowledge.audit e retornam
metadados/ETag. Fila PostgreSQL e lease permitem retomar trabalho interrompido, com SKIP LOCKED
e proteção contra confirmação por proprietário antigo. Nenhuma requisição externa é feita.

O worker prepara trechos e manifesto textual em transação única após a extração completa;
mantém geração processing, operação queued/EMBEDDINGS_PENDING e versão unpublished.
Falha de página ilegível não persiste trechos parciais. A etapa de embeddings não foi simulada.

## Rastreabilidade

| Ajuste / requisito | Implementação | Evidência automatizada |
| --- | --- | --- |
| 1; QR-001–003 | Controllers tipados registrados explicitamente, aplicação/portas, domínio e adaptadores Ingestao; composição preserva MCP sem novas tools | Compilação/analisadores; regressão FoundationTests e testes MCP existentes |
| 2; FR-001–003, FR-010–011, FR-015–019 | IngerirFonte, IngestionPolicy, DocumentRepository, IngestionWorker, IngestController e acompanhamento | IngestionContractTests: upload concorrente/replay, JWT/permissões, entradas inválidas, retenção antes do efeito e leitura administrativa independente |
| 3; FR-002–004, QR-001 | FonteOficial, VersaoDocumental, Aplicabilidade, GeracaoProcessamento, Trecho/UUID de referência, Localizacao e PerfilProcessamento; migration 0006 | DocumentInvariantsTests; IngestionJourneyTests; original byte a byte, reutilização de geração, conflito de origem/perfil, rollback e lease em PostgreSQL |
| 4; FR-001–004, FR-019–021 | PdfPig por página, segmentação estrutural/janela lexical, spans, hashes e perfis externos versionados | DocumentInvariantsTests: páginas físicas, localização ausente explícita, texto repetido/proveniência, offsets, overlap, PDF parcial/inválido e limite de páginas/bytes |

## Verificação executada

Suíte completa: **60 aprovados, 0 falhas, 0 ignorados** (22 Unit, 15 Contract,
23 Integration). Infraestrutura: PostgreSQL/PgVector descartável com identidades de migração
e aplicação separadas; runner aplica todas as migrações duas vezes. PDF sintético gerado
localmente; não há corpus fiscal aprovado, credencial OpenAI ou chamada faturável.
TRX sanitizado: [ingestion-adjustments.trx](ingestion-adjustments.trx).

Build final: 0 avisos e 0 erros. Formatação final verificada sem alterações (exit 0).

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/ingestion-complete
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore
```

## Estado das tarefas e limites

T020–T028 cobrem modelos, persistência e preparo textual. T030 implementa recebimento/fila
sem depender de chamada de embeddings; sequência incremental autorizada no ADR 0005.
T019/T031/T033/T034 permanecem parcialmente abertas, pois seu aceite integral inclui
geração com embeddings e/ou aprovação. T029 e T032 não foram implementadas.

Rag/Tools têm documentação de responsabilidade; não há serviços vazios ou ferramentas
que anunciem busca indisponível. A API ainda não fornece recuperação semântica funcional.
Não foram implementados publicação, expurgo, consulta vetorial ou parecer SPED.
P01–P04 produtivos e a feature 001 completa não foram aprovados por estes testes.

Limites explícitos: parâmetros sintéticos não aprovam qualidade de produção; tokens lexicais
não são BPE/consumo OpenAI; rótulos impressos/seções não são inferidos; páginas sem texto,
inclusive brancas, são recusadas; cancelamento do parser é cooperativo entre páginas.
Modelo/provider/dimensão, chunk/overlap, avaliação e retenção reais continuam externos.
Não houve migração de banco real, commit ou push neste incremento.

Revisão posterior: cinco achados corrigidos e 68 testes aprovados; ver [ingestion-review.md](ingestion-review.md). Os 60 testes acima são a evidência histórica do incremento inicial.
