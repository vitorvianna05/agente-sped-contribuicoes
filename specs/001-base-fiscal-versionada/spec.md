# Feature Specification: Base de conhecimento fiscal versionada com recuperação rastreável

**Feature Branch**: `main` na publicação inicial dos artefatos; identificação da feature:
`001-base-fiscal-versionada`. Implementações futuras devem seguir a política de branches/PR vigente.

**Created**: 2026-10-29

**Status**: Draft — duas rodadas de cinco esclarecimentos concluídas; pendências remanescentes explícitas.

**Base normativa**: constitution 1.1.0; requisitos de DDD, Clean Code e arquitetura ágil
incorporados por solicitação do usuário em 2026-10-29, sem alterar as cinco decisões anteriores.

**Input**: User description: "Administradores ingerem documentos oficiais (manuais, guias,
schemas), com segmentação, deduplicação, reprocessamento e atualização. Consumidores recuperam
conhecimento por tipo documental e versão compatível, com referências recuperáveis e limitações
explícitas. Controle administrativo e de ciclo de vida. Comunicação funcional exclusivamente
pelo protocolo constitucional. Pendências: D04, D07, D12 e D11."

## Revisão de escopo integrado — 2026-11-05

Arquitetura alvo alinhada à Api.Conhecimento/Api.Atendimento da solução CorporativoAI
indicada pelo usuário. Esta feature fornece embeddings dos manuais/consultas, recuperação
semântica e servidor MCP. A Api.Agente consumirá as evidências para interpretar os resultados
determinísticos e elaborar parecer com IA generativa, planejada na feature 002.
O objetivo continua validar arquivos SPED Contribuições. Similaridade não aprova layouts,
referências históricas não são descartadas e o LLM não substitui validadores obrigatórios.
A revisão altera documentação/contratos planejados, sem modificar o código da fundação.
## Clarifications

### Session 2026-10-29

- Q: A base de documentos oficiais será compartilhada entre organizações ou cada organização terá sua própria base? → A: Base oficial compartilhada entre organizações, alterada somente por administradores centrais e consultada por consumidores autorizados (opção A).
- Q: Após a ingestão bem-sucedida, o documento deve ficar disponível automaticamente ou exigir aprovação administrativa explícita? → A: Exigir aprovação explícita de administrador central; quem ingeriu também pode aprovar (opção B).
- Q: Quando uma fonte já disponível for reprocessada, como deve ocorrer a substituição dos trechos usados nas consultas? → A: Manter os trechos atuais e substituí-los automaticamente após reprocessamento bem-sucedido, sem nova aprovação (opção A).
- Q: Quando uma fonte for retirada de novas consultas, o conteúdo já citado deve continuar acessível pelas referências anteriores? → A: Preservar o conteúdo citado para consumidores autorizados durante a retenção aprovada, com indicação de retirada e exclusão das novas buscas (opção A).
- Q: Quais tipos de documentos devem compor a primeira entrega e seu conjunto de avaliação? → A: Manuais e guias em PDF com texto extraível; schemas e documentos digitalizados ficam para outra entrega (opção A).

- Q: Qual política deve controlar a retenção e a exclusão definitiva de fontes retiradas e gerações substituídas? → A: Prazo obrigatório configurável por categoria, contado da retirada ou substituição, com exclusão manual por administrador central somente após o vencimento; prazos definidos antes da ativação (opção A).

- Q: Quem será responsável por selecionar os documentos oficiais e aprovar as perguntas e evidências esperadas do conjunto de avaliação? → A: Responsável fiscal seleciona e aprova fontes, perguntas e evidências esperadas; equipe técnica organiza e executa a avaliação (opção A).

- Q: Qual volume de documentos e consultas simultâneas devemos considerar para a primeira entrega? → A: 300 PDFs e 5 consultas simultâneas como perfil inicial de capacidade.

- Q: Qual tempo máximo de resposta deve orientar o aceite das consultas nesse perfil de carga? → A: Recuperação completa em até 5 segundos em pelo menos 95% das consultas, com 300 PDFs e 5 consultas simultâneas, excluída a geração de parecer por LLM (opção B).

- Q: Como tratar conteúdo diferente recebido com a mesma identificação e versão oficial de um documento? → A: Rejeitar o conflito; aceitar atualização de conteúdo somente com outra versão oficial, preservando o conteúdo existente (opção A).

### Session 2026-11-05

- Q: Qual função de IA além dos embeddings? → A: Interpretar os resultados das validações e elaborar o parecer com evidências, na Api.Agente consumidora MCP (feature 002).
- Diretriz: seguir o padrão funcional Api.Conhecimento/Api.Atendimento, preservando contexto fiscal, rastreabilidade e validações determinísticas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ingerir fontes oficiais identificadas (Priority: P1)

