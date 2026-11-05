# Constitution do AgenteSpedContrib

## 1. Propósito e escopo

O AgenteSpedContrib tem por objetivo analisar arquivos SPED Contribuições, validar sua
estrutura e examinar créditos, débitos, alíquotas e cruzamentos, produzindo análises e
relatórios de conformidade fundamentados em evidências recuperáveis.

A solução DEVE combinar validação determinística, consulta a manuais por RAG, ferramentas
externas controladas, análise com LLM e respostas estruturadas rastreáveis. DEVE prever
integração com outros agentes e orquestradores por A2A.

Esta constitution DEVE orientar especificações, arquitetura, planos, tarefas, implementações
e revisões. DEVE expressar regras duradouras; detalhes de implementação, versões de SDKs e
parâmetros ajustáveis DEVEM permanecer nos planos técnicos.

DEVE indica obrigação; NÃO DEVE indica proibição; PODE indica opção sujeita às demais regras.
Decisões pendentes NÃO DEVEM ser tratadas como requisitos aprovados.

## 2. Princípios obrigatórios numerados

### 1. Autoridade documental

**Regra:** layouts, regras fiscais, schemas e critérios de conformidade DEVEM ser obtidos de
documentos oficiais identificados e versionados e registrados nas especificações. Cada regra
DEVE indicar fonte e aplicabilidade. O projeto NÃO DEVE inventar esses elementos nem converter
inferências do modelo em regras fiscais. Ambiguidades DEVEM ser esclarecidas antes da regra afetada.

**Justificativa:** conclusões tributárias precisam de fundamento verificável e contexto de aplicação.

**Verificação:** revisar vínculos entre requisito, fonte oficial, versão, regra e testes;
rejeitar regras sem fundamento registrado.

### 2. Validação determinística

**Regra:** verificações estruturais e regras de negócio determinísticas DEVEM ser executadas
por validadores ou ferramentas verificáveis. O LLM NÃO DEVE substituí-las. XSD DEVE ser
aplicado somente a XML com schema correspondente identificado. Outros formatos DEVEM usar
validadores compatíveis com seus layouts. O agente NÃO DEVE declarar conformidade quando
qualquer validação obrigatória não tiver sido concluída.

**Justificativa:** verificações reproduzíveis não podem depender de geração probabilística.

**Verificação:** testar documentos válidos, inválidos, versões desconhecidas e validações
incompletas; comprovar o bloqueio de declaração de conformidade nos casos impeditivos.

### 3. Fluxo rastreável e classificação honesta

**Regra:** cada execução DEVE seguir o fluxo da seção 3, registrando etapas concluídas,
falhas e validações não realizadas. Resultados DEVEM distinguir erro estrutural, inconsistência
de negócio, indício identificado pela IA, ausência de evidências e falha técnica ou dependência
indisponível. Essas categorias NÃO DEVEM ser intercambiadas para ocultar falhas ou transformar
indícios em constatações.

**Justificativa:** consumidores precisam distinguir defeitos do documento de limites da análise.

**Verificação:** testar cada categoria e interrupções do fluxo; rastrear resultados até
validadores, ferramentas, fontes e chamadas correspondentes.

### 4. Limites de serviço e simplicidade

**Regra:** Api.Agente e Api.RAG DEVEM ser proprietárias de seus dados. A comunicação funcional
entre elas DEVE ocorrer por MCP. Api.Agente NÃO DEVE acessar diretamente tabelas ou vetores
internos da Api.RAG. Endpoints, aplicação, domínio e infraestrutura DEVEM permanecer separados.
Projetos ou bibliotecas adicionais DEVEM ter necessidade, responsabilidade e benefício justificados.

**Justificativa:** limites explícitos permitem evolução independente sem complexidade arbitrária.

**Verificação:** inspecionar dependências, credenciais, consultas e testes de integração;
confirmar uso de MCP e ausência de acesso cruzado aos dados internos.

