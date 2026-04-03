namespace Verbex.Database.Sqlite.Queries
{
    using System;
    using System.Collections.Generic;

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
        public const string SchemaVersion = "3.3";

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
    custom_metadata TEXT,
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
    custom_metadata TEXT,
    indexing_runtime_ms REAL,
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

-- Labels for tenants, users, credentials, documents, and indexes
CREATE TABLE IF NOT EXISTS labels (
    id TEXT PRIMARY KEY,
    tenant_id TEXT,
    user_id TEXT,
    credential_id TEXT,
    document_id TEXT,
    index_id TEXT,
    label TEXT NOT NULL,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE,
    FOREIGN KEY (credential_id) REFERENCES credentials(identifier) ON DELETE CASCADE,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
);

-- Tags (key-value pairs) for tenants, users, credentials, documents, and indexes
CREATE TABLE IF NOT EXISTS tags (
    id TEXT PRIMARY KEY,
    tenant_id TEXT,
    user_id TEXT,
    credential_id TEXT,
    document_id TEXT,
    index_id TEXT,
    key TEXT NOT NULL,
    value TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE,
    FOREIGN KEY (credential_id) REFERENCES credentials(identifier) ON DELETE CASCADE,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
);

-- Schema metadata
CREATE TABLE IF NOT EXISTS schema_metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Insert schema version
INSERT OR REPLACE INTO schema_metadata (key, value) VALUES ('schema_version', '3.3');
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
CREATE INDEX IF NOT EXISTS idx_tenants_created_utc ON tenants(created_utc);
CREATE INDEX IF NOT EXISTS idx_tenants_active_created_utc ON tenants(active, created_utc);

-- Administrator indexes
CREATE INDEX IF NOT EXISTS idx_administrators_email ON administrators(email);
CREATE INDEX IF NOT EXISTS idx_administrators_active ON administrators(active);
CREATE INDEX IF NOT EXISTS idx_administrators_created_utc ON administrators(created_utc);
CREATE INDEX IF NOT EXISTS idx_administrators_active_created_utc ON administrators(active, created_utc);

-- User indexes
CREATE INDEX IF NOT EXISTS idx_users_tenant ON users(tenant_id);
CREATE INDEX IF NOT EXISTS idx_users_email ON users(tenant_id, email);
CREATE INDEX IF NOT EXISTS idx_users_active ON users(active);
CREATE INDEX IF NOT EXISTS idx_users_tenant_active ON users(tenant_id, active);
CREATE INDEX IF NOT EXISTS idx_users_tenant_created_utc ON users(tenant_id, created_utc);
CREATE INDEX IF NOT EXISTS idx_users_tenant_active_created_utc ON users(tenant_id, active, created_utc);
CREATE INDEX IF NOT EXISTS idx_users_tenant_identifier ON users(tenant_id, identifier);

-- Credential indexes
CREATE INDEX IF NOT EXISTS idx_credentials_tenant ON credentials(tenant_id);
CREATE INDEX IF NOT EXISTS idx_credentials_user ON credentials(user_id);
CREATE INDEX IF NOT EXISTS idx_credentials_bearer ON credentials(bearer_token);
CREATE INDEX IF NOT EXISTS idx_credentials_active ON credentials(active);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_active ON credentials(tenant_id, active);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_created_utc ON credentials(tenant_id, created_utc);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_active_created_utc ON credentials(tenant_id, active, created_utc);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_user_created_utc ON credentials(tenant_id, user_id, created_utc);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_identifier ON credentials(tenant_id, identifier);

-- Index indexes
CREATE INDEX IF NOT EXISTS idx_indexes_tenant ON indexes(tenant_id);
CREATE INDEX IF NOT EXISTS idx_indexes_name ON indexes(tenant_id, name);
CREATE INDEX IF NOT EXISTS idx_indexes_tenant_created_utc ON indexes(tenant_id, created_utc);
CREATE INDEX IF NOT EXISTS idx_indexes_tenant_identifier ON indexes(tenant_id, identifier);

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
CREATE INDEX IF NOT EXISTS idx_terms_tenant_index_term ON terms(tenant_id, index_id, term);
CREATE INDEX IF NOT EXISTS idx_terms_document_frequency ON terms(document_frequency DESC);
CREATE INDEX IF NOT EXISTS idx_terms_orphan_cleanup ON terms(tenant_id, index_id, document_frequency);

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
CREATE INDEX IF NOT EXISTS idx_labels_tenant ON labels(tenant_id);
CREATE INDEX IF NOT EXISTS idx_labels_tenant_label ON labels(tenant_id, label);
CREATE INDEX IF NOT EXISTS idx_labels_user ON labels(user_id);
CREATE INDEX IF NOT EXISTS idx_labels_user_label ON labels(user_id, label);
CREATE INDEX IF NOT EXISTS idx_labels_credential ON labels(credential_id);
CREATE INDEX IF NOT EXISTS idx_labels_credential_label ON labels(credential_id, label);

-- Tag indexes (for filtering by key-value pairs)
CREATE INDEX IF NOT EXISTS idx_tags_document ON tags(document_id);
CREATE INDEX IF NOT EXISTS idx_tags_index ON tags(index_id);
CREATE INDEX IF NOT EXISTS idx_tags_key ON tags(key);
CREATE INDEX IF NOT EXISTS idx_tags_document_key ON tags(document_id, key);
CREATE INDEX IF NOT EXISTS idx_tags_index_key ON tags(index_id, key);
CREATE INDEX IF NOT EXISTS idx_tags_key_value ON tags(key, value);
CREATE INDEX IF NOT EXISTS idx_tags_tenant ON tags(tenant_id);
CREATE INDEX IF NOT EXISTS idx_tags_tenant_key ON tags(tenant_id, key);
CREATE INDEX IF NOT EXISTS idx_tags_user ON tags(user_id);
CREATE INDEX IF NOT EXISTS idx_tags_user_key ON tags(user_id, key);
CREATE INDEX IF NOT EXISTS idx_tags_credential ON tags(credential_id);
CREATE INDEX IF NOT EXISTS idx_tags_credential_key ON tags(credential_id, key);
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
PRAGMA busy_timeout = 30000;
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

        /// <summary>
        /// Gets migration SQL from schema v3.0 to v3.1.
        /// Adds tenant_id, user_id, credential_id columns to labels and tags tables.
        /// </summary>
        /// <returns>Migration SQL.</returns>
        public static string? GetMigrationFromV3()
        {
            return @"
-- Migration from Schema v3.0 to v3.1
-- Adds labels and tags support for tenants, users, and credentials

-- Add new columns to labels table
ALTER TABLE labels ADD COLUMN tenant_id TEXT REFERENCES tenants(identifier) ON DELETE CASCADE;
ALTER TABLE labels ADD COLUMN user_id TEXT REFERENCES users(identifier) ON DELETE CASCADE;
ALTER TABLE labels ADD COLUMN credential_id TEXT REFERENCES credentials(identifier) ON DELETE CASCADE;

-- Add new columns to tags table
ALTER TABLE tags ADD COLUMN tenant_id TEXT REFERENCES tenants(identifier) ON DELETE CASCADE;
ALTER TABLE tags ADD COLUMN user_id TEXT REFERENCES users(identifier) ON DELETE CASCADE;
ALTER TABLE tags ADD COLUMN credential_id TEXT REFERENCES credentials(identifier) ON DELETE CASCADE;

-- Create new indexes for labels
CREATE INDEX IF NOT EXISTS idx_labels_tenant ON labels(tenant_id);
CREATE INDEX IF NOT EXISTS idx_labels_tenant_label ON labels(tenant_id, label);
CREATE INDEX IF NOT EXISTS idx_labels_user ON labels(user_id);
CREATE INDEX IF NOT EXISTS idx_labels_user_label ON labels(user_id, label);
CREATE INDEX IF NOT EXISTS idx_labels_credential ON labels(credential_id);
CREATE INDEX IF NOT EXISTS idx_labels_credential_label ON labels(credential_id, label);

-- Create new indexes for tags
CREATE INDEX IF NOT EXISTS idx_tags_tenant ON tags(tenant_id);
CREATE INDEX IF NOT EXISTS idx_tags_tenant_key ON tags(tenant_id, key);
CREATE INDEX IF NOT EXISTS idx_tags_user ON tags(user_id);
CREATE INDEX IF NOT EXISTS idx_tags_user_key ON tags(user_id, key);
CREATE INDEX IF NOT EXISTS idx_tags_credential ON tags(credential_id);
CREATE INDEX IF NOT EXISTS idx_tags_credential_key ON tags(credential_id, key);

-- Update schema version
UPDATE schema_metadata SET value = '3.1' WHERE key = 'schema_version';
";
        }

        /// <summary>
        /// Gets migration SQL from schema v3.1 to v3.2.
        /// Adds custom_metadata column to indexes and documents tables.
        /// </summary>
        /// <returns>Migration SQL.</returns>
        public static string? GetMigrationFromV31()
        {
            return @"
-- Migration from Schema v3.1 to v3.2
-- Adds custom_metadata support for indexes and documents

-- Add custom_metadata column to indexes table
ALTER TABLE indexes ADD COLUMN custom_metadata TEXT;

-- Add custom_metadata column to documents table
ALTER TABLE documents ADD COLUMN custom_metadata TEXT;

-- Update schema version
UPDATE schema_metadata SET value = '3.2' WHERE key = 'schema_version';
";
        }

        /// <summary>
        /// Gets migration SQL from schema v3.2 to v3.3.
        /// Adds indexing_runtime_ms column to documents table.
        /// </summary>
        /// <returns>Migration SQL.</returns>
        public static string? GetMigrationFromV32()
        {
            return @"
-- Migration from Schema v3.2 to v3.3
-- Adds indexing_runtime_ms support for documents

-- Add indexing_runtime_ms column to documents table
ALTER TABLE documents ADD COLUMN indexing_runtime_ms REAL;

-- Update schema version
UPDATE schema_metadata SET value = '3.3' WHERE key = 'schema_version';
";
        }

        /// <summary>
        /// Generates SQL to create index-specific tables with the given table prefix.
        /// </summary>
        /// <param name="tablePrefix">The table prefix (typically the index identifier).</param>
        /// <returns>SQL CREATE TABLE statements for index-specific tables.</returns>
        /// <exception cref="ArgumentException">Thrown when the table prefix is invalid.</exception>
        public static string CreateIndexTables(string tablePrefix)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);

            return $@"
