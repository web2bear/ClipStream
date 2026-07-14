namespace ClipStream.Infrastructure.Persistence;

public static class DatabaseSchema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS streams (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            icon TEXT,
            sort_order INTEGER NOT NULL DEFAULT 0,
            is_pinned INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS fragments (
            id TEXT PRIMARY KEY,
            captured_at TEXT NOT NULL,
            kind INTEGER NOT NULL,
            preview_text TEXT,
            source_process TEXT,
            source_process_id INTEGER,
            content_hash TEXT,
            metadata_json TEXT,
            title TEXT
        );

        CREATE TABLE IF NOT EXISTS fragment_streams (
            fragment_id TEXT NOT NULL,
            stream_id TEXT NOT NULL,
            routed_at TEXT NOT NULL,
            PRIMARY KEY (fragment_id, stream_id),
            FOREIGN KEY (fragment_id) REFERENCES fragments(id),
            FOREIGN KEY (stream_id) REFERENCES streams(id)
        );

        CREATE TABLE IF NOT EXISTS format_payloads (
            id TEXT PRIMARY KEY,
            fragment_id TEXT NOT NULL,
            format_name TEXT NOT NULL,
            storage_key TEXT NOT NULL,
            size_bytes INTEGER NOT NULL,
            content_hash TEXT,
            FOREIGN KEY (fragment_id) REFERENCES fragments(id)
        );

        CREATE TABLE IF NOT EXISTS routing_rules (
            id TEXT PRIMARY KEY,
            stream_id TEXT NOT NULL,
            priority INTEGER NOT NULL,
            condition_json TEXT NOT NULL,
            FOREIGN KEY (stream_id) REFERENCES streams(id)
        );

        CREATE INDEX IF NOT EXISTS idx_fragments_captured_at ON fragments(captured_at DESC);
        CREATE INDEX IF NOT EXISTS idx_fragments_content_hash ON fragments(content_hash);
        CREATE INDEX IF NOT EXISTS idx_fragment_streams_stream ON fragment_streams(stream_id, routed_at DESC);
        """;
}
