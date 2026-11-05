# Guia de validação — base fiscal versionada

Setup atualizado em 2026-11-05: projetos existem; restore, locked mode, build e um teste local de compatibilidade foram aprovados na T004 (validation/phase-1.md). T005 concluída: host sintético, dublês e cliente MCP em processo separado; seis testes Unit/Contract/Integration aprovados. Detalhes em validation/t005-infrastructure.md. Funcionalidades de negócio ainda pendentes. Não há evidência de ingestão, recuperação ou carga.

Contratos: [REST administrativo](contracts/admin-rest.md), [MCP](contracts/mcp.md),
[schemas](contracts/mcp-tools.schema.json). Regras e estados: [modelo](data-model.md).
Gates e rastreabilidade: [plano](plan.md). Não usar uma fixture sintética como documento fiscal oficial.

## Pré-requisitos

- SDK estável .NET 10 selecionado pelo global.json; PowerShell; HTTPS de desenvolvimento.
- PostgreSQL com extensão PgVector compatível, banco lógico da Api.RAG e identidades distintas
  de migração e aplicação. Usar instância isolada para testes, com permissões restritas.
- Pacotes fixados e compatibilidade verificada na implementação. Versões restauradas e fixadas nos lockfiles pela T004. Endpoint MCP e cliente real devem usar a mesma revisão compatível documentada.
- Emissor/audience configurados e tokens de teste para consumidor, administrador central e
  administrador organizacional sem atribuição central. Não registrar tokens nos artefatos.
- Limites finitos: arquivo, páginas/processamento, argumentos, timeout, retries, admissão e lease.
  Valores de ensaio explícitos em configuração de teste, sem promovê-los a política de produção.
- Políticas sintéticas de retenção em teste, com relógio controlável. Para dados reais, P01:
  prazos aprovados e política própria de originais, referências, tombstones, auditoria e consultas.
- Fixtures técnicas: PDF textual válido, sem localização recuperável, sem texto útil, formato
  não suportado, versões distintas, bytes conflitantes sob mesma versão e texto com instruções.
- Para avaliação oficial: P02 preenchido, responsáveis designados, manifesto e expectativas
  aprovados pelo fiscal. Para capacidade: 300 PDFs caracterizados, conforme P03.

## Configuração esperada da implementação

O projeto deverá documentar configuração externa, validar entradas obrigatórias e manter
segredos em variáveis de ambiente ou secret manager. Grupos de configuração planejados:

| Grupo | Conteúdo obrigatório |
| --- | --- |
| ConnectionStrings | Conexão Api.RAG; migração usa identidade separada. |
| Authentication | Emissor, audience e permissões atribuídas explicitamente. |
| Processing / Retrieval | Perfis versionados, parâmetros candidatos ou adotados e limites finitos. |
| Embeddings | Provedor/modelo/dimensão compatíveis, credencial protegida e política de tentativas. |
| Retention | Categorias, prazos, versão da política e retenção de cada classe persistida. |
| Evaluation | Manifesto aprovado, critérios prévios e diretório de relatórios sem segredos. |

Exemplos criados na T006: src/Api.RAG/appsettings.Example.json e tests/Api.RAG.Tests/Fixtures/synthetic-settings.json. Registro em validation/gates.md; validação de configuração será implementada na T008.
Configuração ausente de retenção deve impedir expurgo; ausência de perfil adotado impede consulta
operacional, sem falsificar resultado vazio. Ambiente sintético usa provider/relógio controlados.

## Comandos PowerShell após implementação

Executar a partir da raiz do repositório. Os testes usam categorias Unit, Contract,
Integration, Evaluation e Capacity para permitir os filtros abaixo; não criar um projeto extra
apenas para cada categoria.

```powershell
dotnet --version
dotnet restore src/Api.RAG/Api.RAG.csproj
dotnet restore tests/Api.RAG.Tests/Api.RAG.Tests.csproj
dotnet build src/Api.RAG/Api.RAG.csproj --no-restore
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --filter-trait "Category=Unit"
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --filter-trait "Category=Contract" --report-xunit-trx
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --filter-trait "Category=Integration" --report-xunit-trx
```

Após migrações explícitas pela identidade própria e configuração, iniciar serviço:

```powershell
dotnet run --project src/Api.RAG/Api.RAG.csproj --no-build
```

As tarefas devem documentar o comando real de migração escolhido antes de executar integração.
Este guia não inventa script de migração ou compose disponível. Integração deverá subir o
servidor e usar cliente MCP real; mocks não comprovam compatibilidade de transporte.

## Cenários ponta a ponta e resultados esperados

