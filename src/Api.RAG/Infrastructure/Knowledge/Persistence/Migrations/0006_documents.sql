CREATE TABLE rag.processing_profiles (
    profile_id uuid PRIMARY KEY,
    definition jsonb NOT NULL,
    definition_hash text NOT NULL CHECK (definition_hash ~ '^[0-9a-f]{64}$')
);
CREATE TABLE rag.sources (
    source_id uuid PRIMARY KEY,
    issuer text NOT NULL CHECK (length(issuer) BETWEEN 1 AND 1000),
    official_identifier text NOT NULL CHECK (length(official_identifier) BETWEEN 1 AND 1000),
    title text NOT NULL,
    kind text NOT NULL CHECK (kind IN ('manual','guide')),
    origin_uri text NOT NULL CHECK (origin_uri LIKE 'https://%'),
    created_at timestamptz NOT NULL,
    UNIQUE (issuer,official_identifier)
);
CREATE TABLE rag.document_versions (
    document_version_id uuid PRIMARY KEY,
    source_id uuid NOT NULL REFERENCES rag.sources,
    official_version text NOT NULL CHECK (length(official_version) BETWEEN 1 AND 1000),
    original_content_hash text NOT NULL CHECK (original_content_hash ~ '^[0-9a-f]{64}$'),
    original_artifact_id uuid NOT NULL UNIQUE,
    original bytea NOT NULL CHECK (octet_length(original) > 0),
    metadata jsonb NOT NULL,
    publication_state text NOT NULL DEFAULT 'unpublished' CHECK (publication_state IN ('unpublished','published','withdrawn','purged')),
    active_generation_id uuid NULL,
    approved_by text NULL,
    approved_at timestamptz NULL,
    withdrawn_at timestamptz NULL,
    revision uuid NOT NULL,
    created_at timestamptz NOT NULL,
    retention_policy_version text NOT NULL,
    UNIQUE (source_id,official_version),
    CHECK ((publication_state = 'published') = (active_generation_id IS NOT NULL)),
    CHECK ((approved_by IS NULL) = (approved_at IS NULL)),
    CHECK (publication_state <> 'published' OR approved_at IS NOT NULL),
    CHECK (publication_state <> 'withdrawn' OR withdrawn_at IS NOT NULL)
);
CREATE TABLE rag.applicabilities (
    document_version_id uuid NOT NULL REFERENCES rag.document_versions,
    document_type text NOT NULL CHECK (length(document_type) > 0),
    layout_version text NOT NULL CHECK (length(layout_version) > 0),
    official_basis text NOT NULL CHECK (length(official_basis) > 0),
    recorded_by text NOT NULL,
    recorded_at timestamptz NOT NULL,
    PRIMARY KEY (document_version_id,document_type,layout_version)
);
CREATE TABLE rag.generations (
    generation_id uuid PRIMARY KEY,
    document_version_id uuid NOT NULL REFERENCES rag.document_versions,
    operation_id uuid NOT NULL UNIQUE REFERENCES rag.operations,
    profile_id uuid NOT NULL REFERENCES rag.processing_profiles,
    source_hash text NOT NULL CHECK (source_hash ~ '^[0-9a-f]{64}$'),
    state text NOT NULL CHECK (state IN ('queued','processing','succeeded','failed','cancelled')),
    preparation_state text NOT NULL CHECK (preparation_state IN ('queued','processing','ready','failed')),
    created_at timestamptz NOT NULL,
    completed_at timestamptz NULL,
    superseded_at timestamptz NULL,
    content_manifest_hash text NULL CHECK (content_manifest_hash ~ '^[0-9a-f]{64}$'),
    preparation_manifest_hash text NULL CHECK (preparation_manifest_hash ~ '^[0-9a-f]{64}$'),
    failure_code text NULL,
    lease_owner uuid NULL,
    lease_until timestamptz NULL,
    UNIQUE (generation_id,document_version_id),
    CHECK ((lease_owner IS NULL) = (lease_until IS NULL)),
    CHECK ((preparation_state = 'processing') = (lease_owner IS NOT NULL)),
    CHECK (completed_at IS NULL OR completed_at >= created_at),
    CHECK (state NOT IN ('succeeded','failed','cancelled') OR completed_at IS NOT NULL),
    CHECK (state <> 'succeeded' OR (preparation_state='ready' AND content_manifest_hash IS NOT NULL))
);
ALTER TABLE rag.document_versions ADD CONSTRAINT active_generation_belongs_to_version
    FOREIGN KEY (active_generation_id,document_version_id) REFERENCES rag.generations (generation_id,document_version_id);
CREATE TABLE rag.chunks (
    chunk_id uuid PRIMARY KEY,
    generation_id uuid NOT NULL REFERENCES rag.generations,
    ordinal integer NOT NULL CHECK (ordinal >= 0),
    text text NOT NULL CHECK (length(text) > 0),
    text_hash text NOT NULL CHECK (text_hash ~ '^[0-9a-f]{64}$'),
    source_spans jsonb NOT NULL CHECK (jsonb_array_length(source_spans)>0),
    location jsonb NOT NULL,
    reference_id uuid NOT NULL UNIQUE,
    created_at timestamptz NOT NULL,
    UNIQUE (generation_id,ordinal)
);
CREATE INDEX generations_preparation_queue ON rag.generations (created_at) WHERE preparation_state IN ('queued','processing');
