# Pesquisa técnica — base fiscal versionada

**Data**: 2026-10-29. **Estado**: Fase 0 concluída para desenho; dez respostas incorporadas.
Pré-requisitos de ativação e aceite P01–P04 estão em [plan.md](plan.md); não representam
avaliação executada nem aprovação operacional.

As decisões abaixo são propostas técnicas de planejamento. As dez escolhas de negócio
registradas em [spec.md](spec.md) permanecem preservadas. Pesquisa não aprova prazo de
retenção, fontes fiscais, critérios de relevância ou metas operacionais em nome do usuário.

## R01 — Fronteiras e processamento

**Atualização normativa (2026-10-29):** constitution 1.1.0 exige DDD, Clean Code e arquitetura
ágil. A proposta delimita Conhecimento Fiscal, usa linguagem da spec e mantém invariantes no
domínio, casos de uso na aplicação e integrações na infraestrutura. Organização por camada
deve também permitir localizar funcionalidades. O desenho tático está em data-model.md;
gates operacionais condicionam ativação e aceite, sem inventar regras fiscais ou de retenção.

**Decisão proposta:** uma Api.RAG ASP.NET Core .NET 10, com camadas lógicas e worker hospedado
no próprio processo. REST somente administrativo; MCP somente para recuperação funcional.
Operações de ingestão/reprocessamento são duráveis e acompanháveis; resposta de aceitação
não significa processamento concluído nem publicação.

**Justificativa:** isola a responsabilidade sem adicionar um serviço de fila ou outra API.
A fila deve estar no PostgreSQL, com estado, lease e recuperação após interrupção; uma fila
somente em memória não satisfaz durabilidade. O ciclo de vida do host precisa suportar o worker.

**Alternativas:** processamento síncrono foi descartado como padrão de PDFs de duração variável;
broker/worker separado fica condicionado a necessidade demonstrada por volume e implantação.
Um serviço hospedado usa escopo próprio para suas dependências e observa cancelamento.
[ASP.NET Core — hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0).

## R02 — Persistência D11

**Decisão proposta:** banco lógico dedicado da Api.RAG em PostgreSQL/PgVector. Manter originais
PDF, metadados, trechos e operações no mesmo limite de dados. Para o primeiro perfil, avaliar
`bytea` para originais, evitando um segundo armazenamento antes de conhecer volume e tamanho.
Separar papel de migração, papel de aplicação e identidades dos consumidores; Api.Agente não
acessa esse banco. Dimensionamento e ativação dependem de P01/P03, com 300 PDFs e 5 consultas simultâneas.

