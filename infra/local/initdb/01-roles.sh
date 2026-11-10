#!/bin/sh
# Cria a identidade da aplicação (distinta da identidade de migração) e a extensão pgvector.
# O MigrationRunner concede os privilégios sobre o schema rag a esse papel.
set -eu
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v app_password="$RAG_APPLICATION_PASSWORD" <<'SQL'
CREATE EXTENSION IF NOT EXISTS vector;
SELECT format('CREATE ROLE rag_application LOGIN PASSWORD %L', :'app_password') \gexec
SQL