### 5. Conhecimento aplicável e recuperável

**Regra:** documentos e chunks DEVEM preservar origem, identificação, versão e localização
quando disponível. Recuperação DEVE respeitar tipo de documento e versão do manual aplicável.
Respostas fundamentadas no RAG DEVEM apresentar referências recuperáveis e declarar insuficiência
de evidências. Segmentação, deduplicação, reprocessamento, atualização e busca DEVEM ser
especificados e avaliados com exemplos representativos. Tamanho de chunks, topK e limiares
NÃO DEVEM ser fixados arbitrariamente.

**Justificativa:** similaridade textual isolada não comprova aplicabilidade normativa.

**Verificação:** avaliar fontes esperadas, versões distintas e evidência insuficiente;
confirmar resolução das referências e documentação dos parâmetros avaliados.

### 6. Saída estruturada validada

**Regra:** a integração DEVE usar OpenAI, modelo e parâmetros externos à implementação,
contrato JSON explícito e versionado e mecanismo de saída estruturada compatível com o modelo.
Estrutura e regras semânticas DEVEM ser validadas antes da aceitação. Retentativas DEVEM ter
limite e tratamento de esgotamento. O LLM NÃO DEVE fabricar evidências, resultados de ferramentas,
referências ou metadados operacionais.

**Justificativa:** JSON sintaticamente válido não garante uma análise consistente ou fundamentada.

**Verificação:** testar JSON inválido, violações semânticas, referências inexistentes, recusa,
resposta incompleta e esgotamento de retentativas; rejeitar resultados não validados.

### 7. Ferramentas com autorização independente

**Regra:** Function Calling DEVE ser integrado e executado por Semantic Kernel. Cada ferramenta
DEVE possuir nome, responsabilidade, contratos de entrada e saída, validação de argumentos,
autenticação, autorização, timeout, tratamento de falhas, política de retentativa, testes e
registros de execução. Idempotência DEVE ser definida quando aplicável; inaplicabilidade
DEVE ser justificada. A seleção pelo LLM NÃO DEVE conceder autorização para execução.

**Justificativa:** uma sugestão do modelo não representa permissão para produzir efeitos.

**Verificação:** testar argumentos inválidos, acesso negado, timeout, falha e repetição;
comprovar que autorização antecede os efeitos da ferramenta.

### 8. Dados não confiáveis e proteção por contexto

**Regra:** arquivos, chunks, memória e saídas externas DEVEM ser tratados como dados não
confiáveis e NÃO DEVEM alterar instruções do sistema, permissões ou políticas de execução.
Acesso DEVE respeitar usuário, conversa e organização aplicável. Dados sensíveis DEVEM ser
protegidos na entrada, transmissão, persistência e observabilidade.

**Justificativa:** conteúdo documental pode conter instruções maliciosas e dados de terceiros.

**Verificação:** testar injeção de instruções e acesso entre contextos; revisar permissões,
segredos, logs e dados enviados ao provedor.

### 9. Memória limitada e sem autoridade normativa

**Regra:** memória conversacional DEVE usar MongoDB ou Redis, mediante ADR anterior à
implementação. DEVE haver isolamento por usuário e conversa, e organização quando aplicável,
retenção, expiração, exclusão, proteção de dados sensíveis e limite de histórico enviado ao
modelo. Histórico DEVE permanecer separado do conhecimento oficial e NÃO DEVE adquirir sua
autoridade sem atender aos critérios documentais e de ingestão aprovados.

**Justificativa:** continuidade de conversa não confere validade normativa ao conteúdo armazenado.

**Verificação:** revisar ADR e políticas; testar isolamento, expiração, exclusão e limites
de recuperação e envio de histórico ao modelo.

### 10. Consumo e custos auditáveis