Como administrador autorizado, quero incorporar um manual ou guia oficial em PDF com texto
extraível e acompanhar o resultado
para disponibilizar conhecimento cuja origem e versão possam ser verificadas.

**Why this priority**: nenhuma recuperação confiável é possível sem fontes identificadas.

**Independent Test**: ingerir uma fonte de teste com metadados conhecidos e conferir os trechos
e referências resultantes, sem depender de uma análise tributária ou de geração de parecer.

**Acceptance Scenarios**:

1. **Given** documento oficial suportado com origem, identificação, versão e aplicabilidade,
   **When** o administrador o ingere, **Then** o processamento é identificado, produz trechos
   vinculados à fonte e informa conclusão ou falha. A conclusão bem-sucedida mantém a fonte
   aguardando aprovação explícita, sem disponibilização automática. (FR-001 a FR-004, FR-013, FR-018)
2. **Given** ausência de identificação, origem, versão ou aplicabilidade obrigatória,
   **When** ocorre a ingestão, **Then** a fonte não fica disponível para consulta e a pendência
   é informada, sem preenchimento inventado. (FR-002, FR-003)
3. **Given** fonte sem localização recuperável em parte do conteúdo, **When** seus trechos
   são disponibilizados, **Then** a ausência é explícita, sem página ou seção fabricada,
   preservando referência à fonte e ao trecho armazenado. (FR-004, FR-009)
4. **Given** arquivo não suportado, ilegível ou fora das condições de processamento definidas,
   **When** é recebido, **Then** a operação falha de modo identificável sem publicar conteúdo
   parcial como fonte concluída. (FR-003, FR-018)
5. **Given** schema, documento digitalizado que exige reconhecimento de texto ou arquivo em
   formato diferente de PDF, **When** sua ingestão é solicitada nesta entrega, **Then** o
   documento é recusado como fora do escopo e nenhum conteúdo é disponibilizado. (FR-001, FR-003)

### User Story 2 - Recuperar evidências da versão aplicável (Priority: P1)

Como consumidor autorizado, quero consultar conhecimento pelo tipo documental e pela versão
aplicável para receber somente evidências compatíveis, com referências verificáveis.

**Why this priority**: evidência de versão errada pode fundamentar uma conclusão incorreta.

**Independent Test**: consultar uma base previamente preparada com versões distintas e comparar
os trechos retornados com as fontes esperadas, sem executar ingestão durante o teste.

**Acceptance Scenarios**:

1. **Given** trechos semelhantes de versões compatíveis e incompatíveis, **When** uma versão
   é solicitada, **Then** somente conteúdo com compatibilidade registrada é retornado;
   semelhança textual não autoriza troca de versão. (FR-005, FR-006)
2. **Given** consulta respondida por evidências, **When** o consumidor recebe o resultado,
   **Then** cada trecho tem identificação, versão, origem e referência resolvível, com
   localização disponível ou ausência declarada. (FR-004, FR-009)
3. **Given** contexto sem tipo ou versão suficientes, **When** é solicitada recuperação,
   **Then** a insuficiência é informada e nenhuma versão é presumida. (FR-005, FR-006)
4. **Given** consumidor funcional, **When** recupera conhecimento ou resolve referências,
   **Then** utiliza somente o protocolo definido na constitution, sem acesso ao armazenamento
   interno nem canal administrativo alternativo. (FR-017)

### User Story 3 - Reconhecer falta de evidência e falhas (Priority: P1)

Como consumidor, quero distinguir ausência de conhecimento de indisponibilidade técnica para
não tratar uma consulta incompleta como prova de conformidade.

**Why this priority**: resultados vazios ou falhos não podem sustentar conclusões fiscais.

**Independent Test**: consultar casos sem resposta e simular falha de dependência sobre uma
base preparada, verificando resultados distintos e suas limitações.

**Acceptance Scenarios**:

1. **Given** nenhuma evidência suficiente para o tipo e versão, **When** ocorre consulta,
   **Then** há limitação explícita e nenhuma resposta ou referência inventada. (FR-007)
2. **Given** apenas evidência parcial, **When** ocorre consulta, **Then** são apresentados
   os trechos disponíveis e a limitação, sem indicar cobertura completa. (FR-007, FR-009)
3. **Given** dependência indisponível, **When** a consulta falha, **Then** o consumidor recebe
   falha técnica distinguível de ausência de evidências e não recebe declaração de conformidade.
   (FR-008, FR-018)
4. **Given** trecho com instruções para ignorar políticas ou mudar versão, **When** é processado,
   **Then** o conteúdo permanece dado documental e não altera permissões ou filtros. (FR-019)

### User Story 4 - Atualizar e reprocessar sem redundância (Priority: P2)

Como administrador autorizado, quero repetir ingestões, atualizar versões e reprocessar fontes
sem duplicar trechos nem perder a identificação do conteúdo consultado anteriormente.

**Why this priority**: a base precisa evoluir preservando rastreabilidade e integridade.

