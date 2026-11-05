# Implementation Plan: Base de conhecimento fiscal versionada com recuperação rastreável

**Branch**: `main` na publicação inicial dos artefatos; diretório da feature
`001-base-fiscal-versionada`, independente do nome da branch.
**Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-base-fiscal-versionada/spec.md`.
**Status**: Fases 0 e 1 concluídas no nível de desenho; plano sincronizado às dez respostas
da spec e à constitution 1.1.0. Ativação e aceite dependem dos gates P01–P04 abaixo.
Este estado não representa implementação, avaliação executada ou aprovação de produção.

**Revisão de consistência**: 2026-10-30 — aplicadas as três correções aprovadas pelo usuário:
gates anteriores aos ensaios reais, retenção parametrizada com valores antes da ativação e
descoberta administrativa dos metadados/revisões de artefatos retidos. Tarefas geradas e
sincronizadas. Em 2026-10-30 foi iniciado somente o scaffold da Phase 1 das tasks:
T001–T003 criaram solução, convenções e documentação. Em 2026-11-05, T004 e T006 foram concluídas com restore/build e teste local aprovados; T005 também concluída em 2026-11-05: host, dublês, consumidor MCP separado e PostgreSQL/PgVector descartável validados; seis testes passaram. Não há comportamento de negócio implementado. Ver
[evidências da implementação](validation/phase-1.md).

## Summary

Disponibilizar na Api.RAG uma base oficial compartilhada de manuais e guias PDF com texto
extraível, administrada centralmente e consultada exclusivamente por MCP. A ingestão inicial
exige aprovação explícita; reprocessamento bem-sucedido substitui automaticamente uma geração
já disponível, mantendo a anterior durante processamento e falhas. Retirada bloqueia novas
buscas, mas preserva resolução histórica autorizada durante a retenção aprovada.
Retenção exige prazo por categoria, contado da retirada/substituição, com expurgo manual
central após vencimento. Atualização de bytes exige outra versão oficial; conflito de mesma
identidade/versão é rejeitado. Perfil inicial: 300 PDFs, 5 consultas simultâneas e pelo menos
95% das consultas completas em até 5 segundos, medidos pelo cliente, incluindo embedding
da pergunta e excluindo geração de parecer. Fiscal aprova fontes e expectativas; técnica avalia.

A proposta técnica usa ASP.NET Core .NET 10, PostgreSQL/PgVector, extração PDF por adaptador,
gerações imutáveis e uma fila durável no próprio banco. Recuperação inicial usa busca exata
com filtros relacionais de aplicabilidade e geração ativa. Não haverá LLM gerador, interface
gráfica, OCR, schemas fiscais ou acesso funcional REST nesta feature.

As escolhas técnicas abaixo são propostas de planejamento fundamentadas em
[research.md](research.md) e [ADR-0001](adrs/0001-arquitetura-rag.md), não novos requisitos
fiscais ou parâmetros de qualidade aprovados.

**Atualização normativa**: alinhado à constitution 1.1.0 e a QR-001–003 da spec em 2026-10-29.
DDD, Clean Code e arquitetura ágil são obrigações aprovadas pelo usuário; a proposta técnica
e as condições de ativação e aceite estão discriminadas em P01–P04.

## Technical Context

**Language/Version**: C# com alvo `net10.0`, ASP.NET Core 10. SDK 10.0.303 e runtime 10
observados localmente; SDK padrão atual é 11 RC. A implementação deverá selecionar
explicitamente SDK estável da família 10 em `global.json`, sem alterar a constitution.

**Primary Dependencies**: SDK oficial MCP para ASP.NET Core e cliente de teste compatível;
Npgsql e Pgvector para persistência; PdfPig para extração; autenticação Bearer do ASP.NET
Core; cliente de embeddings OpenAI isolado em infraestrutura. Pesquisa MCP observou versão
2.3.0 e protocolo 2026-07-28. Pacotes restaurados e fixados nos lockfiles na T004, com build e teste local Pgvector/Npgsql aprovados. Integrações reais e protocolo MCP permanecem nas tarefas próprias.

**Storage**: proposta D11: banco lógico dedicado à Api.RAG no PostgreSQL, extensão `vector`,
metadados, trechos, gerações, originais PDF em `bytea`, fila de processamento e auditoria.
Credencial de aplicação sem permissão de administrar schema; migração com credencial separada.
Api.Agente não recebe credencial desse banco. A viabilidade de PDFs em banco depende do
volume/limite aprovado; armazenamento de objetos é alternativa a reavaliar se necessário.
Não há MongoDB/Redis ou novo serviço de fila nesta feature.

**Testing**: xUnit; testes unitários sem rede/provedor real; integração com PostgreSQL/PgVector
isolado e cliente MCP real em processo separado; contratos REST/MCP; testes de concorrência,
falhas e referências históricas. Avaliação de qualidade usa corpus aprovado, separado de
fixtures técnicas sintéticas. Seis testes de infraestrutura passaram em T004/T005 (Unit, Contract e Integration); testes funcionais fiscais ainda não foram implementados.

**Target Platform**: desenvolvimento Windows PowerShell; serviço ASP.NET Core portável,
processo persistente capaz de executar tarefas em segundo plano. Implantação e fornecedor de
infraestrutura não selecionados; ambiente que suspenda o processo exige reavaliar o worker.
Não se presume implantação serverless ou configuração de produção existente.

**Project Type**: serviço web de conhecimento e biblioteca de testes; somente Api.RAG será
implementada nesta feature. Um cliente MCP de teste representa o consumidor sem criar uma
Api.Agente incompleta. Semantic Kernel e A2A permanecem nas features próprias da Api.Agente.

**Performance Goals**: SC-009 e SC-010: 300 PDFs, 5 consultas simultâneas, ≥95% das
consultas completas em até 5 segundos pelo cliente. Incluir embedding de pergunta, busca e
serialização/transferência da resposta; excluir parecer LLM. Falhas/timeouts não contam como
sucesso nem podem ser omitidos do relatório. Registrar amostra, duração, ambiente, cache e
características dos PDFs antes do ensaio. Os valores não são limite máximo de capacidade.

**Constraints**: PDF textual, sem OCR; versão do manual distinta da versão do layout;
referências imutáveis, aprovação inicial e troca automática apenas de geração concluída de
fonte ainda publicada. Mesma identidade/versão com bytes diferentes é rejeitada. Prazo de
retenção é obrigatório por categoria; sem configuração não habilitar expurgo. Vencimento não
apaga conteúdo. Limites finitos de entrada, timeout e tentativas são configuração operacional
obrigatória, não parâmetro de qualidade aprovado por convenção.

**Scale/Scope**: base oficial global compartilhada, administração central e consulta autorizada;
300 PDFs com 5 consultas simultâneas. Tamanho/páginas e crescimento não foram quantificados:
caracterizar corpus e dimensionar ambiente no protocolo de capacidade antes do aceite. Fontes,
perguntas e evidências são aprovadas pelo responsável fiscal; equipe técnica organiza e executa.
Não há corpus oficial nem parâmetros finais de recuperação aprovados neste momento.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Verificação pré-pesquisa e pós-design: nenhum desvio constitucional identificado. As respostas
resolveram as escolhas de comportamento necessárias ao desenho. Valores de configuração e
insumos de avaliação continuam como pré-requisitos explícitos das atividades dependentes;
não são considerados aprovados ou executados pela conclusão das Fases 0 e 1.

| Princípio | Verificação no planejamento | Situação |
| --- | --- | --- |
| 1. Autoridade documental | Não inventar fontes, regras, layouts ou compatibilidades; responsável fiscal valida D12. | Conforme no desenho; P02 bloqueia avaliação sem corpus aprovado. |
| 2. Validação determinística | Validar entrada, metadados, versão, estado e contratos por código verificável; nenhum XSD ou conclusão fiscal nesta feature. | Conforme no escopo proposto. |
| 3. Rastreabilidade | Distinguir limitação, entrada inválida e falha técnica; correlação por operação/tentativa. | Conforme no escopo proposto. |
| 4. Limites de serviço | Api.RAG proprietária de dados; REST administrativo e MCP funcional; camadas lógicas sem bibliotecas extras. | Conforme na proposta D11. |
| 5. RAG aplicável | Aplicabilidade relacional, geração ativa, referências exatas; parâmetros exigem avaliação. | Conforme no desenho; parâmetros finais dependem de P02. |
| 6. LLM | Não há geração de análise; embeddings são instrumentados, sem tratar vetores como evidência fiscal. | Não aplicável ao contrato de resposta LLM; não criar gerador. |
| 7. Ferramentas | Tools de leitura, autorização independente, validação, limites e testes; Semantic Kernel fica na Api.Agente. | Conforme no desenho em contracts/. |
| 8. Proteção | Bearer, permissões explícitas, identidade fora dos argumentos, conteúdo não confiável e logs mínimos. | Conforme na proposta; emissor é dependência ambiental. |
| 9. Memória | Não incluir memória conversacional na base oficial. | Fora do escopo. |
| 10. Consumo | Api.RAG registra todas as tentativas de embeddings no seu PostgreSQL; desconhecido nulo e custo estimado separado. | Conforme na proposta; sem preço inventado. |
| 11. Contratos | SDK MCP atual pesquisado; versionar contrato da aplicação separadamente do protocolo. | Conforme no desenho em contracts/. |
| 12. SDD | Spec e dez respostas preservadas; pendências explícitas e decisões técnicas em ADR. | Conforme no desenho; gates P01–P04 rastreados. |
| 13. DDD | Contexto Conhecimento Fiscal, glossário da spec, invariantes explícitas e domínio independente; modelo tático em data-model.md. | Conforme no desenho; invariantes e fronteiras documentadas. |
| 14. Clean Code | Casos de uso localizáveis, regras coesas sem duplicação nos transportes, falhas explícitas e verificações automatizadas de organização. | Diretriz incorporada; implementação e análise estática ainda não existem. |
| 15. Arquitetura ágil | Incrementos por história/risco, ADR revisável e estruturas adicionais justificadas. | Conforme na proposta; evidências por incremento exigidas. |

### Condições de ativação e aceite

Os antigos B01–B03 deixam de bloquear o desenho: política, responsabilidades, volume e meta
foram esclarecidos. Isso não aprova insumos que ainda faltam. Não há esclarecimento de
comportamento pendente que impeça definir contratos e modelo parametrizado.

| Gate | Condição obrigatória | Responsável e momento |
| --- | --- | --- |
| P01 / D07 | Prazos por categoria de fonte retirada e geração substituída, retenção de originais/referências e política própria de auditoria/consultas; sem valores não habilitar persistência operacional correspondente ou expurgo. | Produto/dados e administrador central, antes da ativação/persistência real. Fixtures de teste usam política sintética explícita. |
| P02 / D12 e D04 | Responsável fiscal designado; manifesto de fontes/versões e aplicabilidade, perguntas, evidências, critérios de relevância aprovados antes da avaliação. Técnica compara candidatos e registra modelo/dimensão, segmentação, topK e limiar antes de adotar perfil. | Fiscal aprova conteúdo e expectativas; técnica executa; bloqueia avaliação oficial e adoção de parâmetros, não código parametrizado ou testes sintéticos. |
| P03 / capacidade e D11 | Caracterizar bytes/páginas/trechos dos 300 PDFs, amostra, ambiente e cache; demonstrar 5 consultas simultâneas e ≥95% completas em até 5s, sem ocultar erros. Reavaliar bytea ou índice apenas com evidência. | Técnica/operação, antes do aceite SC-009/010 e do dimensionamento de produção. |
| P04 / operação | Emissor/audience/permissões, credenciais, TLS, limites finitos, timeout, retry, lease de jobs, configuração e backup/restauração validados; pacotes fixados e compatibilidade verificada. | Técnica/operação, antes da integração real e exposição operacional. |

A ratificação constitucional pendente não foi presumida nem alterada. Não foi concedida
exceção. Políticas de retenção de auditoria e consultas ainda precisam ser definidas antes
da persistência operacional; nenhum prazo foi inventado para liberar código dependente.

### Fase 0 — pesquisa e decisões

Concluída para o desenho: [research.md](research.md) incorpora a segunda rodada e distingue
escolhas arquiteturais de pré-requisitos operacionais. D04 define estratégia de avaliação,
isolamento de perfis e baseline exata; parâmetros finais continuam condicionados a P02.
D11 adota desenho inicial de banco dedicado com originais bytea, revisável por P03.
D07 define autorização por operação e retenção manual parametrizada; D12 separa aprovação
fiscal e execução técnica. Fontes oficiais não foram inventadas.

### Fase 1 — desenho e contratos

Concluída em:

- [data-model.md](data-model.md): contexto, agregados, objetos de valor, estados, constraints,
  invariantes, concorrência e retenção configurável.
- [contracts/admin-rest.md](contracts/admin-rest.md): ingestão POST, acompanhamento,
  aprovação, reprocessamento, retirada, consulta dos metadados/revisões de artefatos retidos,
  expurgo e erros administrativos versionados. Consulta exige knowledge.audit central, não
  retorna conteúdo e não concede knowledge.expurge; o comando revalida tudo na transação.
- [contracts/mcp.md](contracts/mcp.md) e [schemas](contracts/mcp-tools.schema.json): tools
  funcionais de busca e resolução, entrada/saída, permissões, falhas e cancelamento.
- [quickstart.md](quickstart.md): guia de execução e validação após implementação, com
  pré-requisitos e resultados esperados, sem afirmar testes contra aplicação inexistente.

Revisão constitucional pós-design registrada na tabela acima. [tasks.md](tasks.md) contém
84 tarefas; T075 comprova P01 e T076 comprova P04 antes da avaliação oficial T077, que depende
explicitamente de ambas. O ensaio T078 herda esses gates via T077. T075/T076 pertencem à fase
de validação da US5, antes dos ensaios, não ao fechamento posterior. Próxima etapa recomendada:
revalidar com `$speckit-analyze`. Não foi criado código de aplicação, configuração operacional,
migração ou teste executável.

### Verificação dos artefatos de desenho

Links locais conferidos e schemas MCP validados com Test-Json do PowerShell: entrada válida,
parâmetro extra proibido, ausência normal de evidências, falha técnica válida, evidência vazia
indevidamente declarada, erro técnico ausente, versão de contrato incorreta e localização
indisponível com/sem página inventada. Nove verificações documentais aprovadas.
Atualização T004 (2026-11-05): restore, build e teste local de compatibilidade aprovados. Integração, embeddings reais e capacidade não executados.

### Cobertura prevista de validação

| Requisitos | Verificação a detalhar na Fase 1 | Critérios |
| --- | --- | --- |
| FR-001–003, FR-018 | PDF textual aceito; outros formatos, OCR necessário e metadados ausentes recusados; acompanhamento correlacionado. | SC-006, SC-007 |
| FR-004, FR-009, FR-014 | Resolver texto da geração citada, localização real/ausente, retirada durante retenção e indisponibilidade após expurgo. | SC-003 |
| FR-005–006 | Tipo/versão obrigatórios; manual/layout distintos; excluir versão incompatível mesmo com texto idêntico; testar filtro no banco. | SC-001 |
| FR-007–008 | Nenhuma evidência, evidência parcial e falha técnica são resultados distintos; sem alegar suficiência fiscal. | SC-004 |
| FR-010–012 | Repetição concorrente, conflito de bytes na mesma versão, falha no meio e troca automática sem mistura; referências imutáveis. | SC-002, SC-006 |
| FR-013, FR-015–016 | Aprovação inicial explícita pelo mesmo administrador permitida; consumidor e administrador organizacional sem permissão central negados; auditoria. | SC-005 |
| FR-017, FR-019 | Cliente real MCP, descoberta e chamadas; sem REST funcional ou acesso cruzado; documento não altera filtros ou autorização. | SC-008 |
| FR-020–021 | Fiscal aprova fontes e expectativas; técnica compara perfis; relatório e critérios prévios; impedir adoção sem avaliação. | SC-007 |
| FR-014–016 | Prazo por categoria, evento de retirada/substituição, expurgo central manual somente após vencimento; sem configuração não expurgar, sem exclusão automática. | SC-003, SC-005 |
| FR-014–016 | Descobrir IDs/ETags por consulta administrativa de metadados; sem permissão negar; sem política informar impedimento; revisão antiga ou perda de elegibilidade bloqueia expurgo. | SC-003, SC-005; T052, T055–T056, T063 |
| SC-009–010 | 300 PDFs caracterizados, 5 consultas simultâneas, ≥95% completas em até 5s pelo cliente, incluindo embedding e registrando falhas. | P03 |
| Constitution §2.10 | Uma linha por tentativa de embedding; tokens/custo desconhecidos nulos; falhas e retentativas preservadas. | Gate de auditoria herdado. |
| QR-001–003 | Contexto e linguagem coerentes, invariantes testáveis, dependências corretas, organização por caso de uso, formatação/análise estática e ADRs de evolução. | Gate arquitetural da spec e DoD constitucional. |

## Project Structure

### Documentation (this feature)

```text
specs/001-base-fiscal-versionada/
├── spec.md
├── plan.md
├── research.md
├── tasks.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── admin-rest.md
│   ├── mcp.md
│   ├── mcp-tools.schema.json
│   └── retained-artifacts.schema.json
├── adrs/
│   └── 0001-arquitetura-rag.md
└── checklists/
    └── requirements.md