**Regra:** consumo DEVE ser persistido em PostgreSQL por chamada, incluindo embeddings,
chamadas adicionais e retentativas. Estimativas DEVEM ser distintas dos valores efetivamente
informados pelo provedor. Consumo desconhecido NÃO DEVE ser registrado como zero. Estimativas
DEVEM indicar referência da tabela de preços e método de cálculo.

**Justificativa:** totais com tentativas omitidas ou lacunas ocultas não permitem auditoria.

**Verificação:** reconciliar execuções, chamadas e tentativas; testar métricas ausentes,
falhas e mudanças de preços, preservando a referência usada no cálculo.

### 11. Contratos e interoperabilidade

**Regra:** contratos REST, MCP, A2A, ferramentas e saída estruturada DEVEM ser explícitos e
versionados. Mudanças incompatíveis DEVEM registrar impacto e estratégia de transição.
Api.Agente DEVE expor o agente por servidor A2A na etapa definida na especificação. Contratos
e segurança dessa exposição DEVEM ser definidos antes de sua implementação.

**Justificativa:** consumidores e agentes externos precisam de comportamento estável e verificável.

**Verificação:** revisar contratos e testes de compatibilidade; confirmar etapa A2A rastreável
com critérios de aceite, sem presumir que essa integração já esteja implementada.

### 12. SDD com evidência de conclusão

**Regra:** funcionalidades DEVEM seguir a seção 6, possuir critérios de aceite verificáveis
e rastreabilidade entre requisitos, tarefas e testes. Decisões arquiteturais relevantes DEVEM
ter ADR. PRs DEVEM demonstrar conformidade constitucional ou apresentar desvio aprovado.

**Justificativa:** intenção documentada e evidências de validação permitem revisão objetiva.

**Verificação:** inspecionar vínculos entre artefatos, resultados de validação e tratamento
de pendências bloqueantes e desvios antes de concluir a funcionalidade.

### 13. DDD e linguagem de domínio

**Regra:** o projeto DEVE seguir Domain-Driven Design (DDD), modelando o software a partir
dos conceitos, comportamentos e invariantes do domínio. Cada funcionalidade DEVE identificar
seu contexto delimitado, vocabulário compartilhado e relações com outros contextos. A mesma
linguagem DEVE ser usada em especificações, modelos, código e testes. Regras e invariantes
DEVEM permanecer no domínio; casos de uso DEVEM coordená-las na aplicação, e adaptadores
DEVEM concentrar detalhes externos. Entidades, objetos de valor e agregados DEVEM ser usados
quando sua semântica justificar identidade, imutabilidade ou limites de consistência; adoção
ou inaplicabilidade DEVE ser explicada no desenho. DDD NÃO DEVE impor um microserviço por
contexto nem padrões táticos sem necessidade demonstrada.

**Justificativa:** um modelo explícito preserva o significado das regras ao longo da evolução técnica.

**Verificação:** revisar mapa de contextos, glossário, responsabilidades e limites de consistência;
confirmar que invariantes possuem testes independentes de HTTP, MCP, banco e provedor de IA.

### 14. Clean Code e organização do código

**Regra:** o projeto DEVE seguir Clean Code, com nomes que expressem intenção e vocabulário
do domínio, responsabilidades coesas e dependências explícitas. A organização DEVE permitir
localizar cada caso de uso, sua regra, contrato, adaptadores e testes. Endpoints e ferramentas
NÃO DEVEM concentrar regras de domínio ou acesso direto à persistência. Regras compartilhadas
NÃO DEVEM ser duplicadas; reaproveitamento DEVE preservar significado e responsabilidade.
Código morto e abstrações sem uso justificado NÃO DEVEM ser introduzidos. Tratamento de erros
DEVE ser explícito; falhas NÃO DEVEM ser silenciosamente suprimidas. Comentários DEVEM explicar
motivações ou restrições que o código não expressa, sem substituir nomes e estrutura claros.

**Justificativa:** código legível e organizado reduz o custo de revisão, correção e evolução.

