namespace Verbex.Database.Sqlite.Queries
{
    using System;

    /// <summary>
    /// Provides SQL queries for SQLite schema setup and initialization.
    /// </summary>
    /// <remarks>
    /// Schema Version 3.0 - Multi-tenant architecture with full tenant isolation.
    /// </remarks>
    internal static class SetupQueries
    {
        /// <summary>
        /// The current schema version.
        /// </summary>
        public const string SchemaVersion = "3.0";

        /// <summary>
        /// Gets the SQL statements to create all tables.
        /// </summary>
        /// <returns>SQL CREATE TABLE statements.</returns>
        public static string CreateTables()
        {
            return @"
-- Schema Version 3.0 (Multi-tenant)

-- Tenants table
CREATE TABLE IF NOT EXISTS tenants (
    identifier TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);

-- Global administrators table
CREATE TABLE IF NOT EXISTS administrators (
    identifier TEXT PRIMARY KEY,
    email TEXT NOT NULL UNIQUE,
    password_sha256 TEXT NOT NULL,
    first_name TEXT,
    last_name TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);

-- Tenant users table
CREATE TABLE IF NOT EXISTS users (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    email TEXT NOT NULL,
    password_sha256 TEXT NOT NULL,
    first_name TEXT,
    last_name TEXT,
    is_admin INTEGER DEFAULT 0,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    UNIQUE(tenant_id, email)
);

-- User credentials (bearer tokens)
CREATE TABLE IF NOT EXISTS credentials (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    bearer_token TEXT NOT NULL UNIQUE,
    name TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE
);

-- Indexes (search indexes within tenants)
CREATE TABLE IF NOT EXISTS indexes (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    UNIQUE(tenant_id, name)
);

-- Documents within indexes
CREATE TABLE IF NOT EXISTS documents (
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    index_id TEXT NOT NULL,
    name TEXT NOT NULL,
    content_sha256 TEXT,
    document_length INTEGER,
    term_count INTEGER,
    indexed_utc TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE,
    UNIQUE(index_id, name)
);

-- Terms (vocabulary) within indexes
CREATE TABLE IF NOT EXISTS terms (
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    index_id TEXT NOT NULL,
    term TEXT NOT NULL,
    document_frequency INTEGER DEFAULT 0,
    total_frequency INTEGER DEFAULT 0,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE,
    UNIQUE(index_id, term)
);

-- Document-term mappings (inverted index)
CREATE TABLE IF NOT EXISTS document_terms (
    id TEXT PRIMARY KEY,
    document_id TEXT NOT NULL,
    term_id TEXT NOT NULL,
    term_frequency INTEGER DEFAULT 0,
    character_positions TEXT,
    term_positions TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (term_id) REFERENCES terms(id) ON DELETE CASCADE,
    UNIQUE(document_id, term_id)
);

-- Labels for documents and indexes
CREATE TABLE IF NOT EXISTS labels (
    id TEXT PRIMARY KEY,
    document_id TEXT,
    index_id TEXT,
    label TEXT NOT NULL,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
);

-- Tags (key-value pairs) for documents and indexes
CREATE TABLE IF NOT EXISTS tags (
    id TEXT PRIMARY KEY,
    document_id TEXT,
    index_id TEXT,
    key TEXT NOT NULL,
    value TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
);

-- Schema metadata
CREATE TABLE IF NOT EXISTS schema_metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Insert schema version
INSERT OR REPLACE INTO schema_metadata (key, value) VALUES ('schema_version', '3.0');
INSERT OR REPLACE INTO schema_metadata (key, value) VALUES ('created_utc', datetime('now'));
";
        }