```

As tarefas existem e refletem esta revisão; sua geração não equivale a execução ou conclusão.

### Source Code (repository root)

Estrutura futura proposta; nenhum destes projetos ou arquivos de aplicação foi criado:

```text
src/
└── Api.RAG/
    ├── Endpoints/
    ├── Application/Knowledge/       # Casos de uso e portas, organizados por funcionalidade
    ├── Domain/Knowledge/            # Modelo, invariantes e linguagem do contexto
    └── Infrastructure/Knowledge/    # Persistência, PDF, embeddings e adaptadores
tests/
└── Api.RAG.Tests/
    ├── Unit/
    ├── Integration/
    ├── Contracts/
    └── Evaluation/
```

**Structure Decision**: um projeto web e um projeto de testes, com pastas por camada;
domínio sem dependências dos SDKs e banco. Worker dentro da Api.RAG para evitar um novo
serviço sem necessidade demonstrada. O projeto de testes justifica cliente MCP isolado e
infraestrutura de integração. Api.Agente completa fica fora do escopo desta entrega.

**Organização DDD e Clean Code**: Conhecimento Fiscal delimita o modelo desta feature;
Análise Fiscal/Orquestração e Memória Conversacional são contextos relacionados, sem modelos
compartilhados impostos por esta entrega. Casos de uso de ingestão, publicação, reprocessamento,
retirada, busca e resolução histórica devem ter localização própria na aplicação. Domínio
concentra regras de estado, aplicabilidade e identidade; infraestrutura implementa as portas
necessárias. Endpoints REST/MCP delegam aos casos de uso; composição registra adaptadores.
Testes de invariantes não dependem de HTTP, MCP ou banco. A organização física proposta é
revisável, desde que esses limites sejam preservados e documentados.

**Qualidade por incremento**: configurar convenções de nomes e formatação com EditorConfig,
analisadores .NET e verificação de dependências no projeto de testes. Documentar as regras
habilitadas antes de usá-las como gate; código novo não deve introduzir violações dessas
regras. Revisão deve confirmar coesão, ausência de regras duplicadas, erros explícitos e
justificativa de novas abstrações. Refatorações exigem testes de comportamento relevantes.
Não há limite arbitrário de linhas, classes ou camadas estabelecido neste plano.

## Complexity Tracking

Nenhum desvio constitucional proposto. Não há justificativa para abstração multibanco,
broker adicional, ANN, memória conversacional ou LLM gerador neste momento. Bloqueios de
informação não são exceções nem podem ser tratados como aprovação de suposições.