**Verificação:** PRs DEVEM verificar nomenclatura, coesão, direção de dependências, duplicações,
tratamento de falhas e localização das regras; análise estática e formatação DEVEM seguir a
configuração registrada no plano. Limites quantitativos só DEVEM ser adotados com justificativa.

### 15. Arquitetura ágil e evolução incremental

**Regra:** a arquitetura DEVE evoluir em incrementos alinhados a requisitos e riscos atuais,
priorizando boa organização do código, testabilidade e capacidade de mudança. Decisões
relevantes DEVEM registrar alternativas, consequências e condições de revisão em ADR.
Refatorações DEVEM preservar comportamentos cobertos por testes e atualizar artefatos afetados.
Novas camadas, serviços, bibliotecas ou padrões DEVEM demonstrar benefício para um caso de uso
ou risco identificado. Complexidade para necessidades hipotéticas NÃO DEVE ser adicionada.
A evolução incremental NÃO DEVE dispensar limites de domínio, critérios de qualidade ou SDD.

**Justificativa:** decisões proporcionais e revisáveis permitem adaptação sem desorganizar a solução.

**Verificação:** cada incremento DEVE ser rastreável a requisitos ou riscos, possuir resultado
verificável e registrar decisões relevantes; revisar impactos, testes de regressão e ausência
de estruturas sem responsabilidade demonstrada.

## 3. Restrições arquiteturais

A solução DEVE se chamar AgenteSpedContrib e usar .NET 10 com ASP.NET Core. A separação de
camadas PODE ser lógica dentro de um projeto; projetos extras NÃO DEVEM ser criados apenas
para reproduzir nomes de camadas. O domínio NÃO DEVE depender de endpoints, bancos, SDKs
de LLM ou transportes externos.

A organização DEVE combinar limites de contexto DDD com separação de responsabilidades por
camada. A aplicação DEVE depender do domínio; infraestrutura DEVE implementar portas definidas
pelo domínio ou pela aplicação. Endpoints DEVEM invocar casos de uso por contratos, sem acoplar
regras a SDKs ou bancos. O ponto de composição PODE conhecer adaptadores para configurar
dependências. Contextos, camadas e funcionalidades DEVEM ter localização e convenções
documentadas no plano; separar pastas, por si só, NÃO DEVE ser considerado evidência de DDD.

| Serviço | Responsabilidades obrigatórias |
| --- | --- |
| Api.Agente | API REST de execução; recebimento de arquivos e solicitações; orquestração das validações; integração OpenAI; cliente MCP da Api.RAG; ferramentas por Semantic Kernel e Function Calling; memória conversacional; consumo e custos; validação da resposta estruturada; servidor A2A. |
| Api.RAG | API REST de administração da base; endpoint POST de ingestão; processamento de documentos, chunks e embeddings; armazenamento e consulta em PostgreSQL com PgVector; servidor MCP de recuperação de conhecimento. |

REST administrativo da Api.RAG NÃO DEVE substituir MCP na comunicação funcional com
Api.Agente. Cada serviço DEVE controlar seus dados e credenciais. Infraestrutura física
PODE ser compartilhada com segregação de acesso, sem acesso direto a dados internos alheios.

Cada análise DEVE seguir a ordem lógica:

1. Receber arquivo e identificar usuário, contexto e execução.
2. Validar tamanho, formato e condições de processamento.
3. Identificar tipo de documento e versão do layout.
4. Validar estrutura, registros e schema aplicável e executar regras determinísticas previstas.
5. Consultar por MCP os chunks do manual correspondente.
6. Executar ferramentas externas quando necessárias e autorizadas.
7. Produzir a análise com o LLM.
8. Validar estrutura e semântica da saída.
9. Retornar resultado com evidências e rastreabilidade.