        /// <summary>
        /// Gets the SQL statements to create all indexes.
        /// </summary>
        /// <returns>SQL CREATE INDEX statements.</returns>
        public static string CreateIndices()
        {
            return @"
-- Tenant indexes
CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants(name);
CREATE INDEX IF NOT EXISTS idx_tenants_active ON tenants(active);

-- Administrator indexes
CREATE INDEX IF NOT EXISTS idx_administrators_email ON administrators(email);
CREATE INDEX IF NOT EXISTS idx_administrators_active ON administrators(active);

-- User indexes
CREATE INDEX IF NOT EXISTS idx_users_tenant ON users(tenant_id);
CREATE INDEX IF NOT EXISTS idx_users_email ON users(tenant_id, email);
CREATE INDEX IF NOT EXISTS idx_users_active ON users(active);
CREATE INDEX IF NOT EXISTS idx_users_tenant_active ON users(tenant_id, active);

-- Credential indexes
CREATE INDEX IF NOT EXISTS idx_credentials_tenant ON credentials(tenant_id);
CREATE INDEX IF NOT EXISTS idx_credentials_user ON credentials(user_id);
CREATE INDEX IF NOT EXISTS idx_credentials_bearer ON credentials(bearer_token);
CREATE INDEX IF NOT EXISTS idx_credentials_active ON credentials(active);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_active ON credentials(tenant_id, active);

-- Index indexes
CREATE INDEX IF NOT EXISTS idx_indexes_tenant ON indexes(tenant_id);
CREATE INDEX IF NOT EXISTS idx_indexes_name ON indexes(tenant_id, name);

-- Document indexes
CREATE INDEX IF NOT EXISTS idx_documents_tenant ON documents(tenant_id);
CREATE INDEX IF NOT EXISTS idx_documents_index ON documents(index_id);
CREATE INDEX IF NOT EXISTS idx_documents_tenant_index ON documents(tenant_id, index_id);
CREATE INDEX IF NOT EXISTS idx_documents_name ON documents(index_id, name);
CREATE INDEX IF NOT EXISTS idx_documents_content_sha256 ON documents(content_sha256);

-- Term indexes (critical for search performance)
CREATE INDEX IF NOT EXISTS idx_terms_tenant ON terms(tenant_id);
CREATE INDEX IF NOT EXISTS idx_terms_index ON terms(index_id);
CREATE INDEX IF NOT EXISTS idx_terms_tenant_index ON terms(tenant_id, index_id);
CREATE INDEX IF NOT EXISTS idx_terms_term ON terms(index_id, term);
CREATE INDEX IF NOT EXISTS idx_terms_document_frequency ON terms(document_frequency DESC);

-- Document-term indexes (critical for inverted index lookups)
CREATE INDEX IF NOT EXISTS idx_document_terms_document ON document_terms(document_id);
CREATE INDEX IF NOT EXISTS idx_document_terms_term ON document_terms(term_id);
CREATE INDEX IF NOT EXISTS idx_document_terms_frequency ON document_terms(term_frequency DESC);
CREATE INDEX IF NOT EXISTS idx_document_terms_term_doc ON document_terms(term_id, document_id);

-- Label indexes (for filtering by labels)
CREATE INDEX IF NOT EXISTS idx_labels_document ON labels(document_id);
CREATE INDEX IF NOT EXISTS idx_labels_index ON labels(index_id);
CREATE INDEX IF NOT EXISTS idx_labels_label ON labels(label);
CREATE INDEX IF NOT EXISTS idx_labels_document_label ON labels(document_id, label);
CREATE INDEX IF NOT EXISTS idx_labels_index_label ON labels(index_id, label);

-- Tag indexes (for filtering by key-value pairs)
CREATE INDEX IF NOT EXISTS idx_tags_document ON tags(document_id);
CREATE INDEX IF NOT EXISTS idx_tags_index ON tags(index_id);
CREATE INDEX IF NOT EXISTS idx_tags_key ON tags(key);
CREATE INDEX IF NOT EXISTS idx_tags_document_key ON tags(document_id, key);
CREATE INDEX IF NOT EXISTS idx_tags_index_key ON tags(index_id, key);
CREATE INDEX IF NOT EXISTS idx_tags_key_value ON tags(key, value);
";
        }

        /// <summary>
        /// Gets the SQL to enable SQLite pragmas for optimal performance.
        /// </summary>
        /// <returns>PRAGMA statements.</returns>
        public static string GetPragmas()
        {
            return @"
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA foreign_keys = ON;
PRAGMA cache_size = -64000;
PRAGMA temp_store = MEMORY;
PRAGMA mmap_size = 268435456;
";
        }

        /// <summary>
        /// Gets the SQL to check if schema is initialized.
        /// </summary>
        /// <returns>SQL query to check schema version.</returns>
        public static string GetSchemaVersion()
        {
            return "SELECT value FROM schema_metadata WHERE key = 'schema_version';";
        }

        /// <summary>
        /// Gets migration SQL from schema v2 to v3.
        /// </summary>
        /// <returns>Migration SQL or null if no migration needed.</returns>
        public static string? GetMigrationFromV2()
        {
            return @"
-- Migration from Schema v2 to v3

-- Create new tables
CREATE TABLE IF NOT EXISTS tenants (
    identifier TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS administrators (
    identifier TEXT PRIMARY KEY,
    email TEXT NOT NULL UNIQUE,
    password_sha256 TEXT NOT NULL,
    first_name TEXT,
    last_name TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS users (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    email TEXT NOT NULL,
    password_sha256 TEXT NOT NULL,
    first_name TEXT,
    last_name TEXT,
    is_admin INTEGER DEFAULT 0,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    UNIQUE(tenant_id, email)
);

CREATE TABLE IF NOT EXISTS credentials (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    bearer_token TEXT NOT NULL UNIQUE,
    name TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE
);

-- Rename index_metadata to indexes and add tenant_id
ALTER TABLE index_metadata RENAME TO indexes_old;

CREATE TABLE IF NOT EXISTS indexes (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    UNIQUE(tenant_id, name)
);

-- Add tenant_id and index_id to documents
ALTER TABLE documents ADD COLUMN tenant_id TEXT;
ALTER TABLE documents ADD COLUMN index_id TEXT;

-- Add tenant_id and index_id to terms
ALTER TABLE terms ADD COLUMN tenant_id TEXT;
ALTER TABLE terms ADD COLUMN index_id TEXT;

-- Update schema version
UPDATE schema_metadata SET value = '3.0' WHERE key = 'schema_version';
";
        }
    }
}