-- Index-specific tables for prefix: {prefix}

-- Documents table
CREATE TABLE IF NOT EXISTS {prefix}_documents (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    content_sha256 TEXT,
    document_length INTEGER,
    term_count INTEGER,
    custom_metadata TEXT,
    indexing_runtime_ms REAL,
    indexed_utc TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL
);

-- Terms table (vocabulary)
CREATE TABLE IF NOT EXISTS {prefix}_terms (
    id TEXT PRIMARY KEY,
    term TEXT NOT NULL UNIQUE,
    document_frequency INTEGER DEFAULT 0,
    total_frequency INTEGER DEFAULT 0,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL
);

-- Document-term mappings (inverted index)
CREATE TABLE IF NOT EXISTS {prefix}_document_terms (
    id TEXT PRIMARY KEY,
    document_id TEXT NOT NULL,
    term_id TEXT NOT NULL,
    term_frequency INTEGER DEFAULT 0,
    character_positions TEXT,
    term_positions TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    UNIQUE(document_id, term_id)
);

-- Labels for documents or index level
CREATE TABLE IF NOT EXISTS {prefix}_labels (
    id TEXT PRIMARY KEY,
    document_id TEXT,
    label TEXT NOT NULL,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL
);

-- Tags (key-value pairs) for documents or index level
CREATE TABLE IF NOT EXISTS {prefix}_tags (
    id TEXT PRIMARY KEY,
    document_id TEXT,
    key TEXT NOT NULL,
    value TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL
);
";
        }

        /// <summary>
        /// Generates SQL to drop index-specific tables with the given table prefix.
        /// </summary>
        /// <param name="tablePrefix">The table prefix (typically the index identifier).</param>
        /// <returns>SQL DROP TABLE statements for index-specific tables.</returns>
        /// <exception cref="ArgumentException">Thrown when the table prefix is invalid.</exception>
        public static string DropIndexTables(string tablePrefix)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);

            return $@"