Falhas impeditivas DEVEM interromper etapas dependentes e produzir resposta controlada com
validações não realizadas. Ferramentas PODEM ser dispensadas quando desnecessárias, com motivo
registrado. Documento ou versão não suportados NÃO DEVEM usar layout presumido. O plano DEVE
definir resultados parciais sem permitir conformidade com verificações obrigatórias pendentes.

## 4. Contratos e governança dos dados

O contrato da análise DEVE contemplar, conforme a especificação: identificador da execução,
status do processamento, resultado da análise, inconsistências e severidades, evidências,
referências, limitações, validações não realizadas e necessidade de revisão humana.
A especificação DEVE definir semântica de status, categorias, severidades e revisão humana,
sem inventar critérios fiscais.

O JSON produzido pelo LLM DEVE ser distinto do envelope da API. O sistema DEVE atribuir e
preservar o identificador da execução e preencher consumo, custos, tempos, correlação e outros
metadados operacionais. Esses valores NÃO DEVEM depender de geração pelo modelo. Referências
e resultados alegados pelo LLM DEVEM ser confrontados com evidências efetivamente disponíveis.

Fontes DEVEM registrar origem oficial, identificação, versão e aplicabilidade. Chunks DEVEM
vincular fonte e localização quando disponível; ausência de localização DEVE ser explícita.
Ingestão DEVE documentar segmentação, deduplicação, reprocessamento e atualização, preservando
a identificação das versões usadas nas análises. Modelo de embeddings, dimensão, indexação
e estratégia de busca DEVEM ser compatíveis e documentados. Mudanças incompatíveis DEVEM
prever reprocessamento e transição antes de misturar representações na recuperação.

Consumo DEVE registrar por chamada, quando disponibilizado pelo provedor:

- Execução, correlação e identificação da chamada ou tentativa.
- Provedor, modelo, tokens de entrada e saída e consumo de embeddings quando aplicável.
- Data, duração e status, inclusive falhas e retentativas.
- Estimativa de custo, referência da tabela de preços e valores efetivamente informados.

Ausência de informação DEVE ser explícita. Propriedade dos registros de consumo de cada
serviço e consolidação DEVEM ser definidas no plano, preservando propriedade dos dados e
auditoria de todas as tentativas.

Políticas de dados DEVEM abranger arquivos, resultados, memória, chunks, métricas e registros,
com responsáveis, acesso, retenção e exclusão definidos antes da persistência correspondente.
Histórico conversacional e conhecimento oficial DEVEM permanecer distintos na recuperação
e na atribuição de autoridade às fontes.

## 5. Segurança e qualidade

Endpoints, transportes e ferramentas DEVEM aplicar autenticação e autorização definidas no
plano. Administração e ingestão DEVEM ter permissões próprias; analisar NÃO DEVE implicar
permissão para alterar fontes. Segredos DEVEM ser externos ao código e NÃO DEVEM aparecer
em logs, prompts ou respostas. Dados sensíveis DEVEM ter proteção em trânsito e armazenamento
conforme o plano aprovado.

Arquivos DEVEM ter limites e condições validados antes do processamento dependente.
Parsers e validadores NÃO DEVEM executar instruções embutidas nos documentos. Conteúdo externo
NÃO DEVE alterar políticas de ferramentas ou contornar autorização. Envio a provedores DEVE
limitar-se ao necessário e respeitar a política de proteção de dados. Logs DEVEM permitir
auditoria sem exposição desnecessária de conteúdo fiscal.

Dependências externas DEVEM ter timeout, retentativas limitadas e comportamento de falha
documentados. Falha técnica NÃO DEVE ser convertida em resultado fiscal.

Validação DEVE incluir testes das regras determinísticas, contratos e integrações alteradas.
Avaliação RAG e LLM DEVE usar exemplos representativos e critérios de aceite especificados.
Casos DEVEM abranger, conforme o escopo, entradas inválidas, versões distintas, evidência
insuficiente, falhas externas, isolamento e injeção de instruções. Dados de teste DEVEM ser
sintéticos ou protegidos pela política de dados. Critérios e tolerâncias para saídas
probabilísticas DEVEM preceder a avaliação; resposta isolada NÃO DEVE comprovar qualidade.

