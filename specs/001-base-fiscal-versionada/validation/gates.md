# Registro operacional P01–P04

Data: 2026-11-05. T006 registra os gates; não aprova sua ativação.
Responsáveis abaixo são papéis previstos no plano; pessoas e aprovações ainda não foram designadas.

| Gate | Responsável pelo fornecimento/aceite | Estado | Evidência exigida para fechamento |
| --- | --- | --- | --- |
| P01 — retenção e proteção | Produto/dados e administrador central; titular nominal pendente | Pendente | T075: aprovação identificável de prazos por categoria, marco retirada/substituição, originais, referências, tombstones, auditoria, consultas e consumo; acesso, exclusão e autoridade de expurgo. |
| P02 — conteúdo e qualidade | Responsável fiscal designado aprova; equipe técnica executa; titulares nominais pendentes | Pendente | T069: manifesto com PDFs oficiais, hashes, versões/aplicabilidade, perguntas, referências esperadas, negativos e critérios aprovados. T077: comparação executada e escolha fundamentada do perfil/modelo/dimensão. |
| P03 — capacidade | Equipe técnica/operação; titular nominal pendente | Pendente | T072/T078: protocolo e relatório reais com 300 PDFs caracterizados por bytes/páginas/trechos/hashes, 5 consultas simultâneas, ambiente/cache/amostra/duração, falhas no denominador e ≥95% completas em até 5s pelo cliente MCP. |
| P04 — ambiente | Equipe técnica/operação; titular nominal pendente | Pendente; dependências e fundação validadas em testes sintéticos, sem aceite do ambiente real | T076: emissor/audience/permissões, TLS, segredos externos, limites/timeouts/retries/leases e infraestrutura autorizada. Recuperação/backup em T081. Restore/build locais não comprovam transporte MCP nem banco operacional. |

## Uso dos exemplos

- `src/Api.RAG/appsettings.Example.json` é um template deliberadamente incompleto, não carregado automaticamente pelo host nem publicado pelo projeto. Nulos representam entradas obrigatórias ainda não fornecidas, nunca zero ou retenção infinita.
- `tests/Api.RAG.Tests/Fixtures/synthetic-settings.json` contém somente parâmetros arbitrários de teste: provider Fake, domínio reservado `.invalid`, perfil candidate e relógio fixo. Não há aprovação fiscal, credenciais, dados oficiais ou perfil adotado. Fixtures T005 injetam conexões e relógio isolados; não reutilizar conexões reais.
- Os nomes de configuração orientam T008; o validador e os bloqueios de persistência foram implementados na Phase 2. Os exemplos não aprovam as entradas operacionais.
- Conexões e credencial OpenAI serão fornecidas por variáveis de ambiente/secret manager (por exemplo `ConnectionStrings__Rag`, `ConnectionStrings__RagMigration` e `OPENAI_API_KEY`); nunca preencher exemplos com segredos. Migração e aplicação usam identidades distintas.
- Não usar o provider Fake fora dos testes. Parâmetros sintéticos de chunk, overlap, topK, limiar e dimensão não constituem decisão de produção. Ausência de perfil adotado impede consulta operacional; testes de busca devem preparar explicitamente seu cenário isolado.
- Antes de persistir cada classe real, satisfazer P01; antes de integração/exposição real, P04. Falta de política impede expurgo; expurgo é manual e requer autorização central, prazo vencido e dependências revalidadas. Não há exclusão automática.
- Avaliação oficial depende de P01/P02/P04; capacidade herda esses gates e exige P03. Código parametrizado e testes sintéticos podem prosseguir sem presumir essas aprovações.

## Registro de aprovação futura

Para cada gate, registrar nome/papel do responsável, data UTC, decisão, escopo,
versão/hash do artefato e referência à evidência revisada, sem segredos. Enquanto faltarem
esses elementos, conservar o estado pendente. Fechar T006 não fecha T069/T075–T078.