**Independent Test**: repetir uma ingestão, reprocessar a mesma versão e adicionar outra;
comparar trechos ativos, estados e referências antes e depois.

**Acceptance Scenarios**:

1. **Given** mesma fonte, versão, conteúdo e configuração de processamento, **When** a ingestão
   é repetida, inclusive concorrentemente, **Then** não há trechos redundantes disponíveis e
   a repetição é identificada. (FR-010)
2. **Given** nova versão oficial de uma fonte, **When** é incorporada, **Then** a versão anterior
   não é sobrescrita como se fosse a nova e consultas continuam respeitando aplicabilidade.
   (FR-006, FR-011)
3. **Given** versão já disponível, **When** seu reprocessamento falha, **Then** não são misturados
   trechos incompletos com uma geração concluída; a geração anteriormente ativa permanece
   disponível e o resultado da tentativa é registrado.
   (FR-012, FR-018)
4. **Given** mesmo identificador e versão com conteúdo diferente, **When** ocorre nova ingestão,
   **Then** a entrada conflitante é rejeitada e o conteúdo existente permanece inalterado;
   uma atualização de conteúdo somente é aceita com outra versão oficial. Revisão interna
   ou reprocessamento NÃO DEVEM contornar essa rejeição. (FR-011)
5. **Given** fonte retirada de consulta por administrador autorizado, **When** há nova consulta,
   **Then** seus trechos não retornam como evidência ativa e referências anteriores preservam
   identidade e indicação do estado. Durante a retenção aprovada, consumidores autorizados
   conseguem recuperar por essas referências o conteúdo originalmente citado, acompanhado
   da indicação de retirada, sem substituição por outra versão. (FR-013, FR-014)
6. **Given** fonte já disponível com reprocessamento em andamento, **When** ocorre consulta,
   **Then** a geração anteriormente ativa continua disponível até a troca. Após conclusão
   bem-sucedida, a nova geração substitui automaticamente a anterior em conjunto, sem nova
   aprovação e sem misturar gerações no resultado. (FR-012, FR-013)

7. **Given** política configurada para a categoria e prazo ainda não vencido, **When** um
   administrador tenta expurgar conteúdo retirado ou substituído, **Then** a exclusão é negada.
   Após vencimento, somente ação manual de administrador central autorizado pode expurgá-lo,
   com auditoria; ausência de prazo configurado impede expurgo e vencimento não o executa
   automaticamente. (FR-014, FR-015, FR-016)
8. **Given** versão com artefatos retidos, **When** administrador central com permissão de
   auditoria consulta seus metadados, **Then** recebe identificador, categoria, estado, evento,
   política/vencimento quando configurados, elegibilidade, impedimentos e revisão de cada
   artefato, sem conteúdo documental. Sem essa permissão a consulta é negada. A leitura não
   autoriza expurgo; alteração concorrente exige nova consulta após rejeição da revisão.
   (FR-014, FR-015, FR-016)

### User Story 5 - Administrar acesso e avaliar qualidade (Priority: P2)

Como responsável pela base, quero controlar alterações e avaliar exemplos representativos
antes de aceitar uma configuração de recuperação para uso.

**Why this priority**: acesso controlado e avaliação impedem alterações indevidas e parâmetros
adotados sem evidência de qualidade.

**Independent Test**: exercitar a matriz de permissões e executar um conjunto de consultas
com respostas esperadas, versões incompatíveis e perguntas sem resposta.

**Acceptance Scenarios**:

1. **Given** identidade sem permissão administrativa, **When** tenta ingerir, atualizar,
   reprocessar ou retirar fonte, **Then** a ação é negada sem alteração dos dados. (FR-015)
2. **Given** consumidor com permissão somente de consulta, **When** tenta administrar fontes,
   **Then** a permissão de análise não autoriza a alteração. (FR-015)
3. **Given** ação administrativa permitida ou negada, **When** é executada, **Then** é possível
   identificar ator, ação, objeto, data, correlação e resultado sem expor segredos. (FR-016)
4. **Given** configurações candidatas, **When** são avaliadas, **Then** há relatório comparável
   por configuração e exemplos antes da escolha de segmentação e parâmetros. (FR-020, FR-021)
   A avaliação somente DEVE ser aceita com fontes, perguntas e evidências esperadas aprovadas
   pelo responsável fiscal e execução registrada pela equipe técnica.
5. **Given** consumidores autorizados de organizações distintas, **When** consultam a mesma
   fonte e versão, **Then** acessam a base oficial compartilhada; ser administrador de uma
   organização sem permissão administrativa central não permite alterar essa base. (FR-015)
6. **Given** fonte processada com sucesso e ainda não aprovada, **When** um consumidor consulta,
   **Then** seus trechos não são retornados como evidência ativa. (FR-013)
