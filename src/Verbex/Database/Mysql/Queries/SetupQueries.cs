namespace Verbex.Database.Mysql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// MySQL setup queries for creating and managing the database schema.
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
    active TINYINT(1) NOT NULL DEFAULT 1,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Administrators table (global admins)
CREATE TABLE IF NOT EXISTS administrators (
    identifier VARCHAR(48) PRIMARY KEY,
    email VARCHAR(256) NOT NULL UNIQUE,
    password_sha256 VARCHAR(128) NOT NULL,
    first_name VARCHAR(128),
    last_name VARCHAR(128),
    active TINYINT(1) NOT NULL DEFAULT 1,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Users table (tenant-scoped)
CREATE TABLE IF NOT EXISTS users (
    identifier VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL,
    email VARCHAR(256) NOT NULL,
    password_sha256 VARCHAR(128) NOT NULL,
    first_name VARCHAR(128),
    last_name VARCHAR(128),
    is_admin TINYINT(1) NOT NULL DEFAULT 0,
    active TINYINT(1) NOT NULL DEFAULT 1,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY unique_tenant_email (tenant_id, email),
    CONSTRAINT fk_users_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Credentials table (bearer tokens)
CREATE TABLE IF NOT EXISTS credentials (
    identifier VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL,
    user_id VARCHAR(48) NOT NULL,
    bearer_token VARCHAR(64) NOT NULL UNIQUE,
    name VARCHAR(256),
    active TINYINT(1) NOT NULL DEFAULT 1,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_credentials_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_credentials_user FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Indexes table (tenant-scoped)
CREATE TABLE IF NOT EXISTS indexes (
    identifier VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL,
    name VARCHAR(256) NOT NULL,
    description TEXT,
    custom_metadata TEXT,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY unique_tenant_name (tenant_id, name),
    CONSTRAINT fk_indexes_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Documents table
CREATE TABLE IF NOT EXISTS documents (
    id VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL,
    index_id VARCHAR(48) NOT NULL,
    name VARCHAR(512) NOT NULL,
    content_sha256 VARCHAR(64),
    document_length INT NOT NULL DEFAULT 0,
    term_count INT NOT NULL DEFAULT 0,
    custom_metadata TEXT,
    indexing_runtime_ms DECIMAL(18,4),
    indexed_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY unique_index_name (index_id, name),
    CONSTRAINT fk_documents_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_documents_index FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Terms table (vocabulary)
CREATE TABLE IF NOT EXISTS terms (
    id VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL,
    index_id VARCHAR(48) NOT NULL,
    term VARCHAR(512) NOT NULL,
    document_frequency INT NOT NULL DEFAULT 0,
    total_frequency BIGINT NOT NULL DEFAULT 0,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY unique_index_term (index_id, term),
    CONSTRAINT fk_terms_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_terms_index FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Document-Terms table (inverted index)
CREATE TABLE IF NOT EXISTS document_terms (
    id VARCHAR(48) PRIMARY KEY,
    document_id VARCHAR(48) NOT NULL,
    term_id VARCHAR(48) NOT NULL,
    term_frequency INT NOT NULL DEFAULT 0,
    character_positions TEXT,
    term_positions TEXT,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY unique_document_term (document_id, term_id),
    CONSTRAINT fk_document_terms_document FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    CONSTRAINT fk_document_terms_term FOREIGN KEY (term_id) REFERENCES terms(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Labels table (tenant, user, credential, document, or index level)
CREATE TABLE IF NOT EXISTS labels (
    id VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48),
    user_id VARCHAR(48),
    credential_id VARCHAR(48),
    document_id VARCHAR(48),
    index_id VARCHAR(48),
    label VARCHAR(256) NOT NULL,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_labels_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_labels_user FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_labels_credential FOREIGN KEY (credential_id) REFERENCES credentials(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_labels_document FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    CONSTRAINT fk_labels_index FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tags table (tenant, user, credential, document, or index level key-value pairs)
CREATE TABLE IF NOT EXISTS tags (
    id VARCHAR(48) PRIMARY KEY,
    tenant_id VARCHAR(48),
    user_id VARCHAR(48),
    credential_id VARCHAR(48),
    document_id VARCHAR(48),
    index_id VARCHAR(48),
    `key` VARCHAR(256) NOT NULL,
    value TEXT,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_tags_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_tags_user FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_tags_credential FOREIGN KEY (credential_id) REFERENCES credentials(identifier) ON DELETE CASCADE,
    CONSTRAINT fk_tags_document FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    CONSTRAINT fk_tags_index FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Schema metadata table
CREATE TABLE IF NOT EXISTS schema_metadata (
    `key` VARCHAR(256) PRIMARY KEY,
    value TEXT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
";

        /// <summary>
        /// Creates indexes for common queries. Each query should be executed individually
        /// with error handling for duplicate index errors (MySQL error 1061).
        /// </summary>
        /// <remarks>
        /// MySQL does not support CREATE INDEX IF NOT EXISTS syntax.
        /// Each index creation must be wrapped in try/catch to handle duplicate key errors.
        /// </remarks>
        public static readonly List<string> CreateIndexes = new List<string>
        {
            // Tenant indexes
            "CREATE INDEX idx_tenants_name ON tenants(name)",
            "CREATE INDEX idx_tenants_active ON tenants(active)",
            "CREATE INDEX idx_tenants_created_utc ON tenants(created_utc)",
            "CREATE INDEX idx_tenants_active_created_utc ON tenants(active, created_utc)",

            // Administrator indexes
            "CREATE INDEX idx_administrators_email ON administrators(email)",
            "CREATE INDEX idx_administrators_active ON administrators(active)",
            "CREATE INDEX idx_administrators_created_utc ON administrators(created_utc)",
            "CREATE INDEX idx_administrators_active_created_utc ON administrators(active, created_utc)",

            // User indexes
            "CREATE INDEX idx_users_tenant ON users(tenant_id)",
            "CREATE INDEX idx_users_email ON users(tenant_id, email)",
            "CREATE INDEX idx_users_active ON users(active)",
            "CREATE INDEX idx_users_tenant_active ON users(tenant_id, active)",
            "CREATE INDEX idx_users_tenant_created_utc ON users(tenant_id, created_utc)",
            "CREATE INDEX idx_users_tenant_active_created_utc ON users(tenant_id, active, created_utc)",
            "CREATE INDEX idx_users_tenant_identifier ON users(tenant_id, identifier)",

            // Credential indexes
            "CREATE INDEX idx_credentials_tenant ON credentials(tenant_id)",
            "CREATE INDEX idx_credentials_user ON credentials(user_id)",
            "CREATE INDEX idx_credentials_bearer ON credentials(bearer_token)",
            "CREATE INDEX idx_credentials_active ON credentials(active)",
            "CREATE INDEX idx_credentials_tenant_active ON credentials(tenant_id, active)",
            "CREATE INDEX idx_credentials_tenant_created_utc ON credentials(tenant_id, created_utc)",
            "CREATE INDEX idx_credentials_tenant_active_created_utc ON credentials(tenant_id, active, created_utc)",
            "CREATE INDEX idx_credentials_tenant_user_created_utc ON credentials(tenant_id, user_id, created_utc)",
            "CREATE INDEX idx_credentials_tenant_identifier ON credentials(tenant_id, identifier)",

            // Index (search index) indexes
            "CREATE INDEX idx_indexes_tenant ON indexes(tenant_id)",
            "CREATE INDEX idx_indexes_name ON indexes(tenant_id, name)",
            "CREATE INDEX idx_indexes_tenant_created_utc ON indexes(tenant_id, created_utc)",
            "CREATE INDEX idx_indexes_tenant_identifier ON indexes(tenant_id, identifier)",

            // Document indexes
            "CREATE INDEX idx_documents_tenant ON documents(tenant_id)",
            "CREATE INDEX idx_documents_index ON documents(index_id)",
            "CREATE INDEX idx_documents_tenant_index ON documents(tenant_id, index_id)",
            "CREATE INDEX idx_documents_name ON documents(index_id, name)",
            "CREATE INDEX idx_documents_content_sha256 ON documents(content_sha256)",

            // Term indexes (critical for search performance)
            "CREATE INDEX idx_terms_tenant ON terms(tenant_id)",
            "CREATE INDEX idx_terms_index ON terms(index_id)",
            "CREATE INDEX idx_terms_tenant_index ON terms(tenant_id, index_id)",
            "CREATE INDEX idx_terms_term ON terms(index_id, term)",
            "CREATE INDEX idx_terms_tenant_index_term ON terms(tenant_id, index_id, term)",
            "CREATE INDEX idx_terms_document_frequency ON terms(document_frequency DESC)",
            "CREATE INDEX idx_terms_orphan_cleanup ON terms(tenant_id, index_id, document_frequency)",

            // Document-term indexes (critical for inverted index lookups)
            "CREATE INDEX idx_document_terms_document ON document_terms(document_id)",
            "CREATE INDEX idx_document_terms_term ON document_terms(term_id)",
            "CREATE INDEX idx_document_terms_frequency ON document_terms(term_frequency DESC)",
            "CREATE INDEX idx_document_terms_term_doc ON document_terms(term_id, document_id)",

            // Label indexes (for filtering by labels)
            "CREATE INDEX idx_labels_document ON labels(document_id)",
            "CREATE INDEX idx_labels_index ON labels(index_id)",
            "CREATE INDEX idx_labels_label ON labels(label)",
            "CREATE INDEX idx_labels_document_label ON labels(document_id, label)",
            "CREATE INDEX idx_labels_index_label ON labels(index_id, label)",
            "CREATE INDEX idx_labels_tenant ON labels(tenant_id)",
            "CREATE INDEX idx_labels_tenant_label ON labels(tenant_id, label)",
            "CREATE INDEX idx_labels_user ON labels(user_id)",
            "CREATE INDEX idx_labels_user_label ON labels(user_id, label)",
            "CREATE INDEX idx_labels_credential ON labels(credential_id)",
            "CREATE INDEX idx_labels_credential_label ON labels(credential_id, label)",

            // Tag indexes (for filtering by key-value pairs)
            "CREATE INDEX idx_tags_document ON tags(document_id)",
            "CREATE INDEX idx_tags_index ON tags(index_id)",
            "CREATE INDEX idx_tags_key ON tags(`key`)",
            "CREATE INDEX idx_tags_document_key ON tags(document_id, `key`)",
            "CREATE INDEX idx_tags_index_key ON tags(index_id, `key`)",
            "CREATE INDEX idx_tags_key_value ON tags(`key`, value(255))",
            "CREATE INDEX idx_tags_tenant ON tags(tenant_id)",
            "CREATE INDEX idx_tags_tenant_key ON tags(tenant_id, `key`)",
            "CREATE INDEX idx_tags_user ON tags(user_id)",
            "CREATE INDEX idx_tags_user_key ON tags(user_id, `key`)",
            "CREATE INDEX idx_tags_credential ON tags(credential_id)",
            "CREATE INDEX idx_tags_credential_key ON tags(credential_id, `key`)"
        };

        /// <summary>
        /// Drops all tables in reverse order of creation.
        /// </summary>
        public static readonly string DropTables = @"
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS schema_metadata;
DROP TABLE IF EXISTS tags;
DROP TABLE IF EXISTS labels;
DROP TABLE IF EXISTS document_terms;
DROP TABLE IF EXISTS terms;
DROP TABLE IF EXISTS documents;
DROP TABLE IF EXISTS indexes;
DROP TABLE IF EXISTS credentials;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS administrators;
DROP TABLE IF EXISTS tenants;
SET FOREIGN_KEY_CHECKS = 1;
";

        /// <summary>
        /// Migration from schema version 3.1 to 3.2.
        /// Adds custom_metadata column to documents and indexes tables.
        /// </summary>
        public static readonly string MigrateFrom31To32 = @"
-- Add custom_metadata column to documents table
ALTER TABLE documents ADD COLUMN custom_metadata TEXT AFTER term_count;

-- Add custom_metadata column to indexes table
ALTER TABLE indexes ADD COLUMN custom_metadata TEXT AFTER description;

-- Update schema version
UPDATE schema_metadata SET value = '3.2' WHERE `key` = 'schema_version';
";

        /// <summary>
        /// Migration from schema version 3.2 to 3.3.
        /// Adds indexing_runtime_ms column to documents table.
        /// </summary>
        public static readonly string MigrateFrom32To33 = @"
-- Add indexing_runtime_ms column to documents table
ALTER TABLE documents ADD COLUMN indexing_runtime_ms DECIMAL(18,4) AFTER custom_metadata;

-- Update schema version
UPDATE schema_metadata SET value = '3.3' WHERE `key` = 'schema_version';
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
    document_length INT NOT NULL DEFAULT 0,
    term_count INT NOT NULL DEFAULT 0,
    custom_metadata TEXT,
    indexing_runtime_ms DECIMAL(18,4),
    indexed_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Terms table (vocabulary)
CREATE TABLE IF NOT EXISTS {prefix}_terms (
    id VARCHAR(48) PRIMARY KEY,
    term VARCHAR(512) NOT NULL UNIQUE,
    document_frequency INT NOT NULL DEFAULT 0,
    total_frequency BIGINT NOT NULL DEFAULT 0,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Document-term mappings (inverted index)
CREATE TABLE IF NOT EXISTS {prefix}_document_terms (
    id VARCHAR(48) PRIMARY KEY,
    document_id VARCHAR(48) NOT NULL,
    term_id VARCHAR(48) NOT NULL,
    term_frequency INT NOT NULL DEFAULT 0,
    character_positions TEXT,
    term_positions TEXT,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY unique_document_term (document_id, term_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Labels for documents or index level
CREATE TABLE IF NOT EXISTS {prefix}_labels (
    id VARCHAR(48) PRIMARY KEY,
    document_id VARCHAR(48),
    label VARCHAR(256) NOT NULL,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tags (key-value pairs) for documents or index level
CREATE TABLE IF NOT EXISTS {prefix}_tags (
    id VARCHAR(48) PRIMARY KEY,
    document_id VARCHAR(48),
    `key` VARCHAR(256) NOT NULL,
    value TEXT,
    last_update_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
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
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS {prefix}_tags;
DROP TABLE IF EXISTS {prefix}_labels;
DROP TABLE IF EXISTS {prefix}_document_terms;
DROP TABLE IF EXISTS {prefix}_terms;
DROP TABLE IF EXISTS {prefix}_documents;
SET FOREIGN_KEY_CHECKS = 1;
";
        }

        /// <summary>
        /// Generates SQL statements to create indexes for index-specific tables.
        /// Each statement should be executed individually with error handling for duplicate index errors.
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
                $"CREATE INDEX idx_{prefix}_docs_name ON {prefix}_documents(name)",
                $"CREATE INDEX idx_{prefix}_docs_sha256 ON {prefix}_documents(content_sha256)",
                $"CREATE INDEX idx_{prefix}_docs_created_utc ON {prefix}_documents(created_utc)",

                // Term indexes (critical for search performance)
                $"CREATE INDEX idx_{prefix}_terms_term ON {prefix}_terms(term)",
                $"CREATE INDEX idx_{prefix}_terms_docfreq ON {prefix}_terms(document_frequency DESC)",
                $"CREATE INDEX idx_{prefix}_terms_orphan ON {prefix}_terms(document_frequency)",

                // Document-term indexes (critical for inverted index lookups)
                $"CREATE INDEX idx_{prefix}_docterms_doc ON {prefix}_document_terms(document_id)",
                $"CREATE INDEX idx_{prefix}_docterms_term ON {prefix}_document_terms(term_id)",
                $"CREATE INDEX idx_{prefix}_docterms_freq ON {prefix}_document_terms(term_frequency DESC)",
                $"CREATE INDEX idx_{prefix}_docterms_term_doc ON {prefix}_document_terms(term_id, document_id)",
                $"CREATE INDEX idx_{prefix}_docterms_doc_term ON {prefix}_document_terms(document_id, term_id)",

                // Label indexes
                $"CREATE INDEX idx_{prefix}_labels_doc ON {prefix}_labels(document_id)",
                $"CREATE INDEX idx_{prefix}_labels_label ON {prefix}_labels(label)",
                $"CREATE INDEX idx_{prefix}_labels_doc_label ON {prefix}_labels(document_id, label)",

                // Tag indexes
                $"CREATE INDEX idx_{prefix}_tags_doc ON {prefix}_tags(document_id)",
                $"CREATE INDEX idx_{prefix}_tags_key ON {prefix}_tags(`key`)",
                $"CREATE INDEX idx_{prefix}_tags_doc_key ON {prefix}_tags(document_id, `key`)",
                $"CREATE INDEX idx_{prefix}_tags_key_value ON {prefix}_tags(`key`, value(255))"
            };
        }
    }
}
