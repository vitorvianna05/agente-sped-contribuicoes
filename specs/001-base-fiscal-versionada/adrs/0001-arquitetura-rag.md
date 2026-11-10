# ADR-0001 — Proposta de arquitetura da base fiscal versionada

> **Nota (2026-11-10):** proposta inicial. Aprovação explícita, retirada/expurgo e catálogo descritos aqui foram substituídos pela [ADR 0009](0009-simplificacao-ingestao-busca.md); publicação passou a ser automática.

**Data**: 2026-10-29
**Estado**: Desenho proposto atualizado às dez respostas da spec; ativação e aceite sujeitos
a P01–P04 em plan.md. Não representa aprovação operacional ou implementação concluída.
**Escopo**: D04 (estratégia), D11 (topologia) e fronteira MCP da feature 001.

## Contexto

A spec exige base compartilhada, administração central, aprovação inicial explícita,
reprocessamento com troca automática em caso de sucesso e referências históricas recuperáveis.
Somente manuais e guias PDF com texto extraível estão no escopo inicial.
A constitution determina .NET 10, PostgreSQL/PgVector e comunicação funcional MCP.
Não existem projetos de aplicação no workspace.

## Decisão proposta

1. Uma Api.RAG ASP.NET Core seguindo DDD, Clean Code e arquitetura ágil conforme constitution
   1.1.0. Contexto Conhecimento Fiscal com linguagem compartilhada, invariantes no domínio,
   casos de uso na aplicação e detalhes externos em adaptadores. Organização por camada e
   funcionalidade, sem novas bibliotecas de produção sem justificativa. Modelagem tática e
   limites de consistência estão em data-model.md, sem impor padrões desnecessários.
2. PostgreSQL dedicado logicamente à Api.RAG, com PgVector e credenciais separadas de migração
   e aplicação. Nenhum acesso direto da Api.Agente aos dados internos.
3. Avaliar originais em bytea na mesma fronteira transacional; validar adequação após conhecer
   tamanho/volume e retenção. Não escolher armazenamento local efêmero.
4. Fila durável no banco e processamento por worker hospedado. Reavaliar host se não houver
   garantia de execução persistente; nenhuma escolha de hospedagem implícita.
5. Gerações imutáveis; publicação inicial por aprovação central. Reprocessamento bem-sucedido
   troca a geração ativa em transação curta, sem nova aprovação. Falha preserva a anterior.
   Troca verifica revisão de estado para impedir reativação de fonte retirada.
6. Filtros relacionais de aplicabilidade, publicação, geração e perfil antes da recuperação;
   busca exata por cosseno como baseline de avaliação, sem ANN inicialmente.
7. REST para administração e Streamable HTTP stateless MCP para consulta e resolução histórica.
   SDK/protocolo pesquisados em research.md; validar combinação na implementação.
8. Adaptador PdfPig para extração; perfis imutáveis para segmentação e embeddings. Nenhum
   tamanho, topK, limiar ou modelo de embeddings aprovado sem avaliação D12.
9. Autorização por operação, identidade do transporte e auditoria sem segredos. Expurgo
   é manual por administrador central, após prazo configurado por categoria e vencido;
   sem política válida não habilitar exclusão, e vencimento não a dispara.
10. Api.RAG registra cada tentativa de embeddings no seu PostgreSQL, com lacunas explícitas;
    não adiciona LLM gerador, Semantic Kernel ou memória conversacional ao servidor RAG.

## Justificativa e alternativas

A proposta mantém um limite operacional de produção e usa transações para impedir publicação
parcial. Sobrescrever trechos prejudicaria referências; broker, serviço de objetos, worker
separado e índices ANN adicionariam operação sem escala demonstrada. Essas alternativas não
são proibidas: exigem medição e revisão deste ADR. Pesquisa e fontes oficiais estão em
[research.md](../research.md), seções R01–R12.

## Consequências e critérios de revisão

- Busca exata oferece referência de comparação; latência pode exigir índice após medição.
- Originais no banco aumentam backup e armazenamento; revisar mediante caracterização dos 300 PDFs e gate P03.
- Filas/leases e trocas concorrentes exigem testes reais de PostgreSQL, não somente dublês.
- Retenção deve cobrir gerações substituídas e retiradas para que referências continuem válidas.
- Similaridade não comprova suficiência fiscal; corpus e critérios aprovados continuam obrigatórios.
- Versões de pacote observadas não equivalem a pacotes instalados ou integração validada.
- Cada incremento deve preservar organização do código, dependências e testes de comportamento;
  revisão de Clean Code e análise estática configurada no plano integram a verificação.
- Novos padrões ou estruturas exigem caso de uso ou risco atual e revisão desta decisão,
  preservando arquitetura evolutiva e limites do domínio.

## Atualização após a segunda rodada

- Perfil de 300 PDFs e 5 consultas simultâneas; ≥95% completas em até 5s pelo cliente,
  incluindo embedding da pergunta. A topologia é hipótese a validar, não desempenho comprovado.
- Responsável fiscal aprova fontes, perguntas e evidências; técnica prepara e executa avaliação.
- Conflito de bytes na mesma identidade/versão é rejeitado. Nova origem exige outra versão oficial.
- Gerações históricas e referências são imutáveis; expurgo manual registra política, marco e
  vencimento e não remove origem compartilhada com geração ativa. Retry não reinicia retenção.
- Diagrama de estados e contratos estão em data-model.md e contracts/; nenhuma fonte fiscal,
  meta de relevância ou prazo foi acrescentado por suposição.

## Condições de ativação e aceite

**Revisão de 2026-10-30:** valores de retenção são aprovados antes de ativar política e
persistência real correspondente, preservando implementação parametrizada e fixtures
sintéticas. A consulta administrativa de artefatos retidos expõe somente metadados e ETag
por item com knowledge.audit central; não expõe conteúdo nem autoriza expurgo. Avaliação
oficial e carga dependem explicitamente dos gates de retenção e ambiente em tasks.md.

P01–P04 substituem os bloqueios gerais antigos. Configurar prazos antes de ativar retenção;
definir políticas de auditoria/consultas antes da persistência operacional; obter corpus e
expectativas aprovados antes da avaliação; adotar parâmetros somente após comparação; medir
capacidade/latência e validar segurança/ambiente antes de aceitar produção. Detalhes em
[plan.md](../plan.md). Este ADR registra o desenho e não declara essas condições satisfeitas.