| Cenário | Ações pelo cliente administrativo/MCP | Resultado |
| --- | --- | --- |
| Ingestão e publicação | POST ingestão; acompanhar; buscar antes de approve; approve pelo mesmo administrador; buscar novamente. | 202/operação concluída não publica; antes da aprovação nenhum trecho ativo; após, referências resolvíveis e fonte correta. |
| Formatos | Enviar schema, imagem/PDF sem texto ou conteúdo inválido. | Recusa suportada pelo contrato ou operação failed, sem evidência ativa; localização ausente não é inventada. |
| Deduplicação | Repetir upload e executar dois uploads simultâneos com mesma identidade/versão/bytes. | Uma representação ativa, identidade preservada, nenhuma redundância causada por retry. |
| Conflito | Enviar bytes diferentes sob mesma identidade/versão. | 409 VERSION_CONTENT_CONFLICT; origem e publicação anteriores intactas; só outra versão oficial permite atualização. |
| Aplicabilidade | Consultar layout compatível e incompatível com textos semelhantes. | Nenhum trecho incompatível; versão de manual e de layout permanecem distintas. |
| Falta de evidências | Consultar caso negativo e simular dependência indisponível. | Limitação normal no primeiro; technical_failure/isError no segundo; sem conformidade ou referência fabricada. |
| Reprocessamento | Reprocessar fonte published, consultar durante, concluir e repetir com falha. | Antiga ativa até sucesso; troca integral automática; falha preserva antiga; referências antigas não mudam. |
| Corrida com retirada | Retirar fonte enquanto reprocessamento termina. | Fonte não é reativada; novas buscas excluem trechos; referências históricas resolvem conteúdo citado. |
| Descoberta para expurgo | Consultar GET retained-artifacts com knowledge.audit central, obter ID/revision por item e conferir lista vazia, versão inexistente e política ausente. | Somente metadados, sem PDF/texto/embedding; sem política elegibilidade false e impedimentos; auditoria não concede expurgo. |
| Retenção | Descobrir artefato/revision; expurgar sem política, antes de prazo e sem permissão; avançar relógio e executar ação manual autorizada. | Negar três primeiros; vencimento isolado nada apaga; expurgo permitido auditado; referência informa indisponibilidade. |
| Corrida após descoberta | Ler metadados elegíveis e revision; alterar estado/política/dependência representada antes de POST expurge com If-Match antigo. | 412 sem exclusão; reler item e revalidar autorização/prazo/dependências; leitura anterior não autoriza o efeito. |
| Segurança | Consumidor tenta escrever; administrador organizacional tenta aprovar; documento pede ignorar filtros. | Acesso negado sem efeito; conteúdo não altera política, filtros ou permissões. |
| Consumo | Simular embeddings com retry, timeout e métricas ausentes. | Cada tentativa registrada; nulos explícitos; nenhuma cobrança desconhecida convertida em zero. |

A resolução histórica deve devolver texto exato ou indisponibilidade sob concorrência com expurgo,
sem resposta parcial ou conteúdo de geração diferente. Verificar revogação/permissão a cada chamada.

## Avaliação e adoção de parâmetros

Antes de executar com dados/provedores reais, concluir T075 (aprovação P01) e T076 (ambiente
P04) em tasks.md. A avaliação oficial T077 depende explicitamente desses gates; ensaio de
capacidade T078 os herda via T077. Testes sintéticos continuam isolados e não os aprovam.

1. Fiscal aprova manifesto de PDFs oficiais, versões/aplicabilidade, perguntas e evidências
   esperadas; técnica versiona o conjunto e registra critérios de relevância antes da execução.
2. Comparar segmentação estrutural e outros candidatos, modelos/dimensões compatíveis, topK
   e limiares sem transformar candidatos em defaults de produção. Registrar tempo, custo,
   precisão/recuperação de referências esperadas e negativos conforme rubrica aprovada.
3. Executar a categoria de avaliação somente com P02 satisfeito:

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --filter-trait "Category=Evaluation" --report-xunit-trx
```

4. Relatório deve identificar corpus/hash, perfil, aprovador, executor, critérios e resultados.
   Sem aprovação ou com critério obrigatório falho, não adotar perfil operacional.

## Capacidade e latência — SC-009/010

Antes do ensaio, registrar bytes/páginas dos 300 PDFs, quantidade de trechos, perfis, máquina,
banco, conectividade do provedor, amostra/duração e condições de cache. Manter cinco consultas
concorrentes efetivamente em execução; não simular concorrência apenas com cinco chamadas sequenciais.

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --filter-trait "Category=Capacity" --report-xunit-trx
```

A categoria deverá medir no cliente MCP do envio até resposta completa, incluindo embedding da
pergunta, busca, transporte e referências/limitações; exclui geração de parecer LLM. Aceitar
somente se ≥95% de todas as consultas da amostra concluírem em até 5 segundos. Falhas/timeouts
não são sucessos e não saem do denominador ou relatório. Registrar percentil e percentual de
sucessos dentro da meta; garantir invariantes de qualidade em todas as respostas.

Não afirmar desempenho medido em fixtures pequenas como cumprimento da meta de 300 PDFs.
Se busca exata/bytea não atenderem, medir alternativa e revisar ADR antes de mudar arquitetura.

## Saída da validação

Entregar relatório com comandos realmente executados, ambiente, passou/falhou/ignorado,
TRX quando gerado, links a requisitos/SC e P01–P04 ainda abertos. Não declarar aceitação
produtiva com corpus não aprovado, políticas ausentes ou teste obrigatório não executado.
