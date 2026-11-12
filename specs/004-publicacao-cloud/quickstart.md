# Roteiro — publicar a demo (PC007–PC013)

Você executa os passos abaixo nas suas contas; nenhum foi executado por esta entrega. Nunca cole
segredos no chat, em issues, commits ou logs. Telas dos provedores mudam: confira as páginas
oficiais no dia (Vercel Functions/Containers e plano Hobby, Render Free, Neon pricing/pgvector,
Auth0 M2M). Use somente o Guia Prático público e arquivos SPED sintéticos.

Ordem: Auth0 → Neon (+ migrações) → Render (RAG) → ingestão → Vercel (Agente) → teste público.

## 1. Auth0 (PC007)

1. Criar tenant (ex.: `sped-demo`, região US). Anotar o domínio: `https://<TENANT>.us.auth0.com/`
   (com `/` no fim; é o `Authentication__Authority` das duas APIs).
2. Applications > APIs > Create API:
   - **Api.RAG** — Identifier `https://api-rag.sped.demo`, RS256. Permissions: `knowledge.read`,
     `knowledge.ingest`, `knowledge.audit`. Em Settings, "Token Expiration" até 2592000 (30 dias).
   - **Api.Agente** — Identifier `https://api-agente.sped.demo`. Permissions: `analysis.execute`,
     `analysis.diagnose`.
3. Applications > Create Application > Machine to Machine:
   - **agente-para-rag** → autorizar em Api.RAG só `knowledge.read`. O token vira
     `KnowledgeMcp__BearerToken` da Vercel (passo 5).
   - **rag-admin** → autorizar em Api.RAG `knowledge.ingest` e `knowledge.audit`. O `sub` dos tokens
     é `<CLIENT_ID>@clients`; ele vai em `Authentication__CentralPermissionAssignments__0__PrincipalId`.
   - **sofia-demo** → autorizar em Api.Agente `analysis.execute` (cliente de teste/SOFIA).
4. Para obter um token (PowerShell; Client Secret digitado, não salvo em arquivo):

```powershell
$secret = Read-Host 'Client Secret' -AsSecureString
$body = @{ grant_type = 'client_credentials'; client_id = '<CLIENT_ID>'; audience = 'https://api-rag.sped.demo'
           client_secret = [System.Net.NetworkCredential]::new('', $secret).Password }
$token = (Invoke-RestMethod -Method Post -Uri 'https://<TENANT>.us.auth0.com/oauth/token' -Body $body).access_token
```

## 2. Neon (PC008)

1. Criar projeto Free na região mais próxima do Render/Vercel escolhidos (ex.: AWS us-east-1).
2. Databases: criar `rag` e `agente`. Roles: criar `rag_application` e `agente_application`
   (senhas geradas pelo Neon). O papel dono do projeto (`neondb_owner`) é o de migração.
3. Connection strings em formato Npgsql, sempre com `SSL Mode=Require` e endpoint **direto**
   (sem `-pooler`) para migrações:
   `Host=<EP>.<REGIAO>.aws.neon.tech;Database=rag;Username=neondb_owner;Password=<...>;SSL Mode=Require`
4. Migrações (da sua máquina, sessão PowerShell nova; nada vai para arquivo):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ConnectionStrings__RagMigration = Read-Host 'Neon rag (neondb_owner)'
$env:ConnectionStrings__Rag = Read-Host 'Neon rag (rag_application)'
dotnet run --project src/Api.RAG --no-launch-profile -- --migrate
$env:AgentData__MigrationConnectionString = Read-Host 'Neon agente (neondb_owner)'
$env:AgentData__ConnectionString = Read-Host 'Neon agente (agente_application)'
$env:AgentData__PolicyVersion = 'demo-cloud-v1'; $env:AgentData__ApprovalReference = 'DEMO-SYNTHETIC-NOT-APPROVED'
$env:AgentData__AuditDays = '7'; $env:AgentData__ConsumptionDays = '7'
dotnet run --project src/Api.Agente --no-launch-profile -- --migrate-agent
```

   A migração 0001 da RAG cria a extensão `vector`; confira no SQL Editor:
   `SELECT extname, extversion FROM pg_extension WHERE extname = 'vector';`
   Rodar de novo é seguro (checksums); alterar SQL já aplicado falha de propósito.

## 3. Render — Api.RAG (PC009)

1. New > Web Service > repositório GitHub; Runtime **Docker**; Dockerfile Path
   `src/Api.RAG/Dockerfile`; Docker Build Context `.`; Instance **Free**; mesma região do Neon.
2. Environment: copiar `deploy/render-api-rag.env.example`, preencher os `<...>` e marcar segredos
   (`ConnectionStrings__Rag` com `rag_application`, `OpenAI__ApiKey`). Primeira vez:
   `Transport__TrustedNetworks__0=10.0.0.0/8` e `Logging__LogLevel__Microsoft.AspNetCore.HttpOverrides=Debug`.
3. Deploy. Verificação: `POST https://<SERVICO-RAG>.onrender.com/mcp` sem token deve dar **401**.
   Se der 400 `INVALID_ARGUMENT`, ver nos logs "Unknown proxy" e ajustar a rede; depois retirar o log Debug.
