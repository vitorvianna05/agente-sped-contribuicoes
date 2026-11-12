# ADR 0002 — Borda HTTP: CORS opcional e TLS em proxy confiável

Data: 2026-11-12. Status: aceito. Responsável: Vitor Vianna. Skill aplicada: `sofia-cors-rest-a2a`.
Escopo: Api.Agente. Não cria endpoint A2A nem Agent Card (AA005/AA007).

## Contexto

A SOFIA chama a Api.Agente servidor-servidor (A2A/REST). CORS é regra do navegador e não
participa dessas chamadas; só importa se um frontend da SOFIA chamar o agente diretamente.
A Api.Agente não tinha CORS (preflight caía na autenticação) e recusava todo HTTP que não fosse
HTTPS na própria Kestrel, o que impede TLS terminado em proxy reverso ou na plataforma de
hospedagem. Decisões do usuário: CORS configurável e desligado; TLS aceito via proxy confiável.

## Decisão

- **CORS** — política nomeada `SofiaBrowser`, aplicada só aos controllers (`RequireCors`);
  `/health/live` e Swagger ficam fora. Origens em `Cors:AllowedOrigins`; vazio = nenhuma origem
  autorizada (padrão nos exemplos). Métodos `GET`/`POST`, cabeçalhos `Authorization` e
  `Content-Type`, expõe `X-Correlation-Id`, sem `AllowCredentials`. Origem precisa ser
  `esquema://host[:porta]`, sem caminho, barra final ou curinga; HTTPS fora de Development.
  `Vary: Origin` em todas as respostas. CORS não substitui o JWT: rotas seguem exigindo token
  e permissão (`analysis.execute`/`analysis.diagnose`).
- **Proxy confiável** — `Transport:TrustedProxies` (IPs) e `Transport:TrustedNetworks` (CIDR,
  prefixo > 0). Com lista, o middleware de cabeçalhos encaminhados aceita `X-Forwarded-Proto` e
  `X-Forwarded-For` (limite 1) só desses remetentes; sem lista, os cabeçalhos são ignorados.
  As listas padrão do ASP.NET (loopback) são limpas. A guarda de transporte continua exigindo
  HTTPS efetivo e `Authentication:AllowedHosts`; HTTP direto segue `403 TRANSPORT_DENIED`.
- Configuração lida das opções validadas em tempo de execução (appsettings, variáveis de
  ambiente, testes), com falha no startup para valores abertos ou malformados.

## Consequências

- Em contêiner: `ASPNETCORE_URLS=http://+:8080` atrás do proxy que termina TLS, nome DNS do
  serviço em `Authentication:AllowedHosts` e IP/rede do proxy em `Transport:*`. A SOFIA chama
  o nome do serviço na rede interna, não `localhost`. Detalhes de deploy ficam com a skill
  `sofia-devops-free-cloud`.
- Quando existirem, o endpoint A2A e o Agent Card devem receber a mesma política só se um
  navegador precisar acessá-los; chamadas da SOFIA backend não dependem disso.
- Pendências externas (AA001): origens reais do frontend, se houver; proxy/rede da hospedagem;
  issuer/audience/scopes da credencial de serviço da SOFIA.

## Evidência

`CorsTransportTests` (19 casos): preflight da origem pactuada (204, origem, métodos, cabeçalhos,
sem credenciais); origem desconhecida, CORS desligado e health sem permissão; chamada sem Origin
exige JWT (401) e permissão (403); navegador permitido recebe origem, `X-Correlation-Id` exposto
e `Vary: Origin`; fora de Testing, HTTP só passa com `X-Forwarded-Proto=https` de proxy listado
(401 = transporte aceito e autenticação exigida); validação recusa origem com caminho, curinga,
HTTP em produção, IP inválido e `0.0.0.0/0`. Suíte Api.Agente 163/163.
