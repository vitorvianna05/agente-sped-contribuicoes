# ADR 0001 — SOFIA orquestra, Api.Agente analisa SPED

Data: 2026-11-10. O usuário confirmou SOFIA/INFIS como API orquestradora e pediu ajuste de
documentação sem mexer na Api.RAG. A constitution já exige servidor A2A da Api.Agente;
não é necessária mudança constitucional. Este incremento não implementa código.

SOFIA possui requisição inicial, seleção dos agentes e agregação. Api.Agente é servidor
especializado A2A v0.3.x JSON-RPC, contrato SOFIA de negócio 1.0; mantém seu fluxo interno
parser/validadores → MCP → parecer validado. O consumidor do parecer especializado passa
a ser a SOFIA, que preserva o resultado e produz sua própria síntese, sem reescrever a
análise original. O endpoint REST de leitura e o diagnóstico atuais continuam disponíveis
como contratos técnicos; não constituem integração A2A ou conversação pública independente.

Mantêm-se as fronteiras internas do banco/conhecimento da RAG e o MCP existente, sem alteração
na Feature 001. A2A conecta orquestradora e agente; MCP conecta agente e ferramenta de
conhecimento. Não se amplia o MCP da RAG para atender a SOFIA diretamente.

O contrato SOFIA envia texto, não bytes. Sua preparação precisa preservar CRLF/ordem/valores;
o parser HTTP Latin-1 atual não pode ser reutilizado por conversão com substituição de
caracteres ou autocorreção. Criar porta textual/adaptador e proveniência específica; hash do
payload não é hash original. Essa lacuna fica rastreada em AA003, não é corrigida por mudar
a documentação ou presumir a extração da SOFIA como fiel.

usuario.nome é informação de negócio e não autenticação. Preservam-se os IDs gerados pela
SOFIA, separadamente dos IDs locais e A2A. Credenciais/audiences são distintas por hop.
Loopback sem JWT não autoriza rede Docker nem acesso remoto; ambiente externo exige acordo
de segurança. Não introduzir persistência/Task store sem política de dados/retensão aprovada.

Alternativas: REST de análise continua útil como adaptador interno/teste, mas não substitui
A2A no contrato SOFIA acordado. Centralizar roteamento na Api.Agente duplicaria o papel SOFIA.
Implementar a SOFIA neste repositório extrapolaria o escopo: depende do proprietário externo.

Agent Card/RPC/DTOs/testes executáveis ainda não existem. tasks.md define AA001–AA010;
capacidades fiscais só podem ser anunciadas quando efetivamente disponíveis. Nome/URL,
autenticação, preservação de texto, referências RAG no resultado e limites reais são decisões
pactuadas antes da implementação dependente. Exemplos são apenas fixtures documentais parciais.

Fontes do perfil: skills sofia-a2a-contrato e sofia-a2a-implementacao/references, e
[especificação oficial A2A 0.3.0](https://a2a-protocol.org/v0.3.0/specification/).