**Justificativa:** transações únicas simplificam vínculo entre conteúdo, estado e referências.
`bytea` armazena bytes; Npgsql oferece mapeamento e leitura em fluxo. Isso demonstra viabilidade
técnica, não garantia de adequação a qualquer tamanho de corpus.
[PostgreSQL — binary types](https://www.postgresql.org/docs/current/datatype-binary.html),
[Npgsql — mappings](https://www.npgsql.org/doc/types/basic.html).

**Alternativas:** objetos externos com manifesto transacional são adequados se testes ou limites
de backup exigirem; aumentam trabalho de consistência, retenção e recuperação. Arquivos no disco
local da aplicação não são persistência durável presumida. Não escolher provedor de hospedagem.

## R03 — Gerações e concorrência

**Decisão proposta:** gerações de processamento imutáveis, referência que identifica geração
e trecho e ponteiro de geração ativa por versão documental. Publicação inicial exige aprovação
central explícita. Reprocessamento de fonte ativa troca o ponteiro somente após sucesso, sem
nova aprovação, conforme decisão do usuário. Geração anterior permanece consultável enquanto
ativa e resolvível historicamente sob retenção; falha não altera o ponteiro.

**Justificativa:** não reescrever conteúdo já citado. Troca, comparação da revisão de estado e
auditoria devem ocorrer em transação curta. Retirada ocorrida durante reprocessamento impede
reativação. Uma busca deve materializar a geração ativa e seus trechos no mesmo snapshot; leitura
em várias etapas precisa de transação consistente. Não manter transação aberta durante chamadas
a embeddings ou extração PDF.
[PostgreSQL — isolation](https://www.postgresql.org/docs/current/transaction-iso.html).

**Alternativas:** atualizar trechos no lugar quebra referências; tabela temporária tornada visível
antes da conclusão expõe resultados parciais. Escritas concorrentes precisam de bloqueio de linha
ou comparação de revisão, e não apenas decisão em memória.
[PostgreSQL — locking](https://www.postgresql.org/docs/current/explicit-locking.html).

## R04 — Identidade e deduplicação D04

**Decisão proposta:** identificar fonte por identificador oficial e emissor documentados, sem
normalizações que fundam fontes diferentes; versão oficial é rótulo opaco, não versão semântica.
Hash dos bytes identifica repetição de conteúdo. Chave de geração inclui versão, hash da origem,
versão do extrator e perfil de segmentação/embeddings. Constraints garantem idempotência sob
concorrência; mesma identidade/versão com bytes diferentes gera conflito explícito.

**Justificativa:** checagem prévia sem unicidade no banco permite duas ingestões simultâneas.
[PostgreSQL — constraints](https://www.postgresql.org/docs/current/ddl-constraints.html).

**Alternativas:** deduplicação global apenas por texto é inadequada. Duas ocorrências em páginas
distintas podem ter texto igual e proveniências diferentes. A identidade do trecho inclui sua
ocorrência/localização real ou sequência extraída quando localização não existir. Repetir a mesma
ingestão reutiliza o resultado; um reprocessamento intencional tem geração acompanhável, mas não
cria cópias redundantes no conjunto ativo.

## R05 — Extração de PDF

**Decisão proposta:** PdfPig encapsulado em infraestrutura, extração por página e análise de
ordem de leitura; preservar intervalo de origem, página física observada e, separadamente,
rótulo impresso quando identificável com segurança. Não usar OCR nesta entrega. Fixar versão
da biblioteca na implementação e comparar texto extraído com corpus antes de adotá-la.

**Justificativa:** a biblioteca expõe texto e posição; a ordem interna de conteúdo PDF não é
necessariamente a ordem de leitura. Colunas e tabelas exigem avaliação específica. Texto parcial
não prova que todo o conteúdo relevante foi extraído.
[PdfPig — documentação do projeto](https://github.com/UglyToad/PdfPig).

**Alternativas:** biblioteca comercial se corpus aprovado mostrar falhas relevantes. OCR é
fora do escopo aprovado. PDFs de imagem ou que dependam de OCR devem ser recusados; localização
não recuperável deve ficar explicitamente ausente, nunca inferida por LLM.

## R06 — Segmentação e embeddings D04

**Decisão proposta:** segmentação determinística que preserve estrutura e intervalos de origem,
comparada com uma estratégia de janela de tokens no conjunto D12. Perfil de processamento
imutável registra extrator, pré-processamento, segmentador, provedor, modelo, dimensão e métrica.
Consulta e documentos devem compartilhar espaço de embeddings; mesma dimensão isolada não prova
compatibilidade. Mudança de perfil exige nova geração e avaliação.

**Justificativa:** parâmetros precisam de evidência; não registrar chunk size, overlap, topK ou
limiar como configuração de produção aprovada nesta pesquisa. A seleção do modelo e dimensão
será resultado do experimento planejado, não uma conclusão baseada só na documentação.

**Alternativas:** comparar candidatos de embeddings compatíveis com PostgreSQL/PgVector e com
os limites reais do provedor. Não misturar scores ou vetores de perfis distintos. Não adicionar
LLM gerador apenas para rotular suficiência; esta feature recupera evidências e limitações.
A integração com Semantic Kernel é responsabilidade da Api.Agente futura.

## R07 — Busca e isolamento de versões D04

**Decisão proposta:** busca vetorial exata com distância cosseno como baseline experimental,
filtrando no SQL por tipo, aplicabilidade aprovada, publicação, geração ativa e perfil de
embeddings. Versão do manual e versão do layout são campos distintos ligados por aplicabilidade
com origem oficial. Não presumir compatibilidade por data, ordem de versão ou proximidade textual.

**Justificativa:** pgvector executa busca exata por padrão. Índices aproximados trocam recall por
desempenho e podem retornar menos resultados sob filtros. Testar candidatos com corpus e carga
antes de introduzi-los. Distância é score técnico, não confiança ou certeza fiscal.
[pgvector — filtering e indexing](https://github.com/pgvector/pgvector#filtering).

**Alternativas:** HNSW/IVFFlat apenas após comprovar necessidade e qualidade. A dimensão precisa
ser compatível também com o tipo e índice escolhidos; limite de índice não equivale a limite
geral de armazenamento. Consulta direta parametrizada por Npgsql/Pgvector evita uma abstração
multibanco sem necessidade atual; EF Core é alternativa se um padrão de persistência a justificar.
[pgvector-dotnet](https://github.com/pgvector/pgvector-dotnet).

## R08 — Transporte MCP e versões pesquisadas

**Decisão proposta:** SDK oficial `ModelContextProtocol.AspNetCore` 2.3.0 e cliente compatível,
Streamable HTTP stateless em `/mcp`, protocolo 2026-07-28. Versão 2.3.0 foi observada no NuGet
em 2026-10-29; isso não significa pacote instalado ou compatibilidade já testada neste projeto.
A implementação deve fixar versões e validar descoberta, tools e comportamento do transporte.
[NuGet — pacote oficial](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore).

**Justificativa:** consultas independentes não precisam de sessão, afinidade de instância ou
notificações espontâneas. O SDK v2 documenta transporte stateless; exemplos legados de sessão,
initialize e cancelamento não devem ser copiados sem validar a revisão selecionada.
[SDK C# — transports](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).

**Alternativas:** stdio é apropriado a subprocessos locais, não ao limite remoto entre os serviços.
SSE legado/stateful não tem necessidade demonstrada. Não haverá fallback para REST funcional.

## R09 — Tools e falhas

**Decisão proposta:** duas tools de leitura: `fiscal_knowledge_search_v1` e
`fiscal_reference_resolve_v1`. A busca recebe pergunta, tipo documental e versão do layout;
a resolução recebe identificador de referência. Identidade vem do transporte autenticado,
nunca de argumentos livres. Perfil de busca é controlado no servidor, não arbitrado pelo LLM.
Nomes e schemas de entrada/saída estão definidos em contracts/ na Fase 1.

**Justificativa:** as ferramentas devem ter schemas de entrada/saída e validar conteúdo
estruturado. Resultado de busca sem evidências é limitação normal; falha de execução usa
`isError` com erro tipado e correlação; erro de protocolo é separado. A resposta da tool é
produzida pelo servidor e não equivale a JSON gerado por LLM.
[MCP — tools 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/server/tools).

**Alternativas:** expor administração ao consumidor ou permitir bypass de versão não atende ao
escopo. Qualificar a recuperação como completa apenas porque houve resultado é inadequado;
sem critérios D12, deve haver limitação explícita e nenhuma alegação de suficiência fiscal.

## R10 — Identidade e permissões D07

**Decisão proposta:** acesso Bearer por OAuth, assinatura/emissor/audience/validade verificados;
Api.RAG é o recurso de destino. Permissões semânticas propostas: `knowledge.read`,
`knowledge.ingest`, `knowledge.publish`, `knowledge.reprocess`, `knowledge.withdraw`,
`knowledge.audit`. Administrador central recebe atribuições explícitas; administrador de
organização não as herda. O mesmo administrador pode ingerir e aprovar, conforme a spec.
Expurgo exige permissão central explícita `knowledge.expurge`, prazo configurado e vencido,
e ação manual; não existe exclusão automática.

**Justificativa:** autorizar cada operação, inclusive resolução histórica e descoberta aplicável.
Publicar metadados de recurso protegido conforme a revisão do protocolo; não repassar tokens
com audience de outro serviço. Servidor de identidade real permanece configuração ambiental,
sem fornecedor presumido.
[MCP — authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization),
[ASP.NET Core — Bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).

**Alternativas:** token arbitrário dentro de argumento da tool ou simples escolha pelo LLM não
concede autorização. A base de fontes compartilhada não torna logs de usuários públicos.
Aplicar HTTPS, validação de Origin quando presente, limites, cancelamento e sanitização.

## R11 — Consumo, auditoria e recuperação operacional

**Decisão proposta:** cada tentativa de embeddings registra operação, correlação, serviço,
provedor/modelo, datas, duração, status e tokens/custos quando informados. Api.RAG possui seus
registros em PostgreSQL; consolidação futura da Api.Agente deve usar contrato autorizado sem
acesso direto. Não criar endpoint de relatório financeiro nesta feature.

**Justificativa:** a obrigação constitucional de registrar embeddings também se aplica ao
servidor RAG. Persistir intenção antes da chamada e resultado após; se houver interrupção entre
provedor e banco, manter estado desconhecido, sem zero fictício. Preços exigem referência e
vigência; valores calculados são estimativas, não faturamento. Retentativas são novas tentativas
auditáveis, não sobrescrita da anterior. SDK com retry interno deve ter suas tentativas
instrumentadas ou retry automático desabilitado em favor da política controlada.

**Alternativas:** best effort silencioso perde chamadas; custo total por execução sem tentativas
não satisfaz auditoria. Chamada de embeddings pode ser repetida após falha ambígua e gerar novo
custo, embora a publicação documental permaneça idempotente. Isso deve constar da política de falhas.

## R12 — Avaliação D12 e liberação de parâmetros

**Decisão proposta:** separar fixtures técnicas sintéticas do corpus oficial aprovado. No corpus,
registrar PDF, origem, versão, hash, aplicabilidade e perguntas com trechos/referências esperados.
Incluir versões próximas incompatíveis, perguntas sem resposta, tabelas/colunas, localização ausente,
repetição, retirada e conteúdo malicioso tratado como dado.

**Justificativa:** critérios determinísticos SC-001–008 são invariantes; qualidade de relevância
exige rubrica e expectativas do responsável fiscal. Medir recuperação de evidência esperada,
precisão dos resultados e rejeição adequada em negativos, além de tempo e custo por configuração.
Não estabelecer limiares numéricos ou número de exemplos como se já aprovados.

**Alternativas:** selecionar chunk size/topK apenas por convenção ou usar outro LLM como única
verdade de avaliação não demonstra o aceite solicitado. Um modelo juiz futuro seria auxiliar,
nunca substituto da evidência oficial e revisão responsável.

## R13 — Segunda rodada: política e pré-requisitos

**Correção de consistência aprovada em 2026-10-30:** FR-014 distingue implementação
parametrizada de aprovação dos valores antes da ativação/persistência correspondente.
P01/P04 são predecessores explícitos dos ensaios reais nas tarefas, não apenas recomendações.
Descoberta de artefatos retidos usa GET administrativo somente de metadados, com knowledge.audit
central e token de revisão por item para If-Match. Não altera consulta funcional MCP nem concede
expurgo; revalidação transacional continua obrigatória. Contrato em contracts/admin-rest.md.

**Decisão:** retenção por categoria com prazo obrigatório configurável, contado da retirada
ou substituição; expurgo manual central após vencimento, com auditoria. Sem política válida,
expurgo não é habilitado. Vencimento isolado não exclui conteúdo. Atualização de bytes sob
mesma identidade/versão é rejeitada; reprocessamento muda representação, não a origem.

**Justificativa:** decisões explícitas do usuário em spec.md, segunda rodada. As responsabilidades
foram atribuídas: fiscal seleciona/aprova fontes, perguntas e evidências; técnica organiza e
executa. Esses comportamentos permitem desenhar políticas parametrizadas e contratos sem
inventar prazos ou escolher um manual oficial. Valores ausentes bloqueiam ativação do recurso,
e política de auditoria/consultas bloqueia sua persistência operacional, conforme a constitution.

**Alternativas:** não adotar expurgo automático, revisão interna de PDF alterado ou aprovação
técnica no lugar de aprovação fiscal. Não manter um bloqueio geral de Fase 1 depois de resolver
as escolhas de comportamento; não declarar condições de avaliação ou produção satisfeitas.

**Decisão técnica de retenção:** registrar categoria, evento, versão da política e vencimento
aplicável. Evento não é reiniciado por retry. Mudança de política não pode antecipar exclusão
silenciosamente; revisão retroativa exige decisão de produto/dados antes de ser oferecida.
Sem regras retroativas aprovadas, o mecanismo conserva os vencimentos registrados. Para
expurgar um artefato compartilhado, todas as retenções e dependências ativas precisam permitir.

## R14 — Carga e latência esclarecidas

**Decisão:** validar 300 PDFs e 5 consultas simultâneas, com pelo menos 95% das consultas
completas em até 5 segundos, do envio ao recebimento pelo cliente MCP. Incluir embedding da
pergunta; excluir geração de parecer por LLM. Falhas e timeouts são reportados e não contam
como consultas bem-sucedidas dentro da meta. Percentual de sucesso dentro de 5s usa como
denominador todas as consultas da amostra, sem omitir as falhas.

**Justificativa:** SC-009 e SC-010 aprovados. Tamanho de corpus em PDFs não determina bytes,
páginas, quantidade de trechos ou tempo por consulta. Antes do ensaio, registrar ambiente,
manifesto, amostra, duração, cache e parâmetros avaliados. Não afirmar que busca exata ou
bytea já atingiram a meta, nem tratar 300 PDFs como limite máximo de entrada.

**Alternativas:** ANN e objetos externos continuam opções de revisão por medição. Não adotar
índice aproximado apenas para presumir cumprimento de latência; manter isolamento determinístico.

## Gates operacionais e de aceite

- **P01 / D07:** preencher prazos e políticas por categoria; definir retenção de auditoria,
  consultas e tombstones antes da persistência operacional correspondente. Valores não aprovados.
- **P02 / D12/D04:** designar responsável fiscal, manifestar fontes/versões, aprovar perguntas,
  referências e critérios; executar comparações antes da adoção dos parâmetros de produção.
- **P03:** caracterizar os 300 PDFs, ambiente e protocolo; medir 5 consultas simultâneas e
  percentual de respostas completas em até 5 segundos, incluindo falhas no relatório.
- **P04:** configuração operacional, identidade, segredos, limites e compatibilidade de pacotes
  devem ser verificados antes da exposição real. Versões pesquisadas anteriormente são evidência
  datada, não afirmação de instalação ou nova consulta ao catálogo nesta atualização.
- **Governança:** ratificação da constitution não foi presumida nem alterada.

## R15 — Tentativa de setup em 2026-10-30

Foram declaradas versões candidatas em `Directory.Packages.props`, consultadas no
catálogo oficial NuGet: [MCP](https://www.nuget.org/packages/ModelContextProtocol/2.3.0)
e [MCP ASP.NET Core](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/2.3.0)
2.3.0; [Npgsql](https://www.nuget.org/packages/Npgsql/10.0.3) 10.0.3;
[Pgvector](https://www.nuget.org/packages/Pgvector/0.3.2) 0.3.2;
[PdfPig](https://www.nuget.org/packages/PdfPig/0.1.16) 0.1.16;
[OpenAI](https://www.nuget.org/packages/OpenAI/2.14.0) 2.14.0;
[Mvc.Testing](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing/10.0.14)
10.0.14; [Test SDK](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.11.0)
18.11.0; [Testcontainers.PostgreSql](https://www.nuget.org/packages/Testcontainers.PostgreSql/4.16.0)
4.16.0; [xunit.v3](https://www.nuget.org/packages/xunit.v3/4.0.2) 4.0.2 e
[runner](https://www.nuget.org/packages/xunit.runner.visualstudio/4.0.1) 4.0.1.
Essa declaração NÃO comprova compatibilidade integrada: restore falhou por acesso
negado ao NuGet.Config do usuário. Nenhuma versão foi efetivamente restaurada
nesta solução e nenhum lockfile foi produzido. O projeto de testes adota xUnit v3
como candidato; sua validação permanece em T004. Ver [evidências](validation/phase-1.md).

Não há esclarecimento de comportamento necessário ao desenho ainda sem resposta; há entradas
obrigatórias de ativação/avaliação com responsáveis e etapas definidos. Fase 1 foi produzida
sem executar implantação, embeddings, restore, build ou testes de aplicação.

## R16 — T004 validada em 2026-11-05

Restore real concluído para os dois projetos com SDK 10.0.303 e fonte exclusiva
`https://api.nuget.org/v3/index.json`. Os dois `packages.lock.json` foram gerados
pelo NuGet, incluindo dependências transitivas e hashes, e confirmados com
`--locked-mode`. As 11 versões diretas de R15 foram restauradas sem alterações;
`Directory.Packages.props` permanece como declaração central dessas versões.

Fontes oficiais: páginas NuGet versionadas de R15 e manifests `.nuspec` dos pacotes
restaurados. Os manifests apontam para os projetos oficiais
[SDK MCP](https://github.com/modelcontextprotocol/csharp-sdk),
[VSTest](https://github.com/microsoft/vstest), [xUnit](https://github.com/xunit/xunit),
[runner VS](https://github.com/xunit/visualstudio.xunit) e
[Testcontainers](https://github.com/testcontainers/testcontainers-dotnet).
Npgsql 10.0.3 oferece alvo net8.0; Pgvector 0.3.2 e PdfPig 0.1.16 oferecem net6.0/netstandard2.0;
OpenAI 2.14.0 oferece net8.0/netstandard2.0; MCP e Mvc.Testing oferecem net10.0.
O NuGet resolveu os assets compatíveis com net10.0, sem conflito de dependências.
Pgvector declara Npgsql mínimo 8.0.5; a compatibilidade local com Npgsql 10.0.3
foi exercitada registrando `UseVector()` e construindo o data source sem abrir conexão.

Diferença frente à pesquisa: nenhuma troca de versão, mas foi necessário selecionar
Microsoft.Testing.Platform em global.json e UseMicrosoftTestingPlatformRunner no projeto:
xUnit 4 usa MTP 2, incompatível com o modo VSTest de dotnet test no SDK 10.
Referências: [Microsoft — dotnet test](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
e [xUnit — MTP](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform).
O runner instalado usa `--report-xunit-trx`, não `--logger trx` nem `--report-trx`.
IDE0005 também exige GenerateDocumentationFile; habilitado e documentado o ponto de entrada.

Build passou sem avisos/erros; teste Unit de compatibilidade passou (1/1), validando
runner e carregamento/registro Pgvector/Npgsql. Isso não valida conexão PostgreSQL,
extensão vector instalada, extração PDF, embeddings reais ou transporte MCP.
Esses ensaios permanecem nas tarefas próprias, começando pela infraestrutura T005.
Não foi adicionado Semantic Kernel ou LLM gerador. Evidências em validation/phase-1.md.
