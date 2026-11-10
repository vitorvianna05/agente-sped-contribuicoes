# ADR 0007 — Correções de perfil, retenção e consumo

> **Nota (2026-11-10):** as correções de perfil staged/catálogo e de obrigações de retenção foram substituídas pela [ADR 0009](0009-simplificacao-ingestao-busca.md) (sem catálogo nem expurgo; LifecycleCorrectionsTests removido). A correção de consumo (uso conhecido preservado em vetor inválido) continua vigente.

Data: 2026-11-10. Escopo: quatro achados da revisão de requirements/architecture.

A conclusão de um reprocessamento com outro perfil mantém a geração íntegra staged,
sem publicar referências ou trocar o ponteiro ativo. Revisão, estado published e ponteiro
esperados continuam obrigatórios. O corte técnico exige todas as versões publicadas
preparadas e troca o catálogo em uma transação. Perfil idêntico mantém ativação automática.

Retenção de geração tem obrigações distintas por substituição e retirada. A migração
0009_retention_obligations preserva o registro original e seus marcos; acrescenta uma
obrigação por tipo de evento, sem reiniciar prazos em retries. Não altera migrações antigas.
A projeção administrativa apresenta o maior vencimento das obrigações conhecidas; prazo
ou aprovação ausente impede expurgo. Nova obrigação muda a revisão do artefato, invalidando
uma precondição obtida antes da retirada. O comando reavalia todas as obrigações sob lock.

ObrigacaoRetencao calcula prazos por categoria e vencimento efetivo no domínio. O repositório
persiste snapshots; não escolhe duração por convenção nem recalcula a política anterior.
Backfill conserva prazos existentes. Para gerações substituídas de versões já retiradas,
quando não existe snapshot do prazo da retirada, registra obrigação pendente com marco real
e prazo/política nulos. Sua regularização exige decisão explícita de política, fora deste
incremento; nenhuma aprovação retroativa é presumida. Conteúdo já expurgado não é recuperado.

Se uma resposta de embedding contém uso conhecido, a medição o conserva mesmo quando a
validação do vetor falha. A tentativa fica failed e não é repetida por erro de dimensão ou
valor não finito. Custos desconhecidos continuam nulos; valores reportados e estimados
permanecem separados. A API OpenAI real não é chamada pelos testes de regressão.

Rastreio: RG-009/RG-010; FR-012/014/016/018; T029/T037/T053/T054/T058/T064.
Testes: LifecycleCorrectionsTests, FoundationTests.InvalidEmbeddingPreservesReportedUsage,
VectorDomainTests.RetentionObligationsPreserveAllEventDeadlines.
