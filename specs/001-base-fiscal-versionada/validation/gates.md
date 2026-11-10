# Registro operacional P01–P04

Registro inicial: 2026-11-05. Sincronização: 2026-11-10. P01 foi encerrado pela
[ADR 0009](../adrs/0009-simplificacao-ingestao-busca.md) (a API não exclui conteúdo) e P02 deixou
de condicionar a busca; P02–P04 continuam pendentes como critérios de aceite. T006 e os testes
técnicos não aprovam sua ativação.
Responsáveis abaixo são papéis previstos no plano; pessoas e aprovações ainda não foram designadas.

| Gate | Responsável pelo fornecimento/aceite | Estado | Evidência exigida para fechamento |
| --- | --- | --- | --- |
| P01 — retenção e proteção | Vitor Vianna (ADR 0009) | Encerrado em 2026-11-10 | Sem expurgo pela API; configuração `Retention` exigida só como registro. T075 cancelada. Desvio constitucional definitivo registrado. |
| P02 — conteúdo e qualidade | Responsável fiscal designado aprova; equipe técnica executa; titulares nominais pendentes | Pendente (não bloqueia a busca) | T069: manifesto com PDFs oficiais, hashes, versões/aplicabilidade, perguntas, referências esperadas, negativos e critérios aprovados. T077: comparação executada e escolha fundamentada do perfil/modelo/dimensão. |
| P03 — capacidade | Equipe técnica/operação; titular nominal pendente | Pendente | T072/T078: protocolo e relatório reais com 300 PDFs caracterizados por bytes/páginas/trechos/hashes, 5 consultas simultâneas, ambiente/cache/amostra/duração, falhas no denominador e ≥95% completas em até 5s pelo cliente MCP. |
| P04 — ambiente | Equipe técnica/operação; titular nominal pendente | Pendente; dependências e fundação validadas em testes sintéticos, sem aceite do ambiente real | T076: emissor/audience/permissões, TLS, segredos externos, limites/timeouts/retries/leases e infraestrutura autorizada. Recuperação/backup em T081. Restore/build locais não comprovam transporte MCP nem banco operacional. |

## Uso dos exemplos

- `src/Api.RAG/appsettings.Example.json` é um template deliberadamente incompleto, não carregado automaticamente pelo host nem publicado pelo projeto. Nulos representam entradas obrigatórias ainda não fornecidas, nunca zero ou retenção infinita.
- `tests/Api.RAG.Tests/Fixtures/synthetic-settings.json` contém somente parâmetros arbitrários de teste: provider Fake, domínio reservado `.invalid`, perfil candidate e relógio fixo. Não há aprovação fiscal, credenciais, dados oficiais ou perfil adotado. Fixtures T005 injetam conexões e relógio isolados; não reutilizar conexões reais.
- Os nomes de configuração orientam T008; o validador e os bloqueios de persistência foram implementados na Phase 2. Os exemplos não aprovam as entradas operacionais.
- Conexões e credencial OpenAI são fornecidas por User Secrets/ambiente/secret manager (por exemplo `ConnectionStrings__Rag`, `ConnectionStrings__RagMigration` e `OpenAI__ApiKey`); nunca preencher exemplos com segredos. Migração e aplicação usam identidades distintas.
- Não usar o provider Fake fora dos testes. Parâmetros de chunk, overlap, topK, limiar e dimensão não são avaliados; a busca usa o perfil configurado (ADR 0009).
- Antes de integração/exposição real, P04. A API não exclui conteúdo (sem expurgo).
- Avaliação oficial depende de P02/P04; capacidade exige P03. Código parametrizado e testes sintéticos podem prosseguir sem presumir essas aprovações.

## Registro de aprovação futura

Para cada gate, registrar nome/papel do responsável, data UTC, decisão, escopo,
versão/hash do artefato e referência à evidência revisada, sem segredos. Enquanto faltarem
esses elementos, conservar o estado pendente. Fechar T006 não fecha T069/T075–T078.

## Estado técnico consolidado em 2026-11-10

Api.RAG implementa ingestão com publicação automática, vetorização e consulta MCP (ADR 0009);
97 testes sintéticos passaram em 2026-11-10. O cliente real em processo separado comprova
cenários técnicos, não qualidade fiscal/capacidade ou P04 real. A chave lida é `OpenAI:ApiKey`
(ou `Embeddings:ApiKey`); OPENAI_API_KEY não é ligada automaticamente às opções.

O usuário autorizou dispensar JWT neste momento: somente Development loopback na Api.RAG,
com flag explícita e identidade development-local. HTTPS/hosts continuam exigidos; outros
ambientes recusam a flag. P04 permanece pendente e não há autorização de exposição externa.
Essa exceção não dispensa política de dados, retenção ou avaliação. [ADR 0008](../adrs/0008-configuracao-local-modelos.md).

Modelos escolhidos: text-embedding-3-small (1536 no exemplo local) e gpt-4.1-mini para a
Api.Agente (ADR 0010). A escolha não fecha P02/AG014; o template geral e a configuração de
testes não representam configuração produtiva.
