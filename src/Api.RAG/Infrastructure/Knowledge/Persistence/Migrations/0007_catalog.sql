ALTER TABLE rag.generations ADD COLUMN embedding_state text NOT NULL DEFAULT 'queued'
    CHECK (embedding_state IN ('queued','processing','ready','failed'));
ALTER TABLE rag.generations ADD COLUMN embedding_owner uuid NULL;
ALTER TABLE rag.generations ADD COLUMN embedding_until timestamptz NULL;
ALTER TABLE rag.generations ADD COLUMN embedding_attempts integer NOT NULL DEFAULT 0 CHECK (embedding_attempts >= 0);
ALTER TABLE rag.generations ADD COLUMN expected_revision uuid NULL;
ALTER TABLE rag.generations ADD COLUMN expected_active_generation_id uuid NULL;
ALTER TABLE rag.generations ADD COLUMN activated_at timestamptz NULL;
ALTER TABLE rag.generations ADD CONSTRAINT embedding_lease CHECK
    ((embedding_state='processing')=(embedding_owner IS NOT NULL) AND (embedding_owner IS NULL)=(embedding_until IS NULL));
ALTER TABLE rag.chunks ADD COLUMN embedding vector NULL;
ALTER TABLE rag.operations DROP CONSTRAINT operations_type_check;
ALTER TABLE rag.operations ADD CONSTRAINT operations_type_check CHECK (type IN ('ingestion','approve','reprocess','withdraw','expurge','query'));
ALTER TABLE rag.operations ADD COLUMN reason text NULL CHECK (length(reason)<=1000);
CREATE TABLE rag.catalog (
    singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
    active_profile_id uuid NOT NULL REFERENCES rag.processing_profiles,
    revision uuid NOT NULL
    ,catalog_id uuid NOT NULL UNIQUE
);
CREATE INDEX generation_embedding_queue ON rag.generations(created_at) WHERE preparation_state='ready' AND embedding_state IN ('queued','processing');