-- Drop index-specific tables for prefix: {prefix}
DROP TABLE IF EXISTS {prefix}_tags;
DROP TABLE IF EXISTS {prefix}_labels;
DROP TABLE IF EXISTS {prefix}_document_terms;
DROP TABLE IF EXISTS {prefix}_terms;
DROP TABLE IF EXISTS {prefix}_documents;
";
        }

        /// <summary>
        /// Generates SQL statements to create indexes for index-specific tables.
        /// </summary>
        /// <param name="tablePrefix">The table prefix (typically the index identifier).</param>
        /// <returns>List of SQL CREATE INDEX statements.</returns>
        /// <exception cref="ArgumentException">Thrown when the table prefix is invalid.</exception>
        public static List<string> CreateIndexTableIndexes(string tablePrefix)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);

            return new List<string>
            {
                // Document indexes
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docs_name ON {prefix}_documents(name)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docs_sha256 ON {prefix}_documents(content_sha256)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docs_created_utc ON {prefix}_documents(created_utc)",

                // Term indexes (critical for search performance)
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_terms_term ON {prefix}_terms(term)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_terms_docfreq ON {prefix}_terms(document_frequency DESC)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_terms_orphan ON {prefix}_terms(document_frequency)",

                // Document-term indexes (critical for inverted index lookups)
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_doc ON {prefix}_document_terms(document_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_term ON {prefix}_document_terms(term_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_freq ON {prefix}_document_terms(term_frequency DESC)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_term_doc ON {prefix}_document_terms(term_id, document_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_search ON {prefix}_document_terms(term_id, document_id, term_frequency)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_doc_term ON {prefix}_document_terms(document_id, term_id)",

                // Label indexes
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_labels_doc ON {prefix}_labels(document_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_labels_label ON {prefix}_labels(label)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_labels_doc_label ON {prefix}_labels(document_id, label)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_labels_label_nocase ON {prefix}_labels(label COLLATE NOCASE)",

                // Tag indexes
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_tags_doc ON {prefix}_tags(document_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_tags_key ON {prefix}_tags(key)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_tags_doc_key ON {prefix}_tags(document_id, key)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_tags_key_value ON {prefix}_tags(key, value)"
            };
        }
    }
}
