namespace Verbex.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// PostgreSQL setup queries for creating and managing the database schema.
    /// </summary>
    internal static class SetupQueries
    {
        /// <summary>
        /// Schema version for migration tracking.
        /// </summary>
        public const string SchemaVersion = "3.3";

        /// <summary>
        /// Creates all tables for the multi-tenant inverted index.
        /// </summary>
        public static readonly string CreateTables = @"
-- Tenants table
CREATE TABLE IF NOT EXISTS tenants (
    identifier VARCHAR(48) PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    description TEXT,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Administrators table (global admins)
CREATE TABLE IF NOT EXISTS administrators (
    identifier VARCHAR(48) PRIMARY KEY,
    email VARCHAR(256) NOT NULL UNIQUE,
    password_sha256 VARCHAR(128) NOT NULL,
    first_name VARCHAR(128),
    last_name VARCHAR(128),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Users table (tenant-scoped)
CREATE TABLE IF NOT EXISTS users (
    identifier VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL REFERENCES tenants(identifier) ON DELETE CASCADE,
    email VARCHAR(256) NOT NULL,
    password_sha256 VARCHAR(128) NOT NULL,
    first_name VARCHAR(128),
    last_name VARCHAR(128),
    is_admin BOOLEAN NOT NULL DEFAULT FALSE,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, email)
);

-- Credentials table (bearer tokens)
CREATE TABLE IF NOT EXISTS credentials (
    identifier VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL REFERENCES tenants(identifier) ON DELETE CASCADE,
    user_id VARCHAR(48) NOT NULL REFERENCES users(identifier) ON DELETE CASCADE,
    bearer_token VARCHAR(64) NOT NULL UNIQUE,
    name VARCHAR(256),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes table (tenant-scoped)
CREATE TABLE IF NOT EXISTS indexes (
    identifier VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL REFERENCES tenants(identifier) ON DELETE CASCADE,
    name VARCHAR(256) NOT NULL,
    description TEXT,
    custom_metadata TEXT,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, name)
);

-- Documents table
CREATE TABLE IF NOT EXISTS documents (
    id VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL REFERENCES tenants(identifier) ON DELETE CASCADE,
    index_id VARCHAR(48) NOT NULL REFERENCES indexes(identifier) ON DELETE CASCADE,
    name VARCHAR(512) NOT NULL,
    content_sha256 VARCHAR(64),
    document_length INTEGER NOT NULL DEFAULT 0,
    term_count INTEGER NOT NULL DEFAULT 0,
    custom_metadata TEXT,
    indexing_runtime_ms NUMERIC(18,4),
    indexed_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(index_id, name)
);

-- Terms table (vocabulary)
CREATE TABLE IF NOT EXISTS terms (
    id VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL REFERENCES tenants(identifier) ON DELETE CASCADE,
    index_id VARCHAR(48) NOT NULL REFERENCES indexes(identifier) ON DELETE CASCADE,
    term VARCHAR(512) NOT NULL,
    document_frequency INTEGER NOT NULL DEFAULT 0,
    total_frequency BIGINT NOT NULL DEFAULT 0,
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(index_id, term)
);

-- Document-Terms table (inverted index)
CREATE TABLE IF NOT EXISTS document_terms (
    id VARCHAR(48) PRIMARY KEY,
    document_id VARCHAR(48) NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    term_id VARCHAR(48) NOT NULL REFERENCES terms(id) ON DELETE CASCADE,
    term_frequency INTEGER NOT NULL DEFAULT 0,
    character_positions TEXT,
    term_positions TEXT,
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(document_id, term_id)
);

-- Labels table (tenant, user, credential, document, or index level)
CREATE TABLE IF NOT EXISTS labels (
    id VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) REFERENCES tenants(identifier) ON DELETE CASCADE,
    user_id VARCHAR(48) REFERENCES users(identifier) ON DELETE CASCADE,
    credential_id VARCHAR(48) REFERENCES credentials(identifier) ON DELETE CASCADE,
    document_id VARCHAR(48) REFERENCES documents(id) ON DELETE CASCADE,
    index_id VARCHAR(48) REFERENCES indexes(identifier) ON DELETE CASCADE,
    label VARCHAR(256) NOT NULL,
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Tags table (tenant, user, credential, document, or index level key-value pairs)
CREATE TABLE IF NOT EXISTS tags (
    id VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) REFERENCES tenants(identifier) ON DELETE CASCADE,
    user_id VARCHAR(48) REFERENCES users(identifier) ON DELETE CASCADE,
    credential_id VARCHAR(48) REFERENCES credentials(identifier) ON DELETE CASCADE,
    document_id VARCHAR(48) REFERENCES documents(id) ON DELETE CASCADE,
    index_id VARCHAR(48) REFERENCES indexes(identifier) ON DELETE CASCADE,
    key VARCHAR(256) NOT NULL,
    value TEXT,
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Schema metadata table
CREATE TABLE IF NOT EXISTS schema_metadata (
    key VARCHAR(256) PRIMARY KEY,
    value TEXT NOT NULL
);
";

        /// <summary>
        /// Creates indexes for common queries. Each query should be executed individually.
        /// PostgreSQL supports CREATE INDEX IF NOT EXISTS syntax.
        /// </summary>
        public static readonly List<string> CreateIndexes = new List<string>
        {
            // Tenant indexes
            "CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants(name)",
            "CREATE INDEX IF NOT EXISTS idx_tenants_active ON tenants(active)",

            // Administrator indexes
            "CREATE INDEX IF NOT EXISTS idx_administrators_email ON administrators(email)",
            "CREATE INDEX IF NOT EXISTS idx_administrators_active ON administrators(active)",

            // User indexes
            "CREATE INDEX IF NOT EXISTS idx_users_tenant ON users(tenant_id)",
            "CREATE INDEX IF NOT EXISTS idx_users_email ON users(tenant_id, email)",
            "CREATE INDEX IF NOT EXISTS idx_users_active ON users(active)",
            "CREATE INDEX IF NOT EXISTS idx_users_tenant_active ON users(tenant_id, active)",

            // Credential indexes
            "CREATE INDEX IF NOT EXISTS idx_credentials_tenant ON credentials(tenant_id)",
            "CREATE INDEX IF NOT EXISTS idx_credentials_user ON credentials(user_id)",
            "CREATE INDEX IF NOT EXISTS idx_credentials_bearer ON credentials(bearer_token)",
            "CREATE INDEX IF NOT EXISTS idx_credentials_active ON credentials(active)",
            "CREATE INDEX IF NOT EXISTS idx_credentials_tenant_active ON credentials(tenant_id, active)",

            // Index (search index) indexes
            "CREATE INDEX IF NOT EXISTS idx_indexes_tenant ON indexes(tenant_id)",
            "CREATE INDEX IF NOT EXISTS idx_indexes_name ON indexes(tenant_id, name)",

            // Document indexes
            "CREATE INDEX IF NOT EXISTS idx_documents_tenant ON documents(tenant_id)",
            "CREATE INDEX IF NOT EXISTS idx_documents_index ON documents(index_id)",
            "CREATE INDEX IF NOT EXISTS idx_documents_tenant_index ON documents(tenant_id, index_id)",
            "CREATE INDEX IF NOT EXISTS idx_documents_name ON documents(index_id, name)",
            "CREATE INDEX IF NOT EXISTS idx_documents_content_sha256 ON documents(content_sha256)",

            // Term indexes (critical for search performance)
            "CREATE INDEX IF NOT EXISTS idx_terms_tenant ON terms(tenant_id)",
            "CREATE INDEX IF NOT EXISTS idx_terms_index ON terms(index_id)",
            "CREATE INDEX IF NOT EXISTS idx_terms_tenant_index ON terms(tenant_id, index_id)",
            "CREATE INDEX IF NOT EXISTS idx_terms_term ON terms(index_id, term)",
            "CREATE INDEX IF NOT EXISTS idx_terms_tenant_index_term ON terms(tenant_id, index_id, term)",
            "CREATE INDEX IF NOT EXISTS idx_terms_document_frequency ON terms(document_frequency DESC)",
            "CREATE INDEX IF NOT EXISTS idx_terms_orphan_cleanup ON terms(tenant_id, index_id, document_frequency)",

            // Document-term indexes (critical for inverted index lookups)
            "CREATE INDEX IF NOT EXISTS idx_document_terms_document ON document_terms(document_id)",
            "CREATE INDEX IF NOT EXISTS idx_document_terms_term ON document_terms(term_id)",
            "CREATE INDEX IF NOT EXISTS idx_document_terms_frequency ON document_terms(term_frequency DESC)",
            "CREATE INDEX IF NOT EXISTS idx_document_terms_term_doc ON document_terms(term_id, document_id)",

            // Label indexes (for filtering by labels)
            "CREATE INDEX IF NOT EXISTS idx_labels_document ON labels(document_id)",
            "CREATE INDEX IF NOT EXISTS idx_labels_index ON labels(index_id)",
            "CREATE INDEX IF NOT EXISTS idx_labels_label ON labels(label)",
            "CREATE INDEX IF NOT EXISTS idx_labels_document_label ON labels(document_id, label)",
            "CREATE INDEX IF NOT EXISTS idx_labels_index_label ON labels(index_id, label)",
            "CREATE INDEX IF NOT EXISTS idx_labels_tenant ON labels(tenant_id)",
            "CREATE INDEX IF NOT EXISTS idx_labels_tenant_label ON labels(tenant_id, label)",
            "CREATE INDEX IF NOT EXISTS idx_labels_user ON labels(user_id)",
            "CREATE INDEX IF NOT EXISTS idx_labels_user_label ON labels(user_id, label)",
            "CREATE INDEX IF NOT EXISTS idx_labels_credential ON labels(credential_id)",
            "CREATE INDEX IF NOT EXISTS idx_labels_credential_label ON labels(credential_id, label)",

            // Tag indexes (for filtering by key-value pairs)
            "CREATE INDEX IF NOT EXISTS idx_tags_document ON tags(document_id)",
            "CREATE INDEX IF NOT EXISTS idx_tags_index ON tags(index_id)",
            "CREATE INDEX IF NOT EXISTS idx_tags_key ON tags(key)",
            "CREATE INDEX IF NOT EXISTS idx_tags_document_key ON tags(document_id, key)",
            "CREATE INDEX IF NOT EXISTS idx_tags_index_key ON tags(index_id, key)",
            "CREATE INDEX IF NOT EXISTS idx_tags_key_value ON tags(key, value)",
            "CREATE INDEX IF NOT EXISTS idx_tags_tenant ON tags(tenant_id)",
            "CREATE INDEX IF NOT EXISTS idx_tags_tenant_key ON tags(tenant_id, key)",
            "CREATE INDEX IF NOT EXISTS idx_tags_user ON tags(user_id)",
            "CREATE INDEX IF NOT EXISTS idx_tags_user_key ON tags(user_id, key)",
            "CREATE INDEX IF NOT EXISTS idx_tags_credential ON tags(credential_id)",
            "CREATE INDEX IF NOT EXISTS idx_tags_credential_key ON tags(credential_id, key)"
        };

        /// <summary>
        /// Drops all tables in reverse order of creation.
        /// </summary>
        public static readonly string DropTables = @"
DROP TABLE IF EXISTS schema_metadata CASCADE;
DROP TABLE IF EXISTS tags CASCADE;
DROP TABLE IF EXISTS labels CASCADE;
DROP TABLE IF EXISTS document_terms CASCADE;
DROP TABLE IF EXISTS terms CASCADE;
DROP TABLE IF EXISTS documents CASCADE;
DROP TABLE IF EXISTS indexes CASCADE;
DROP TABLE IF EXISTS credentials CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS administrators CASCADE;
DROP TABLE IF EXISTS tenants CASCADE;
";

        /// <summary>
        /// Inserts the initial schema version into the metadata table.
        /// </summary>
        public static readonly string InsertSchemaVersion = @"
INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '3.3')
ON CONFLICT (key) DO UPDATE SET value = '3.3';
";

        /// <summary>
        /// Migration query from schema version 3.1 to 3.2.
        /// Adds custom_metadata column to documents and indexes tables.
        /// </summary>
        public static readonly string MigrateFrom31To32 = @"
-- Add custom_metadata column to documents table if it doesn't exist
ALTER TABLE documents ADD COLUMN IF NOT EXISTS custom_metadata TEXT;

-- Add custom_metadata column to indexes table if it doesn't exist
ALTER TABLE indexes ADD COLUMN IF NOT EXISTS custom_metadata TEXT;

-- Update schema version
UPDATE schema_metadata SET value = '3.2' WHERE key = 'schema_version';
";

        /// <summary>
        /// Migration query from schema version 3.2 to 3.3.
        /// Adds indexing_runtime_ms column to documents table.
        /// </summary>
        public static readonly string MigrateFrom32To33 = @"
-- Add indexing_runtime_ms column to documents table if it doesn't exist
ALTER TABLE documents ADD COLUMN IF NOT EXISTS indexing_runtime_ms NUMERIC(18,4);

-- Update schema version
UPDATE schema_metadata SET value = '3.3' WHERE key = 'schema_version';
";

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
    id VARCHAR(48) PRIMARY KEY,
    name VARCHAR(512) NOT NULL UNIQUE,
    content_sha256 VARCHAR(64),
    document_length INTEGER NOT NULL DEFAULT 0,
    term_count INTEGER NOT NULL DEFAULT 0,
    custom_metadata TEXT,
    indexing_runtime_ms NUMERIC(18,4),
    indexed_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Terms table (vocabulary)
CREATE TABLE IF NOT EXISTS {prefix}_terms (
    id VARCHAR(48) PRIMARY KEY,
    term VARCHAR(512) NOT NULL UNIQUE,
    document_frequency INTEGER NOT NULL DEFAULT 0,
    total_frequency BIGINT NOT NULL DEFAULT 0,
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Document-term mappings (inverted index)
CREATE TABLE IF NOT EXISTS {prefix}_document_terms (
    id VARCHAR(48) PRIMARY KEY,
    document_id VARCHAR(48) NOT NULL,
    term_id VARCHAR(48) NOT NULL,
    term_frequency INTEGER NOT NULL DEFAULT 0,
    character_positions TEXT,
    term_positions TEXT,
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(document_id, term_id)
);

-- Labels for documents or index level
CREATE TABLE IF NOT EXISTS {prefix}_labels (
    id VARCHAR(48) PRIMARY KEY,
    document_id VARCHAR(48),
    label VARCHAR(256) NOT NULL,
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Tags (key-value pairs) for documents or index level
CREATE TABLE IF NOT EXISTS {prefix}_tags (
    id VARCHAR(48) PRIMARY KEY,
    document_id VARCHAR(48),
    key VARCHAR(256) NOT NULL,
    value TEXT,
    last_update_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
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
DROP TABLE IF EXISTS {prefix}_tags CASCADE;
DROP TABLE IF EXISTS {prefix}_labels CASCADE;
DROP TABLE IF EXISTS {prefix}_document_terms CASCADE;
DROP TABLE IF EXISTS {prefix}_terms CASCADE;
DROP TABLE IF EXISTS {prefix}_documents CASCADE;
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

                // Term indexes (critical for search performance)
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_terms_term ON {prefix}_terms(term)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_terms_docfreq ON {prefix}_terms(document_frequency DESC)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_terms_orphan ON {prefix}_terms(document_frequency)",

                // Document-term indexes (critical for inverted index lookups)
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_doc ON {prefix}_document_terms(document_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_term ON {prefix}_document_terms(term_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_freq ON {prefix}_document_terms(term_frequency DESC)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_term_doc ON {prefix}_document_terms(term_id, document_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_docterms_doc_term ON {prefix}_document_terms(document_id, term_id)",

                // Label indexes
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_labels_doc ON {prefix}_labels(document_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_labels_label ON {prefix}_labels(label)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_labels_doc_label ON {prefix}_labels(document_id, label)",

                // Tag indexes
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_tags_doc ON {prefix}_tags(document_id)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_tags_key ON {prefix}_tags(key)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_tags_doc_key ON {prefix}_tags(document_id, key)",
                $"CREATE INDEX IF NOT EXISTS idx_{prefix}_tags_key_value ON {prefix}_tags(key, value)"
            };
        }
    }
}