4. Ingestão do guia (≈7 min, ≈US$ 0,01; manter a operação sendo consultada para o serviço não dormir):

```powershell
$env:RAG_ACCESS_TOKEN = $token   # token do rag-admin (audience Api.RAG)
./scripts/local/ingest-sample.ps1 -BaseUrl https://<SERVICO-RAG>.onrender.com -PdfPath "<guia 1.35>.pdf" `
  -OfficialIdentifier guia-pratico-efd-contribuicoes -OfficialVersion 1.35 -Title 'Guia Prático da EFD-Contribuições' `
  -OriginUri 'https://example.com/guia-pratico.pdf' -PollSeconds 900
Remove-Item Env:RAG_ACCESS_TOKEN
```

## 4. Vercel — Api.Agente (PC010)

1. Add New > Project > importar o repositório; Root Directory = raiz (onde está `Dockerfile.vercel`).
   Confirmar no dia como a Vercel ativa o Container Runtime (beta) para `Dockerfile.vercel`.
2. Environment Variables (Production): copiar `deploy/vercel-api-agente.env.example`, preencher e
   marcar como Sensitive: `KnowledgeMcp__BearerToken` (token do agente-para-rag),
   `Llm__ApiKey`, `AgentData__ConnectionString` (`agente_application`). Primeira vez:
   `Transport__TrustedNetworks__0=10.0.0.0/8` + log Debug de HttpOverrides, como no Render.
3. Deploy. Verificação: `GET https://<PROJETO>.vercel.app/health/live` → **200**;
   `POST /api/v1/analises` sem token → **401**.

## 5. Teste ponta a ponta público (PC011)

```powershell
# token do sofia-demo (audience https://api-agente.sped.demo)
$sped = "|0000|006|0|||01012024|31012024|Empresa Sintetica|`r`n|9999|2|`r`n"
$r = Invoke-RestMethod -Method Post -Uri 'https://<PROJETO>.vercel.app/api/v1/analises' -Headers @{ Authorization = "Bearer $token" } `
  -ContentType 'text/plain; charset=iso-8859-1' -Body ([Text.Encoding]::Latin1.GetBytes($sped)) -TimeoutSec 180
$r.status; $r.evidences | ForEach-Object { $_.location.physicalPages }
Invoke-RestMethod -Uri "https://<PROJETO>.vercel.app/api/v1/analises/$($r.executionId)/consumo" -Headers @{ Authorization = "Bearer $token" }
```

Esperado: `partial`, evidências das p.67/68 e 416/420, parecer com referências e custo estimado.
A primeira chamada pode demorar (Render e Neon acordando). Conferir nos logs das duas
plataformas que não aparecem tokens, chaves, nomes ou conteúdo do arquivo.

## 6. Rollback e rotação (PC012)

- Vercel: Deployments > deploy anterior > Promote/Instant Rollback.
- Render: Events/Deploys > deploy anterior > Rollback.
- Banco: migrações são só aditivas; o código anterior continua compatível. Para desfazer dados,
  usar restore/branch do Neon dentro da janela do plano, ou recriar o banco e reingerir.
- Token M2M: gerar novo no Auth0, atualizar `KnowledgeMcp__BearerToken` na Vercel e redeployar
  antes de expirar. Chave vazada: revogar no provedor e trocar a variável imediatamente.
- Remover a demo: pausar/excluir serviço no Render, projeto na Vercel, projeto no Neon e as
  aplicações no Auth0.