7. **Given** fonte processada com sucesso e aguardando aprovação, **When** administrador
   central aprova explicitamente sua disponibilização, **Then** ela se torna elegível para
   consultas e a aprovação registra ator, data e resultado. O aprovador PODE ser o mesmo
   responsável pela ingestão. (FR-013, FR-015, FR-016)

### Edge Cases

- Versão ausente, desconhecida ou com compatibilidade não comprovada: não presumir a mais recente.
- Mesma passagem em fontes ou versões diferentes: preservar proveniências; repetição textual
  isolada não autoriza apagar uma fonte ou fundir aplicabilidades. (FR-010)
- Documento vazio, corrompido ou com extração sem conteúdo útil: falhar com motivo, sem publicação.
- PDF sem texto extraível ou cujo conteúdo necessário dependa de reconhecimento de imagem:
  recusar como não suportado nesta entrega, sem alegar cobertura de conteúdo não extraído.
- Interrupção, repetição concorrente ou erro no reprocessamento: não expor uma geração incompleta.
  A geração ativa anterior permanece disponível em caso de falha; o sucesso substitui a geração
  automaticamente, sem nova aprovação. Reprocessamento NÃO DEVE reativar fonte retirada.
- Processamento bem-sucedido sem aprovação explícita: manter conteúdo fora da recuperação ativa.
- Localização inexistente: declarar ausência; referência à fonte não substitui essa declaração.
- Referência de fonte retirada: permitir recuperação do conteúdo originalmente citado por
  consumidores autorizados durante a retenção, indicando a retirada; após expurgo, informar
  indisponibilidade sem apontar para outro texto.
- Falha técnica: diferenciar consulta não concluída de busca concluída sem evidência suficiente.
- Ausência de fonte oficial para provar compatibilidade: manter a relação não aprovada.
- Bytes diferentes sob a mesma identidade e versão oficial: rejeitar sem publicar revisão
  interna, alterar o original ou usar reprocessamento como caminho alternativo.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE permitir ingestão e acompanhamento de documentos oficiais por
  administradores autorizados, distinguindo processamento pendente, concluído e falho.
  Nesta entrega, DEVE aceitar somente manuais e guias oficiais em PDF com texto extraível;
  schemas, outros formatos e documentos que exijam reconhecimento de texto em imagens NÃO
  DEVEM ser processados e DEVEM receber indicação de documento não suportado.
- **FR-002**: Cada fonte DEVE possuir origem, identificação, versão e tipo documental de
  aplicação; compatibilidades adicionais DEVEM possuir fundamento oficial registrado.
- **FR-003**: O sistema DEVE validar metadados, formato e condições de processamento antes
  da disponibilização; entradas inválidas NÃO DEVEM produzir conteúdo consultável como concluído.
- **FR-004**: Cada trecho DEVE preservar vínculo à fonte e sua versão, identificação própria
  e localização quando disponível, com ausência explícita quando indisponível.
- **FR-005**: A recuperação DEVE exigir contexto suficiente de tipo documental e versão;
  contexto insuficiente DEVE resultar em limitação, sem seleção presumida.
- **FR-006**: A recuperação DEVE excluir versões incompatíveis; somente versão solicitada ou
  compatibilidade oficialmente documentada DEVE ser elegível, nunca a mais semelhante por conveniência.
- **FR-007**: Resultado sem evidência suficiente DEVE declarar limitação. Evidência parcial
  DEVE ser identificada como parcial; o sistema NÃO DEVE fabricar respostas ou referências.
- **FR-008**: Falha técnica e dependência indisponível DEVEM ser distinguíveis de ausência de
  evidência. O serviço NÃO DEVE emitir conclusão de conformidade fiscal.
- **FR-009**: Toda resposta com evidências DEVE incluir referências resolvíveis para cada
  trecho e fonte; respostas sem evidência DEVEM declarar a limitação sem referências artificiais.
- **FR-010**: Repetição da mesma ingestão NÃO DEVE gerar trechos redundantes consultáveis
  para a mesma fonte, versão e geração de processamento, inclusive sob concorrência. Fontes
  ou versões distintas NÃO DEVEM perder proveniência pela deduplicação textual.
- **FR-011**: Atualização DEVE preservar distinção entre versões. Conteúdo diferente com mesma
  identidade e versão DEVE ser rejeitado com conflito explícito, preservando o conteúdo
  existente. Atualização de conteúdo DEVE exigir outra versão oficial; revisão interna ou
  reprocessamento NÃO DEVEM permitir substituir os bytes da origem sob a mesma identidade
  e versão. Reprocessamento do mesmo conteúdo com outra configuração continua permitido
  conforme FR-012.
