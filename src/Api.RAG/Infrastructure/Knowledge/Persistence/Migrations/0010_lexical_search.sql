-- Busca híbrida: índice textual (português) combinado à busca vetorial. Inclui o título da seção
-- (location.section) para que trechos de continuação de um registro também sejam encontrados pelo código.
ALTER TABLE rag.chunks ADD COLUMN text_search tsvector
    GENERATED ALWAYS AS (to_tsvector('portuguese', coalesce(location->>'section', '') || ' ' || coalesce(text, ''))) STORED;
CREATE INDEX chunks_text_search ON rag.chunks USING gin (text_search);
