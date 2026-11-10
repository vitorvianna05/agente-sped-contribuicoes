# Plano — base fiscal versionada e recuperação rastreável

Sincronização: 2026-11-10 (simplificação). Feature `001-base-fiscal-versionada`; base normativa:
constitution 1.1.0, [spec.md](spec.md) e [requisitos globais](../../docs/requirements.md).
O desenho foi iniciado em 2026-10-29. Este plano descreve o estado atual; não aprova operação
real nem substitui os relatórios históricos. Decisões vigentes: [ADR 0009](adrs/0009-simplificacao-ingestao-busca.md)
e [ADR 0010](adrs/0010-ambiente-local-openai-swagger.md).

## Estado atual e evidências

A Api.RAG implementa ingestão administrativa durável de PDF textual, extração/segmentação,
embeddings de documento e consulta pelo SDK OpenAI, persistência PgVector, busca exata por
cosseno, publicação automática e servidor MCP. REST é administrativo (ingestão e
acompanhamento); a recuperação funcional ocorre exclusivamente por MCP. Não há validação de
arquivo SPED nem geração de parecer na Api.RAG: pertencem à Feature 002.

- [tasks.md](tasks.md): 86 tasks — 59 concluídas, 22 pendentes e 5 canceladas pela ADR 0009.
  Sincronizar caminhos não revalida automaticamente os critérios das tasks já marcadas.
- Fundação: [Phase 1](validation/phase-1.md) e [Phase 2](validation/phase-2.md).
- Ingestão textual: [incremento](validation/ingestion-adjustments.md) e
  [correções](validation/ingestion-review.md).
- Vetorização/consulta/MCP: [ajustes 5–11](validation/rag-complete.md) (histórico do ciclo administrativo).
- Evidência mais recente: 97 testes aprovados (unitários, contrato e integração com PgVector),
  build sem avisos/erros e formatação conferida em 2026-11-10; ingestão real local do Guia Prático
  da EFD-Contribuições 1.35 com 814 trechos vetorizados pela OpenAI (ADR 0010).
- [Sincronização SDD](validation/sdd-synchronization.md): mapa de implementação/evidência.
  Não equivale ao aceite final T083/T084.

## Fluxo e limites de responsabilidade

POST `/api/v1/ingest` → operação 202 → preparação textual → embeddings com checkpoints →
geração `succeeded` → **publicação automática** (geração ativa; versões anteriores da mesma fonte
retiradas) → consulta MCP. `preparationState=ready` sozinho não significa publicação: sem etapa
vetorial (sem chave OpenAI) a versão fica `unpublished`.

Não há `Idempotency-Key` nem `If-Match`. Reenvio do mesmo documento (issuer + identificador +
versão + perfil) reaproveita a geração concluída/em andamento com trechos; com só falhas ou sem
trechos, cria nova geração; bytes diferentes na mesma versão → 409. Referências históricas
resolvem o trecho original com estado `superseded`/`withdrawn`, nunca conteúdo de outra versão.
Não há aprovação manual, reprocessamento/retirada administrativos, catálogo nem expurgo.

## Contexto técnico confirmado no repositório

| Item | Implementação atual / limite |
| --- | --- |
| Plataforma | C# / ASP.NET Core net10.0; SDK 10.0.303 em global.json. |
| Organização | Api.RAG e Api.RAG.Tests nesta feature (Api.Agente pertence à feature 002); Domain, Application/Ports, Infrastructure, Controllers, Ingestao, Rag, Tools e Endpoints. |
| Pacotes | MCP 2.3.0, OpenAI 2.14.0, Npgsql 10.0.3, Pgvector 0.3.2, PdfPig 0.1.16, Microsoft.AspNetCore.OpenApi 10.0.14, Swashbuckle.AspNetCore.SwaggerUI 10.3.1; versões em Directory.Packages.props e lockfiles. |
| Persistência | PostgreSQL/PgVector dedicado; originais bytea, texto/vetores/proveniência, operações, consumo e auditoria. Aplicação e migrador usam identidades distintas. |
| Migrações | 0001–0009 aplicadas sem alteração; tabelas de catálogo/retenção/tombstones/command_results ficam sem uso após a ADR 0009. A avaliação planejada seria 0010_evaluation.sql, inexistente. |
| Extração/segmentação | PdfPig encapsulado em Ingestao; páginas e spans observados, sem OCR. Estratégias structural/token-window; whitespace-v1 é tokenização lexical, não BPE nem consumo de cobrança. |
| Busca | SQL parametrizado, cosseno exato, filtros de tipo/layout/publicação/geração ativa/perfil e snapshot consistente. Perfil configurado; TopK padrão 5 e sem limiar quando ausentes. HNSW depende de medição futura. |
| MCP | Streamable HTTP stateless em /mcp; BuscarConhecimento e ResolverReferencia; handlers explícitos com schemas versionados, autorização por chamada e JSON estruturado/textual equivalente. |
| Resiliência | Fila PostgreSQL, leases e tentativas limitadas, vetores por checkpoint, concorrência compartilhada por instância e cancelamento. A geração inteira deve caber em `Processing:TimeoutSeconds`. |
| Consumo | Intenção antes da chamada, desfecho por tentativa, retries medidos, SDK sem retries ocultos; custo estimado com `OpenAI:Pricing` quando configurado; desconhecidos nulos. |
| Desenvolvimento | Docker (PostgreSQL/pgvector + pgAdmin), scripts de setup/ingestão e Swagger em Development (ADR 0010). |
| Testes | xUnit, PostgreSQL/PgVector descartável, Kestrel em porta efêmera e consumidor MCP separado. Avaliação fiscal/capacidade/backup não comprovados. |