- **FR-012**: Reprocessamento DEVE registrar geração e resultado e impedir mistura de gerações
  incompletas com conteúdo disponibilizado como concluído. Para fonte já disponível, a geração
  ativa DEVE permanecer consultável durante o processamento e em caso de falha. Após conclusão
  bem-sucedida, a nova geração DEVE substituir automaticamente a anterior em conjunto, sem
  nova aprovação administrativa; cada resultado de consulta NÃO DEVE misturar essas gerações
  da mesma versão documental. A troca DEVE preservar a identidade das referências anteriores
  conforme FR-014 e registrar a mudança conforme FR-016. Reprocessamento NÃO DEVE reativar
  fonte retirada nem dispensar a aprovação inicial de uma fonte ainda não disponibilizada.
- **FR-013**: Administradores autorizados DEVEM poder disponibilizar e retirar fontes de novas
  consultas. Após ingestão bem-sucedida, a fonte DEVE aguardar aprovação explícita de
  administrador central antes de ficar elegível para recuperação. O aprovador PODE ser o
  responsável pela ingestão; segregação entre essas pessoas NÃO DEVE ser obrigatória.
  Conclusão da ingestão inicial NÃO DEVE disponibilizar a fonte automaticamente; a substituição
  de geração de fonte já disponível DEVE seguir a exceção explícita de FR-012. Conteúdo em
  processamento, falho, aguardando aprovação ou retirado NÃO DEVE aparecer como evidência ativa.
- **FR-014**: Alterações de ciclo de vida DEVEM preservar identidade das referências anteriores
  e informar indisponibilidade quando o conteúdo não puder ser recuperado. A retirada de uma
  fonte DEVE excluí-la de novas buscas, mas NÃO DEVE impedir consumidores autorizados de
  recuperar seu conteúdo originalmente citado por referências anteriores durante a retenção
  aprovada. Essa recuperação DEVE indicar o estado retirado e NÃO DEVE substituir o conteúdo
  citado por outra versão. O mecanismo de retenção DEVE ser implementado de forma
  parametrizada, sem prometer preservação indefinida. Após expurgo, a
  resolução DEVE indicar indisponibilidade sem fabricar evidência.
  Para fontes retiradas e gerações substituídas, a retenção DEVE ter prazo obrigatório
  configurável por categoria, contado da retirada ou substituição correspondente. A exclusão
  definitiva DEVE ser manual, executada por administrador central com permissão explícita,
  somente após vencer o prazo; vencimento isolado NÃO DEVE excluir conteúdo automaticamente.
  Os prazos por categoria e as regras de exclusão DEVEM estar aprovados antes da ativação
  da política e da persistência operacional correspondente. Sem configuração válida, o
  sistema NÃO DEVE permitir expurgo. Testes PODEM utilizar políticas sintéticas explicitamente
  identificadas. Exclusão DEVE ser auditada conforme FR-016.
  Administradores centrais com permissão de auditoria DEVEM poder consultar somente os
  metadados dos artefatos retidos por versão documental: identificador, categoria, estado,
  evento/data, política e vencimento quando configurados, elegibilidade, impedimentos e
  revisão a usar na precondição de expurgo. A leitura NÃO DEVE conceder permissão de exclusão;
  o comando DEVE revalidar autorização, prazo, revisão e dependências na transação do efeito.
- **FR-015**: Ações administrativas DEVEM exigir permissão explícita; permissão de consulta
  NÃO DEVE conceder administração. A base oficial DEVE ser compartilhada entre organizações
  e alterada somente por administradores centrais. Consumidores autorizados DEVEM poder
  consultá-la; administração de uma organização NÃO DEVE implicar administração central.
  Esta feature NÃO DEVE criar bases ou fontes adicionais isoladas por organização.
  Ações não autorizadas NÃO DEVEM alterar conteúdo ou estado.
- **FR-016**: Operações administrativas e consultas DEVEM permitir correlação e auditoria de
  identidade, ação, objeto ou contexto, data e resultado, protegendo dados sensíveis e segredos.
- **FR-017**: Consumidores funcionais DEVEM recuperar conhecimento e resolver referências
  exclusivamente pelo protocolo definido na constitution. NÃO DEVEM acessar dados internos
  ou usar interfaces administrativas como alternativa de consulta funcional.
- **FR-018**: Ingestão, consulta e reprocessamento DEVEM expor resultado e motivo de falha ou
  limitação correlacionáveis; falhas NÃO DEVEM ser apresentadas como operações concluídas.
- **FR-019**: Documentos e trechos DEVEM permanecer dados não confiáveis; suas instruções
  NÃO DEVEM alterar permissões, política do sistema ou restrições de versão.
