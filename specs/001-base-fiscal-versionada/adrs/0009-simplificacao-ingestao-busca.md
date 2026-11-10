# ADR 0009 — Simplificação: ingestão com publicação automática e busca via MCP

Data: 2026-11-10. Status: **aceito, definitivo**. Substitui as partes de ADR 0004, 0006 e 0007
que tratam de aprovação, idempotência por cabeçalho, revisão por ETag, reprocessamento
administrativo, retirada, catálogo de perfis e expurgo. Não altera a constitution.

## Contexto

A Api.RAG implementava um ciclo administrativo completo: `Idempotency-Key` em todos os
comandos, `ETag`/`If-Match` sobre revisões, aprovação manual (`approve`) para publicar,
reprocessamento, retirada, catálogo de perfil adotado, descoberta de artefatos retidos e expurgo.
A busca MCP só enxergava versões aprovadas cujo perfil estivesse `adopted`, com `EvaluationId`,
`ProfileApprovalReference` e catálogo adotado. Na prática local, ingerir um documento não o
tornava pesquisável sem várias etapas manuais e configurações sem avaliação real disponível.

O responsável solicitou: “retirar Idempotency-Key e If-Match dos processos; apenas ingerir dados
e buscar conhecimento via MCP”, escolhendo publicação automática, remoção dos endpoints de ciclo
de vida e busca com o perfil configurado.

## Decisão

1. **Sem cabeçalhos de controle.** `POST /api/v1/ingest` não exige `Idempotency-Key`; nenhuma
   rota usa `If-Match`/`ETag`. A idempotência passa a ser natural pelo documento:
   - mesmo `issuer` + `officialIdentifier` + `officialVersion` + perfil, com geração concluída ou
     em andamento e com trechos → devolve a operação existente, sem novo processamento;
   - só gerações com falha, ou texto pronto sem trechos (ex.: trechos apagados manualmente) →
     cria nova geração e processa de novo;
   - mesma versão com bytes diferentes → 409 `VERSION_CONTENT_CONFLICT`; metadados de fonte
     diferentes → 409 `SOURCE_METADATA_CONFLICT`;
   - envios concorrentes do mesmo documento são serializados por lock consultivo e convergem
     para uma operação; a operação registrada e não usada é descartada (sem órfãs em `queued`).
2. **Publicação automática.** Ao concluir texto e vetores, a geração vira a ativa e a versão
   passa a `published`. `approvedBy`/`approvedAt` registram o autor e o instante da primeira
   publicação. Uma nova geração concluída da mesma versão substitui a ativa (a anterior recebe
   `superseded_at`). Reenvio de geração concluída ainda não publicada (dado anterior a esta
   decisão) publica sem reprocessar.
3. **Versão mais recente vence.** Publicar uma versão retira (`withdrawn`) as demais versões
   publicadas da mesma fonte. Referências antigas continuam resolvíveis por `ResolverReferencia`
   com estado `withdrawn`/`superseded`.
4. **Busca com o perfil configurado.** `Retrieval:AdoptedProfileId` ou, se vazio, o único perfil de
   `Processing:Profiles`; deve coincidir com provedor/modelo/dimensão de `Embeddings`. Não exige
   `adopted`, `EvaluationId`, `ProfileApprovalReference` nem catálogo. `TopK` ausente = 5;
   `Threshold` ausente = sem corte de similaridade. Filtros por tipo de documento e versão de
   layout permanecem obrigatórios.
5. **Removidos da API:** `POST /admin/v1/document-versions/{id}/approve|reprocess|withdraw`,
   `GET /admin/v1/document-versions/{id}/retained-artifacts` e
   `POST /admin/v1/retained-artifacts/{id}/expurge` (agora 404), com o código que só eles usavam
   (`AdministrarConhecimento`, `AdotarPerfilDoCatalogo`, `IdempotencyCoordinator`,
   `RevisionPreconditions`, catálogo, retenção/expurgo e seus testes) e o schema
   `contracts/retained-artifacts.schema.json`.
6. **Mantidos:** `POST /api/v1/ingest`, `GET /admin/v1/operations/{id}`,
   `GET /admin/v1/document-versions/{id}`, tools MCP `BuscarConhecimento` e `ResolverReferencia`
   (contrato inalterado), autenticação/autorização, limites, leases, retomada de vetores,
   medição de consumo e auditoria.

## Desvio constitucional (constitution §8)

| Item | Registro |
| --- | --- |
| Regras afetadas | §5 (segmentação, reprocessamento, atualização e busca avaliados com exemplos; topK/limiares não fixados arbitrariamente); Definition of Done (retenção e exclusão verificadas). |
| Justificativa | Viabilizar o uso local de ponta a ponta (ingerir → buscar) sem avaliação oficial nem política de retenção aprovadas disponíveis. |
| Impacto | Documentos ingeridos entram na busca sem revisão humana; topK 5/sem limiar quando não configurados; nenhum dado é excluído pela API (retenção configurada é apenas exigência formal, sem expurgo); escopos `knowledge.publish`, `knowledge.reprocess`, `knowledge.withdraw` e `knowledge.expurge` ficam sem rota. |
| Responsável | Vitor Vianna. |
| Aprovação | Solicitação do responsável em 2026-11-10 (sessão de desenvolvimento), confirmada como mudança definitiva de requisito. |
| Escopo | Feature 001 (Api.RAG). Definitivo; não é desvio temporário. |

## Consequências

- Requisitos de aprovação, retirada administrativa, expurgo e catálogo saem do escopo da feature
  001 (ver spec, tasks e contratos atualizados). Tabelas `catalog`, `retained_artifacts`,
  `retention_obligations`, `reference_tombstones` e `command_results` permanecem no esquema por
  compatibilidade das migrações, sem uso pela aplicação.
- Configurações sem efeito: `Processing:ProfileApprovalReference`; `AdoptionState`/`EvaluationId`
  só são validados no formato do perfil; prazos de `Retention` não disparam exclusão.
- Para refazer um documento basta reenviá-lo; não apagar trechos manualmente (quebra referências
  já devolvidas).
- Evidência: 97 testes da Api.RAG (unitários, contrato e integração com PgVector), incluindo
  publicação automática, substituição por nova versão, reenvio após falha, reenvio após trechos
  apagados, convergência de envios concorrentes e 404 das rotas removidas.

Rastreio: ADR 0004/0006/0007 (substituídos parcialmente); contratos `admin-rest.md` e `mcp.md`.
