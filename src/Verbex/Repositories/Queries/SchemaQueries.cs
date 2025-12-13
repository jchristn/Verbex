namespace Verbex.Repositories.Queries
{
    /// <summary>
    /// Contains SQL queries for schema creation and management.
    /// </summary>
    internal static class SchemaQueries
    {
        /// <summary>
        /// Returns the complete schema creation SQL.
        /// </summary>
        /// <returns>SQL string to create all tables and indices.</returns>
        internal static string CreateSchema()
        {
            return @"
                -- Documents table
                CREATE TABLE IF NOT EXISTS documents (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    content_sha256 TEXT,
                    document_length INTEGER NOT NULL,
                    term_count INTEGER NOT NULL DEFAULT 0,
                    indexed_utc TEXT NOT NULL,
                    last_modified_utc TEXT,
                    created_utc TEXT NOT NULL,
                    UNIQUE(name)
                );

                CREATE INDEX IF NOT EXISTS idx_documents_name ON documents(name);
                CREATE INDEX IF NOT EXISTS idx_documents_content_sha256 ON documents(content_sha256);
                CREATE INDEX IF NOT EXISTS idx_documents_indexed_utc ON documents(indexed_utc);

                -- Terms table
                CREATE TABLE IF NOT EXISTS terms (
                    id TEXT PRIMARY KEY,
                    term TEXT NOT NULL UNIQUE,
                    document_frequency INTEGER NOT NULL DEFAULT 0,
                    total_frequency INTEGER NOT NULL DEFAULT 0,
                    last_updated_utc TEXT NOT NULL,
                    created_utc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_terms_term ON terms(term);
                CREATE INDEX IF NOT EXISTS idx_terms_document_frequency ON terms(document_frequency DESC);

                -- Document-Terms mapping table
                CREATE TABLE IF NOT EXISTS document_terms (
                    id TEXT PRIMARY KEY,
                    document_id TEXT NOT NULL,
                    term_id TEXT NOT NULL,
                    term_frequency INTEGER NOT NULL,
                    character_positions TEXT NOT NULL,
                    term_positions TEXT NOT NULL,
                    last_modified_utc TEXT NOT NULL,
                    created_utc TEXT NOT NULL,
                    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
                    FOREIGN KEY (term_id) REFERENCES terms(id) ON DELETE CASCADE,
                    UNIQUE(document_id, term_id)
                );

                CREATE INDEX IF NOT EXISTS idx_document_terms_document_id ON document_terms(document_id);
                CREATE INDEX IF NOT EXISTS idx_document_terms_term_id ON document_terms(term_id);
                CREATE INDEX IF NOT EXISTS idx_document_terms_frequency ON document_terms(term_frequency DESC);

                -- Labels table
                CREATE TABLE IF NOT EXISTS labels (
                    id TEXT PRIMARY KEY,
                    document_id TEXT,
                    label TEXT NOT NULL,
                    last_modified_utc TEXT NOT NULL,
                    created_utc TEXT NOT NULL,
                    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
                    UNIQUE(document_id, label)
                );

                CREATE INDEX IF NOT EXISTS idx_labels_document_id ON labels(document_id);
                CREATE INDEX IF NOT EXISTS idx_labels_label ON labels(label);
                CREATE INDEX IF NOT EXISTS idx_labels_document_label ON labels(document_id, label);

                -- Tags table
                CREATE TABLE IF NOT EXISTS tags (
                    id TEXT PRIMARY KEY,
                    document_id TEXT,
                    key TEXT NOT NULL,
                    value TEXT,
                    last_modified_utc TEXT NOT NULL,
                    created_utc TEXT NOT NULL,
                    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
                    UNIQUE(document_id, key)
                );

                CREATE INDEX IF NOT EXISTS idx_tags_document_id ON tags(document_id);
                CREATE INDEX IF NOT EXISTS idx_tags_key ON tags(key);
                CREATE INDEX IF NOT EXISTS idx_tags_document_key ON tags(document_id, key);
                CREATE INDEX IF NOT EXISTS idx_tags_key_value ON tags(key, value);

                -- Index metadata table (single row)
                CREATE TABLE IF NOT EXISTS index_metadata (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    last_modified_utc TEXT NOT NULL,
                    created_utc TEXT NOT NULL
                );
            ";
        }

        /// <summary>
        /// Returns SQL to apply performance-related PRAGMA settings.
        /// </summary>
        /// <returns>SQL string with PRAGMA statements.</returns>
        internal static string ApplyPragmaSettings()
        {
            return @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA cache_size = -64000;
                PRAGMA temp_store = MEMORY;
                PRAGMA mmap_size = 268435456;
                PRAGMA busy_timeout = 5000;
                PRAGMA foreign_keys = ON;
            ";
        }

        /// <summary>
        /// Returns SQL to check if schema exists.
        /// </summary>
        /// <returns>SQL string.</returns>
        internal static string CheckSchemaExists()
        {
            return "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='index_metadata';";
        }

        /// <summary>
        /// Returns SQL to drop all tables (for testing/reset).
        /// </summary>
        /// <returns>SQL string.</returns>
        internal static string DropAllTables()
        {
            return @"
                DROP TABLE IF EXISTS document_terms;
                DROP TABLE IF EXISTS labels;
                DROP TABLE IF EXISTS tags;
                DROP TABLE IF EXISTS terms;
                DROP TABLE IF EXISTS documents;
                DROP TABLE IF EXISTS index_metadata;
            ";
        }
    }
}