- **FR-020**: Antes de aceitar parâmetros de segmentação ou recuperação, DEVE haver avaliação
  documentada com fontes e versões identificadas, perguntas representativas, evidências
  esperadas, casos negativos e critérios de aprovação definidos antes da execução.
  O conjunto inicial DEVE avaliar manuais e guias oficiais em PDF com texto extraível;
  schemas e documentos digitalizados NÃO DEVEM compor o escopo de recuperação desta entrega,
  mas DEVEM aparecer entre os casos de rejeição de entrada não suportada.
  O responsável fiscal DEVE selecionar e aprovar fontes e versões oficiais, aplicabilidade,
  perguntas e evidências esperadas. A equipe técnica DEVE organizar o conjunto versionado,
  preparar os testes e executar a avaliação, preservando o registro da aprovação fiscal.
  Preparação técnica NÃO DEVE substituir a aprovação fiscal das expectativas.
- **FR-021**: O relatório de avaliação DEVE identificar conjunto usado, configurações candidatas,
  resultados e justificativa da escolha. Tamanho de trecho, quantidade recuperada e limiar
  NÃO DEVEM ser fixados sem essa evidência.

- **FR-022**: A ingestão DEVE gerar embeddings dos trechos por um provedor de IA configurado;
  a recuperação DEVE gerar o embedding da consulta em espaço compatível e ordenar candidatos
  por similaridade, após aplicar a elegibilidade fiscal. Fixtures fake não comprovam a integração
  real nem aprovam parâmetros. Avaliação continua regida por FR-020–021.
### Restrições de qualidade e organização herdadas

- **QR-001 — DDD**: A feature DEVE seguir DDD no contexto de Conhecimento Fiscal, com
  vocabulário compartilhado para fonte, versão documental, versão de layout, aplicabilidade,
  geração, trecho, referência e publicação. O desenho DEVE explicitar responsabilidades,
  invariantes e limites de consistência, distinguindo conhecimento oficial de análise fiscal
  e memória conversacional. A versão de um manual NÃO DEVE ser confundida com a de um layout.
- **QR-002 — Clean Code**: Casos de uso e regras DEVEM ter responsabilidades e nomes claros,
  localização previsível, dependências explícitas e tratamento de falhas verificável. Regras
  de elegibilidade, publicação e preservação de referências NÃO DEVEM ser duplicadas entre
  canais de acesso; DEVEM ser testáveis sem depender de transporte ou serviços externos.
- **QR-003 — Arquitetura ágil**: A entrega DEVE evoluir por incrementos verificáveis,
  rastreáveis às histórias e aos riscos atuais. Cada decisão arquitetural relevante DEVE ter
  ADR com alternativas, consequências e condições de revisão. Boa organização do código
  DEVE ser preservada em cada incremento; estruturas extras DEVEM ter necessidade demonstrada.

**Aceite arquitetural:** a revisão DEVE apresentar contexto e glossário coerentes com as
entidades abaixo, vínculo das invariantes a testes, revisão de organização e dependências,
e evidência de formatação e análise estática configuradas no plano. Refatorações DEVEM ter
validação de comportamento. Esses critérios complementam SC-001–010 e NÃO DEVEM ser usados
como substitutos dos resultados funcionais ou como escolhas de bibliotecas e pastas nesta spec.

### Key Entities *(include if feature involves data)*

- **Fonte oficial**: documento de origem verificável, identificação e natureza (manual, guia
  nesta entrega); relaciona-se a versões documentais. Schemas permanecem fora do escopo inicial.
- **Versão documental**: versão identificada da fonte, conteúdo e aplicabilidade por tipo e
  versão do documento analisado; seu estado controla elegibilidade para novas consultas.
  Processamento concluído e aprovação de disponibilização DEVEM ser distinguíveis; a aprovação
  DEVE identificar administrador central e data, inclusive quando ele também realizou a ingestão.
- **Compatibilidade documentada**: relação de aplicabilidade fundamentada em fonte oficial;
  não é deduzida apenas por semelhança textual ou ordenação de versões.
- **Trecho (chunk)**: unidade recuperável de conteúdo, com identidade, versão da fonte,
  geração de processamento, referência e localização disponível ou ausência explícita.
- **Operação de ingestão ou reprocessamento**: identidade, ator, fonte, geração, estado,
  resultado e correlação; repetição não cria redundância ativa.
- **Consulta e resultado**: contexto de tipo e versão, identidade autorizada, evidências,
  referências, limitações e resultado técnico distinguível.
- **Referência**: identidade recuperável de fonte, versão e trecho; localização não é
  fabricada e indisponibilidade posterior não muda o conteúdo originalmente referenciado.
  Retirada de novas buscas e acesso histórico por referência DEVEM ser distinguíveis:
  consumidores autorizados mantêm acesso ao conteúdo citado durante a retenção aprovada.
- **Avaliação**: conjunto versionado de exemplos, expectativas, critérios, configurações
  candidatas, resultados e decisão de adoção.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% dos casos de aceite com versões incompatíveis na base, nenhum trecho
  incompatível é retornado ao consumidor. (FR-005, FR-006)
- **SC-002**: Repetir a mesma ingestão nos casos sequenciais e concorrentes do conjunto de
  aceite produz zero trechos redundantes ativos para a mesma fonte e versão. (FR-010)
