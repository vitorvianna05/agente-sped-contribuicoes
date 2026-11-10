# ADR 0008 — Modelos escolhidos e exceção local de autenticação

> **Nota (2026-11-10):** a exceção local de autenticação continua vigente. A escolha de gpt-4o-mini, a chave apenas em Embeddings:ApiKey e EmbeddingStageEnabled=false foram substituídas pela [ADR 0010](0010-ambiente-local-openai-swagger.md) (gpt-4.1-mini, seção OpenAI, etapa ligada pela chave); gates de retenção/perfil pela [ADR 0009](0009-simplificacao-ingestao-busca.md).

Registro documental: 2026-11-10. Consolida instruções já autorizadas pelo usuário e código
existente; não altera a constitution, não introduz nova exceção de produção e não implementa
Api.Agente. Complementa ADR 0003/0006/0007 e preserva P01–P04 pendentes.

## Instrução do usuário e interpretação aplicada

O usuário autorizou desconsiderar autenticação no momento e escolheu “chatgpt4.0-mini” e
“embedding-3small”. O esclarecimento aplicado é `gpt-4o-mini` para interpretar resultados e
elaborar parecer na futura Api.Agente; `text-embedding-3-small` para vetorização na Api.RAG.
O exemplo local usa 1536 dimensões, mantendo modelo/dimensão configuráveis e versionados
no perfil. Não é um parecer de qualidade, aprovação fiscal ou autorização para inventar
chunk/overlap/topK/limiar; avaliações T070/T071/T077 e AG014 continuam pendentes.

## Limite da exceção implementada

Somente Api.RAG em Development com Authentication:DisabledForDevelopment=true e conexão
loopback recebe identidade development-local e permissões locais. Não se confia no ator do
corpo/argumentos. HTTPS, hosts e limites continuam exigidos. Metadados OAuth retornam 404
nesse modo; fora de Development a flag falha na inicialização. Modo normal usa JWT e
atribuições centrais. Não foi removida autenticação em produção nem criado login ChatGPT.

O exemplo Development possui limites finitos e EmbeddingStageEnabled=false até que chave,
banco/perfis/retenção sejam configurados; isso permite iniciar o host, não demonstrar busca
operacional. Templates não contêm segredos. Embeddings:ApiKey / Embeddings__ApiKey é a chave
consumida; UserSecretsId está no csproj e o arquivo local é ignorado pelo Git.

## Evidência e consequências

- Código: RagOptionsValidator, KnowledgeAuthorization, DevelopmentAuthenticationHandler,
  KnowledgeEndpointRegistration e appsettings.Development.Example.json.
- Testes já executados: DevelopmentAuthenticationTests (loopback IPv4/IPv6, recusa de acesso
  remoto/sem endereço, autorização local e rejeição da flag fora de Development), integrantes
  da suíte de 94 testes em validation/review-corrections.md.
- Nenhuma chamada OpenAI real, validação fiscal ou aceite de ambiente inferido desses testes.
- A exceção não desativa gates de retenção/perfil, não fecha P04 e não foi implementada na
  Api.Agente. AG004 deverá definir seus controles antes de integrar fora de testes.

Rastreio: RG-005/006/009/010; T008/T011/T029/T076; AG004/AG008/AG010/AG014.