## 6. Fluxo de desenvolvimento SDD

O fluxo obrigatório DEVE ser:

constitution → especificação → esclarecimentos → plano técnico → tarefas → implementação → validação.

1. Especificação DEVE delimitar escopo, fontes, versões, regras, dependências e critérios de
   aceite com identificadores rastreáveis de requisitos.
2. Esclarecimentos DEVEM resolver ambiguidades bloqueantes; quando não existirem, a etapa
   DEVE registrar essa conclusão. Suposições NÃO DEVEM substituir decisões aprovadas.
3. Plano DEVE demonstrar conformidade constitucional, arquitetura, contratos, dados,
   segurança, falhas e estratégia de testes. ADRs DEVEM registrar decisões relevantes,
   alternativas, justificativa e consequências.
   DEVE também documentar contextos delimitados, glossário, invariantes, direção de
   dependências, organização dos casos de uso e critérios de Clean Code para revisão.
4. Tarefas DEVEM indicar dependências, resultado verificável e vínculos aos requisitos e
   validações. Trabalho sem requisito correspondente DEVE motivar ajuste de escopo.
5. Implementação DEVE seguir tarefas e contratos aprovados. Mudanças de requisito ou
   arquitetura DEVEM atualizar artefatos afetados antes da aceitação.
6. Validação DEVE produzir evidências por critério de aceite. PRs DEVEM indicar requisitos
   atendidos, decisões relevantes, validação realizada, pendências e desvios.

Decisões pendentes DEVEM ser resolvidas antes da implementação das tarefas dependentes.
Trabalho independente PODE prosseguir quando a pendência e os limites estiverem explícitos.

## 7. Definition of Done

Uma funcionalidade somente DEVE ser concluída com evidência dos itens aplicáveis abaixo.
Inaplicabilidade DEVE ser justificada na revisão.

- Especificação, esclarecimentos, plano e tarefas atualizados e rastreáveis.
- Critérios de aceite verificados e testes e avaliações exigidos aprovados.
- Regras fiscais e estruturais vinculadas a fontes oficiais e versões aplicáveis.
- Validadores determinísticos e categorias de resultados verificados.
- Contratos alterados versionados e validados, com transição para incompatibilidades.
- RAG com referências recuperáveis e declaração de evidência insuficiente.
- Saída LLM validada estrutural e semanticamente, sem conformidade indevida em falhas.
- Ferramentas com argumentos, autorização, falhas e repetição verificados.
- Isolamento, proteção, retenção e exclusão verificados no escopo alterado.
- Consumo incluindo chamadas adicionais e retentativas, sem desconhecidos convertidos em zero.
- Correlação entre execução, etapas, fontes, ferramentas e chamadas demonstrada.
- ADRs e documentação operacional afetada atualizados.
- Contextos, vocabulário, invariantes e limites de consistência do domínio documentados e testados.
- Organização, nomenclatura, coesão, dependências e tratamento de erros revisados conforme Clean Code.
- Formatação e análise estática configuradas para o incremento verificadas; refatorações com
  comportamento preservado e estruturas adicionais justificadas por requisitos ou riscos atuais.
- Pendências bloqueantes resolvidas e desvios formalizados conforme a seção 8.
- Revisão do PR com resultado da verificação de conformidade constitucional registrado.

## 8. Governança e alterações

Esta constitution DEVE prevalecer sobre decisões técnicas conflitantes em especificações,
planos, tarefas e código. PRs DEVEM verificar regras aplicáveis. Incompatibilidades DEVEM
ser corrigidas ou ter desvio formal aprovado antes da integração.