- **SC-003**: Em 100% das respostas de aceite com evidências, cada trecho possui referência
  resolvível, origem, identificação e versão; localização ausente é declarada em todos os
  casos correspondentes, com zero localizações inventadas. (FR-004, FR-009, FR-014)
  Em 100% dos casos de retirada durante a retenção, referências anteriores permitem ao
  consumidor autorizado conferir o conteúdo citado e seu estado retirado, sem incluí-lo
  em novas buscas; após expurgo, indicam indisponibilidade sem trocar a evidência.
  Todos os casos de expurgo antes do prazo, sem prazo ou sem permissão central são negados;
  vencimento não causa exclusão automática, e expurgo manual permitido gera auditoria.
  Nos casos de descoberta administrativa, identificadores e revisões permitem endereçar o
  artefato correto sem expor conteúdo; consulta sem permissão e expurgo com revisão desatualizada
  são negados, mesmo quando uma leitura anterior informou elegibilidade.
- **SC-004**: Em 100% dos casos negativos previstos, o consumidor distingue evidência
  insuficiente de falha técnica, sem conclusão de conformidade ou referência fabricada.
  (FR-007, FR-008, FR-018)
- **SC-005**: Todas as tentativas administrativas sem permissão da matriz de aceite são
  negadas sem mudanças de dados; todas as operações previstas têm registro auditável.
  Consumidores autorizados de organizações distintas consultam a mesma base oficial, e
  administradores organizacionais sem permissão central não conseguem alterá-la.
  Em 100% dos casos de ingestão bem-sucedida ainda não aprovada, nenhum trecho é retornado
  como evidência ativa; aprovação pelo próprio administrador central que ingeriu é permitida.
  (FR-015, FR-016)
- **SC-006**: Em todos os casos de atualização, conflito e falha de reprocessamento previstos,
  versões permanecem distinguíveis e zero gerações incompletas são expostas como concluídas.
  Nos casos de reprocessamento de fonte disponível, a geração anterior permanece consultável
  até a troca e em caso de falha; o sucesso substitui a geração sem nova aprovação e sem
  mistura de gerações em uma resposta. Todos os casos de conteúdo diferente sob a mesma
  identidade e versão oficial DEVEM ser rejeitados sem alteração da origem existente.
  (FR-001 a FR-003, FR-011 a FR-014, FR-018)
- **SC-007**: Antes da adoção de parâmetros, existe ao menos um relatório comparativo com
  exemplos representativos e critérios previamente definidos; todos os critérios obrigatórios
  do conjunto aprovado são atendidos. O relatório DEVE identificar a aprovação fiscal das
  fontes, perguntas e evidências esperadas e a execução pela equipe técnica. (FR-020, FR-021)
  O relatório cobre manuais e guias em PDF com texto extraível; em 100% dos casos de entrada
  fora desse escopo, o documento é recusado sem gerar evidências consultáveis. (FR-001, FR-003)
- **SC-008**: Todos os cenários de consulta e resolução de referências de aceite respeitam
  o canal funcional constitucional e nenhum conteúdo documental altera permissões ou filtros.
  (FR-017, FR-019)

- **SC-009**: Com base contendo 300 PDFs do escopo suportado e 5 consultas simultâneas,
  todas as consultas DEVEM concluir preservando isolamento de versões, referências e
  classificação de limitações, sem falhas causadas exclusivamente por essa concorrência.
  O teste DEVE registrar duração e características dos PDFs usados. O perfil não estabelece
  por si só limite máximo de arquivo ou autorização para rejeitar o 301º PDF; a meta de latência
  é definida separadamente em SC-010.

- **SC-010**: No perfil de 300 PDFs e 5 consultas simultâneas, pelo menos 95% das consultas
  DEVEM concluir a recuperação completa em até 5 segundos, incluindo a obtenção de embeddings
  da pergunta quando necessária e a devolução das evidências, referências e limitações.
  A medição DEVE ocorrer pelo cliente consumidor, do envio da consulta até o recebimento da
  resposta completa. Geração de parecer pelo LLM fica excluída. Falhas técnicas e timeouts
  NÃO DEVEM ser descartados do relatório nem contados como consultas concluídas dentro da meta.
  O protocolo de teste DEVE registrar amostra, ambiente e características do corpus antes da execução.

## Assumptions

### Escopo e limites

- Inclui ingestão administrativa, ciclo de vida, recuperação de evidências, referências e
  avaliação. Não inclui parecer fiscal, validação de arquivos SPED do contribuinte, memória
  conversacional ou exposição de agentes a orquestradores.
- A primeira entrega e sua avaliação de recuperação abrangem somente manuais e guias oficiais
  em PDF com texto extraível. Schemas, outros formatos e documentos digitalizados que exigem
  reconhecimento de texto ficam para outra entrega, conforme esclarecimento D12.
