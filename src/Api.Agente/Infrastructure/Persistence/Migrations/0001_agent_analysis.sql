CREATE SCHEMA IF NOT EXISTS agente;
CREATE TABLE agente.executions (
    execution_id uuid PRIMARY KEY, principal_id text NOT NULL, organization_id text NULL,
    correlation_id text NOT NULL, state text NOT NULL CHECK (state IN ('running','completed','partial','failed','unsupported','cancelled')),
    error_code text NULL, started_at timestamptz NOT NULL, finished_at timestamptz NULL,
    expires_at timestamptz NOT NULL, policy_version text NOT NULL, approval_reference text NOT NULL,
    knowledge_calls jsonb NOT NULL DEFAULT '[]'::jsonb CHECK (jsonb_typeof(knowledge_calls)='array')
);
CREATE TABLE agente.consumption_attempts (
    attempt_id uuid PRIMARY KEY, execution_id uuid NOT NULL REFERENCES agente.executions,
    service text NOT NULL CHECK (service='Api.Agente'), provider text NOT NULL CHECK (provider='OpenAI'),
    model text NOT NULL, prompt_version text NOT NULL, started_at timestamptz NOT NULL,
    finished_at timestamptz NULL, state text NOT NULL CHECK (state IN ('pending','succeeded','failed','timeout','cancelled')),
    input_tokens bigint NULL CHECK (input_tokens>=0), output_tokens bigint NULL CHECK (output_tokens>=0),
    estimated_cost numeric NULL CHECK (estimated_cost>=0), estimated_currency text NULL,
    provider_reported_cost numeric NULL CHECK (provider_reported_cost>=0), currency text NULL,
    price_reference text NULL, price_version text NULL, expires_at timestamptz NOT NULL,
    policy_version text NOT NULL, approval_reference text NOT NULL,
    CHECK ((state='pending')=(finished_at IS NULL)), CHECK (finished_at IS NULL OR finished_at>=started_at)
);
CREATE INDEX execution_owner ON agente.executions(principal_id,organization_id,started_at);
CREATE INDEX attempt_execution ON agente.consumption_attempts(execution_id);