Desvios DEVEM registrar regra afetada, justificativa, impacto, responsável, aprovação
identificável e escopo. Desvios temporários DEVEM incluir prazo de revisão e condições de
encerramento; após o prazo, DEVEM ser reavaliados antes de novo trabalho dependente.
Desvios NÃO DEVEM alterar silenciosamente a constitution nem autorizar evidências fabricadas
ou conformidade com validações obrigatórias incompletas.

Alterações DEVEM ter proposta revisável, motivação, comparação de regras, versão, data,
avaliação de impacto nas especificações existentes e plano de adequação quando necessário.
Aprovação DEVE identificar responsáveis definidos na governança. Enquanto esses responsáveis
não estiverem definidos, ratificação e desvios dependentes NÃO DEVEM ser presumidos aprovados.

Versionamento DEVE seguir MAJOR.MINOR.PATCH: MAJOR para remoção ou redefinição incompatível
de obrigações; MINOR para novos princípios ou ampliação normativa compatível; PATCH para
esclarecimentos sem mudança de obrigação. A versão inicial DEVE ser 1.0.0. Data original de
ratificação DEVE ser preservada após confirmação; data de alteração DEVE refletir a última
revisão normativa. Impactos DEVEM identificar artefatos, responsáveis e ações de adequação.
Templates NÃO DEVEM ser modificados implicitamente.

**Versão**: 1.1.0 | **Ratificação**: TODO(RATIFICATION_DATE): confirmar na aprovação formal inicial.
**Última alteração**: 2026-10-29.

## 9. Decisões pendentes

Estas decisões NÃO constituem escolhas aprovadas. Resolução DEVE registrar responsável e
aprovação no artefato indicado.

| Decisão | Etapa limite e registro |
| --- | --- |
| Data de ratificação e responsáveis por aprovar constitution e desvios. | Governança inicial, antes da ratificação ou de desvios; registrar nesta constitution e no processo de revisão. |
| Fontes oficiais, layouts, versões, regras fiscais, schemas e cruzamentos suportados. | Especificação e esclarecimentos, antes do plano das validações afetadas; registrar fontes e aceite. |
| Status, severidades, revisão humana e critérios de conclusão ou resultado parcial. | Especificação e esclarecimentos, antes do plano do contrato de análise. |
| MongoDB ou Redis, isolamento organizacional aplicável, retenção, expiração, exclusão e limite de histórico. | Plano e ADR, antes da implementação da memória. |
| Contratos REST, MCP, ferramentas, JSON do LLM, envelope API e evolução. | Especificação dos comportamentos e plano dos contratos, antes da implementação correspondente. |
| Etapa de entrega A2A, capacidades, contratos, transporte e segurança. | Especificação da integração e plano/ADR, antes da implementação A2A. |
| Ingestão, segmentação, deduplicação, reprocessamento e atualização. | Plano da ingestão, antes da implementação; avaliar exemplos representativos. |
| Modelo de embeddings, dimensão, indexação, busca, topK e limiares. | Plano RAG antes da implementação; avaliação antes da aceitação da recuperação. |
| Modelo OpenAI, parâmetros, saída estruturada e retentativas. | Plano LLM, antes da integração; validar compatibilidade e falhas. |
| Propriedade e consolidação do consumo de cada serviço, preços e cálculo de estimativas. | Plano de observabilidade e dados, antes das chamadas monitoradas. |
| Autenticação, autorização, proteção e minimização de dados, segredos e políticas de armazenamento. | Plano de segurança e dados, antes de expor interfaces ou persistir dados correspondentes. |
| Limites de arquivos e processamento, timeouts, idempotência e falhas por operação. | Plano técnico, antes da implementação das operações afetadas. |
| Corpus, métricas e critérios de avaliação RAG e LLM. | Especificação dos critérios e plano de validação, antes da implementação e avaliação. |
| Projetos adicionais, bibliotecas, versões de SDKs e implantação. | Plano e ADR quando relevante, antes de adotar a escolha; justificar necessidade. |