- O serviço fornece evidências e limitações; não determina conformidade tributária.
- A funcionalidade não exige uma interface gráfica; o meio administrativo será definido no plano.
- A base oficial é compartilhada entre organizações, com administração exclusivamente central
  e consulta por consumidores autorizados, conforme esclarecimento D07. Esta decisão não
  autoriza acesso cruzado a identidades, consultas ou outros dados operacionais de usuários.
- Não se presume compatibilidade entre versões. Relações diferentes da versão solicitada
  exigem fundamento oficial registrado, conforme FR-006.
- A disponibilização inicial exige aprovação central explícita e permite que o responsável
  pela ingestão aprove. Reprocessamento de fonte disponível substitui automaticamente a
  geração após sucesso; falha mantém a anterior. Retirada exclui a fonte das novas buscas,
  preservando acesso autorizado ao conteúdo citado por referências durante a retenção.
  Retenção de fontes retiradas e gerações substituídas segue prazo por categoria e expurgo
  manual central após vencimento. Valores dos prazos permanecem pendentes. Conteúdo conflitante
  sob a mesma identidade e versão é rejeitado; atualização exige outra versão oficial.
  Este documento permanece rascunho até revisão.

### Dependências e restrições herdadas

- A constitution em `.specify/memory/constitution.md` define o protocolo funcional, propriedade
  dos dados, tecnologias obrigatórias e proteção contra instruções em conteúdo documental.
  Esta especificação não redefine essas escolhas nem detalha sua implementação.
- A constitution 1.1.0 torna DDD, Clean Code e arquitetura ágil obrigatórios. QR-001–003
  registram o impacto nesta feature; o plano DEVE definir organização e verificações concretas.
- O responsável fiscal deve selecionar fontes oficiais e suas versões e documentar relações
  de aplicabilidade para o conjunto inicial, antes de aprovar a avaliação D12. Nenhum layout,
  regra fiscal ou schema é inventado por esta especificação.
- Disponibilidade de fontes legíveis e de identidades autorizáveis é dependência da entrega;
  não se presume fornecedor de identidade ou política organizacional existente.
- O perfil inicial aprovado é de 300 PDFs e 5 consultas simultâneas, conforme SC-009.
  A meta aprovada é recuperação completa em até 5 segundos em pelo menos 95% das consultas,
  conforme SC-010. Tamanho e páginas por PDF e crescimento ainda não foram definidos;
  o plano DEVE registrar essas características antes dos testes de capacidade.

### Decisões pendentes

Os identificadores abaixo foram fornecidos para esta feature; não correspondem a uma
numeração já existente na constitution. Decisões não resolvidas bloqueiam as tarefas dependentes.

| ID | Decisão não aprovada | Etapa de resolução |
| --- | --- | --- |
| D04 | Segmentação, identidade/deduplicação de trechos, embeddings, compatibilidade de representações, busca, tamanho, topK e limiar. | Plano técnico e ADR para decisões relevantes; avaliar candidatos com D12 antes de fixar parâmetros para aceitação. |
| D07 (parcialmente resolvida) | Aprovado: base oficial compartilhada, administração central, consulta autorizada e disponibilização inicial após aprovação explícita de administrador central, que pode ser o responsável pela ingestão. Reprocessamento de fonte disponível mantém a geração ativa até substituição automática após sucesso, sem nova aprovação; falha preserva a geração anterior. Retirada exclui a fonte das novas buscas e preserva recuperação autorizada do conteúdo citado por referências anteriores durante a retenção, com indicação de retirada. Aprovado nesta rodada: prazo obrigatório por categoria de fonte retirada/geração substituída, contado da retirada/substituição; expurgo manual central somente após vencimento, sem exclusão automática. Pendentes: valores dos prazos antes de ativar a política, retenção de auditoria/consultas e matriz detalhada de permissões. | Esclarecimentos para política; plano/ADR para mecanismo de identidade e controle, antes da implementação administrativa e de persistência. |
| D12 (parcialmente resolvida) | Aprovado: escopo inicial de manuais e guias oficiais em PDF com texto extraível; schemas e documentos digitalizados ficam para outra entrega. Aprovado nesta rodada: responsável fiscal seleciona e aprova fontes, perguntas e evidências esperadas; equipe técnica organiza e executa a avaliação. Pendentes: identificação dos responsáveis designados, títulos e versões oficiais específicos, exemplos, referências esperadas, métricas e critérios de aprovação. | Responsável fiscal deve selecionar fontes e expectativas antes de aprovar o conjunto; plano de avaliação deve registrar critérios antes de executar avaliações e aceitar D04. |
| D11 | Topologia de persistência, segregação de acesso, retenção técnica e resolução durável de referências. | Plano técnico e ADR antes da implementação de persistência, respeitando propriedade dos dados e tecnologias constitucionais. |