## Modelos e configuração

`text-embedding-3-small` (1536 dimensões no exemplo local) para embeddings; `gpt-4.1-mini`
(`OpenAI:ChatModel`) reservado ao parecer da Api.Agente. A seção `OpenAI` fornece chave, modelo de
embeddings e preços; `Processing:EmbeddingStageEnabled` nulo liga a etapa vetorial quando há chave.
Segredos são externos (appsettings.Development.json ignorado, User Secrets ou `OpenAI__ApiKey`).
[Guia local](../../docs/development-configuration.md) descreve o ambiente e os limites de desenvolvimento.

## Exceção local de autenticação autorizada

Somente Api.RAG em Development com `Authentication:DisabledForDevelopment=true` e conexão
loopback: ator `development-local` com permissões locais. HTTPS e hosts permitidos continuam
exigidos; a flag fora de Development falha no startup; JWT e atribuições centrais são o padrão.
Ver [ADR 0008](adrs/0008-configuracao-local-modelos.md).

## Gates de aceite

| Gate | Condição | Estado |
| --- | --- | --- |
| P01 | Retenção/expurgo. | Encerrado pela ADR 0009: a API não exclui conteúdo; configuração de retenção só é registrada. |
| P02 | Fiscal aprova PDFs/versões/aplicabilidade, perguntas, referências, negativos e critérios; técnica compara perfis. T069/T070/T077. | Pendente; não condiciona a busca desde a ADR 0009 (desvio §5 registrado). |
| P03 | 300 PDFs e 5 consultas simultâneas, ≥95% completas em até 5s pelo cliente MCP. T072/T073/T078. | Pendente. |
| P04 | Ambiente, identidades/permissões, TLS, segredos, limites, timeout/retries/leases e recuperação. T076/T081. | Pendente; ambiente local e infraestrutura sintética não aprovam operação real. |

Registro: [gates.md](validation/gates.md).

## Próximos incrementos e dependências

1. T044/T045/T049: completar matriz de resultados, falhas, timeout e cancelamento.
2. T050/T065: completar concorrência de reenvio e substituição por nova versão.
3. T066–T070/T074: matriz de acesso e harness de avaliação.
4. T069/T076: obter insumos fiscais e validação de ambiente; T072/T073 protocolo de capacidade.
5. T077–T079: avaliação e capacidade com insumos reais.
6. T080–T084: arquitetura, recuperação/backup, matriz FR/QR/SC e DoD.

Feature 002 consome as tools MCP sem mudança de contrato. Parecer, A2A e memória são entregas
separadas; nenhuma integração adicional é presumida.

## Cobertura e aceite ainda exigidos

| Requisitos | Verificação exigida | Critérios |
| --- | --- | --- |
| FR-001–003, FR-018 | PDF textual aceito; outros formatos, OCR necessário e metadados ausentes recusados; acompanhamento correlacionado. | SC-006, SC-007 |
| FR-004, FR-009, FR-014 | Resolver texto da geração citada, localização real/ausente e estado após substituição/nova versão. | SC-003 |
| FR-005–006 | Tipo/versão obrigatórios; manual/layout distintos; excluir versão incompatível mesmo com texto idêntico. | SC-001 |
| FR-007–008 | Nenhuma evidência, evidência parcial e falha técnica são resultados distintos. | SC-004 |
| FR-010–012 | Reenvio concorrente, conflito de bytes, reenvio após falha/trechos ausentes, troca sem mistura. | SC-002, SC-006 |
| FR-013, FR-015–016 | Publicação automática auditada; nova versão retira as anteriores; consumidor e administrador organizacional sem permissão central negados. | SC-005 |
| FR-017, FR-019 | Cliente real MCP; sem REST funcional; documento não altera filtros ou autorização. | SC-008 |
| FR-020–021 | Avaliação documentada (sem gate de uso). | SC-007 |
| SC-009–010 | 300 PDFs, 5 consultas simultâneas, ≥95% em até 5s pelo cliente. | P03 |
| Constitution §2.10 | Uma linha por tentativa de embedding; tokens/custo desconhecidos nulos. | Gate de auditoria herdado. |
| QR-001–003 | Contexto e linguagem coerentes, invariantes testáveis, dependências corretas, ADRs. | Gate arquitetural e DoD. |

## Estrutura e decisões rastreáveis

Estrutura efetiva e fronteiras: [architecture.md](../../docs/architecture.md).
Controllers/Tools delegam aos casos de uso; domínio não depende de HTTP/SDK/SQL.

Decisões: [ADR 0001](adrs/0001-arquitetura-rag.md) (proposta inicial), [0003](adrs/0003-fundacao-operacional.md),
[0004](adrs/0004-padrao-api-conhecimento.md), [0005](adrs/0005-ingestao-textual-incremental.md),
[0006](adrs/0006-embeddings-recuperacao-ciclo-mcp.md), [0007](adrs/0007-correcao-perfis-retencao-consumo.md),
[0008](adrs/0008-configuracao-local-modelos.md), [0009](adrs/0009-simplificacao-ingestao-busca.md)
e [0010](adrs/0010-ambiente-local-openai-swagger.md). ADRs 0004/0006/0007/0008 estão parcialmente
substituídos pelas 0009/0010. Pesquisas históricas em [research.md](research.md). Não foi adotado
ANN, broker, memória conversacional, fallback de provedor ou acesso da Api.Agente ao banco RAG.

A sincronização não declara conformidade integral: T080, T083, T084 e os gates P02–P04
permanecem exigidos. Comandos atuais no [quickstart](quickstart.md).
