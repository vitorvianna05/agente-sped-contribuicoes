# Contexto Conhecimento Fiscal

Este contexto pertence à Api.RAG e implementa a feature
`specs/001-base-fiscal-versionada/`, sob a constitution 1.1.0. A Api.RAG é
proprietária dos documentos, metadados, gerações, referências e vetores. O consumo
funcional DEVE ocorrer por MCP; REST é reservado à administração autenticada.
Api.Agente NÃO DEVE consultar diretamente seu banco. Não há regras fiscais
implementadas pelo scaffold.

Desenho integrado atualizado em [../architecture.md](../architecture.md). Este documento
preserva as invariantes; caminhos das tarefas abertas seguem a revisão de 2026-11-05.

## Linguagem ubíqua

| Termo | Significado e limite |
| --- | --- |
| Fonte oficial | Origem identificada por emissor e identificação oficial; URL é metadado, não autorização para download. |
| Versão documental | Edição oficial de uma fonte, identificada por string opaca e conteúdo imutável. |
| Versão de layout | Versão do documento analisado pelo consumidor; não equivale à edição do manual. |
| Aplicabilidade | Associação explicitamente fundamentada entre tipo documental, versão de layout e versão documental. Não se infere compatibilidade por proximidade. |
| Geração | Resultado de um processamento com perfil identificado; nova geração não altera conteúdo ou referências anteriores. |
| Trecho | Ocorrência textual segmentada dentro de uma geração, com posição observada quando disponível. |
| Referência | Identificador recuperável e não reciclável da evidência; localização ausente é declarada, nunca fabricada. |
| Publicação | Disponibilidade autorizada de uma versão para novas recuperações; processamento concluído não equivale à aprovação inicial. |

## Dependências e organização

A solução DEVE manter apenas `src/Api.RAG` e `tests/Api.RAG.Tests` nesta feature.
As camadas são lógicas dentro do projeto de produção; diretórios são introduzidos
quando houver código correspondente, sem bibliotecas vazias.

- `Domain/Knowledge`: entidades, objetos de valor e invariantes; NÃO DEVE depender
  de ASP.NET Core, MCP, SQL, OpenAI ou infraestrutura.
- `Application/Knowledge`: casos de uso e portas pequenas; DEVE depender do domínio
  e NÃO DEVE conhecer detalhes de transporte ou persistência.
- `Infrastructure/Knowledge`: persistência, observabilidade e adaptadores existentes;
  DEVE implementar portas da aplicação.
- `Controllers` e `Tools`: entrada REST/MCP; `Endpoints/Contracts` e middleware existentes permanecem compartilhados;
  DEVE delegar regras aos casos de uso.
- `Ingestao`: extração/segmentação e coordenação de ingestão, sem duplicar invariantes.
- `Rag`: ConhecimentoService e OpenAiEmbeddingService, preservando portas existentes.
- `Program.cs`: raiz de composição; PODE conhecer adaptadores para registrá-los,
  mas NÃO DEVE conter regras de negócio.

Os testes PODEM referenciar produção; produção NÃO DEVE referenciar testes.
O SDK OpenAI será usado para embeddings nesta feature, sem geração de parecer.
Api.Agente (feature 002) executará validadores SPED, consumirá MCP e usará IA OpenAI para interpretar resultados e redigir parecer com evidências. Semantic Kernel, A2A e memória seguem seus incrementos próprios.
Persistência e decisões relevantes seguem o ADR-0001 e os gates do plano.

## Clean Code e evolução ágil

Tipos e métodos DEVEM expressar responsabilidades na linguagem acima; invariantes
DEVEM ter um único ponto de definição. Abstrações adicionais DEVEM responder a
uma necessidade demonstrável. Revisões DEVEM rejeitar ciclos entre camadas,
regras no transporte e dependências técnicas no domínio. Não há limite arbitrário
de linhas ou classes. Mudanças relevantes DEVEM atualizar ADRs e rastreabilidade.

`.editorconfig` fixa UTF-8, LF, indentação, namespaces de arquivo, chaves,
modificadores de acesso, remoção de imports não usados e convenções de nomes.
`Directory.Build.props` habilita nullable, analisadores recomendados .NET 10,
estilo no build, build determinístico e warnings como erros. Revisões DEVEM
verificar `dotnet build`, `dotnet format --verify-no-changes` e testes pertinentes.
Supressões DEVEM ser específicas e justificadas; não desabilitar os analisadores
globalmente para aprovar uma alteração.

## Verificação por incremento

Cada PR DEVE ligar tarefas e requisitos aos testes e evidências. Testes unitários
NÃO DEVEM chamar serviços reais; integração DEVE usar recursos isolados. O cliente
MCP de teste DEVE executar em processo separado, sem criar terceiro projeto de
produção. Categorias previstas: Unit, Contract, Integration, Evaluation e Capacity.
Preparar fixtures não comprova qualidade fiscal ou capacidade. P01–P04 permanecem
pendentes até receberem as evidências e aprovações exigidas no plano.
