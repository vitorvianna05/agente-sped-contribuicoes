# Verificação documental — integração SOFIA

Data: 2026-11-10. O usuário confirmou SOFIA/INFIS e pediu ajuste documental sem mexer na RAG.

- Referências locais sofia-a2a-contrato/sofia-a2a-implementacao e seus contratos/exemplos lidos.
- Perfil fixado: A2A v0.3.x JSON-RPC e negócio SOFIA v1.0. Compatibilidade/SDK .NET precisa
  de verificação antes da implementação; nenhum pacote foi adicionado.
- Exemplos JSON externos e JSON interno do TextPart desserializam; campos obrigatórios,
  UUIDs de negócio, IDs correlacionados, documento não vazio e Task/Artifact foram conferidos.
- Texto sintético preserva CRLF em round-trip JSON; resposta documental é parcial,
  sem afirmar validação fiscal. Esses checks não são teste de SDK/servidor executável.
- Hash agregado antes/depois: src, tests, Feature 001 inteira e constitution idênticos.
  Não houve alteração de código/teste/configuração da Api.RAG ou Api.Agente, nem da constitution.
- Feature 003 tem 10 tarefas AA pendentes; estados/dependências das 14 AG foram preservados.
  A descrição de AG012 explicita caso de uso compartilhado, sem encerrar a análise integral.
- Nenhum endpoint A2A/Agent Card, chamada SOFIA, build, teste .NET, migração ou deploy foi
  executado por este incremento documental. Não se somam testes anteriores como novos.

Pendentes: acordo de identidade/URLs/segurança/limites/retensão, adaptação do texto SPED,
contratos executáveis, Task store/idempotência, servidor/card, testes em processo separado e
interoperabilidade com o responsável da SOFIA. Referências RAG estruturadas exigem acordo
compatível v1 antes de implementação, sem obrigatoriedade unilateral. Ver spec.md/plan.md/tasks.md.

Exemplos: [requisição](../examples/message-send.request.json),
[resposta parcial](../examples/message-send.response.json).
