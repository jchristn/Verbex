namespace Verbex.Database.SqlServer.Queries
{
    /// <summary>
    /// SQL Server setup queries for creating and managing the database schema.
    /// </summary>
    internal static class SetupQueries
    {
        /// <summary>
        /// Schema version for migration tracking.
        /// </summary>
        public const string SchemaVersion = "3.0";

        /// <summary>
        /// Creates all tables for the multi-tenant inverted index.
        /// </summary>
        public static readonly string CreateTables = @"
-- Tenants table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tenants' AND xtype='U')
CREATE TABLE tenants (
    identifier NVARCHAR(48) PRIMARY KEY,
    name NVARCHAR(256) NOT NULL,
    description NVARCHAR(MAX),
    active BIT NOT NULL DEFAULT 1,
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Administrators table (global admins)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='administrators' AND xtype='U')
CREATE TABLE administrators (
    identifier NVARCHAR(48) PRIMARY KEY,
    email NVARCHAR(256) NOT NULL UNIQUE,
    password_sha256 NVARCHAR(128) NOT NULL,
    first_name NVARCHAR(128),
    last_name NVARCHAR(128),
    active BIT NOT NULL DEFAULT 1,
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Users table (tenant-scoped)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='users' AND xtype='U')
CREATE TABLE users (
    identifier NVARCHAR(48) PRIMARY KEY,
    tenant_id NVARCHAR(48) NOT NULL,
    email NVARCHAR(256) NOT NULL,
    password_sha256 NVARCHAR(128) NOT NULL,
    first_name NVARCHAR(128),
    last_name NVARCHAR(128),
    is_admin BIT NOT NULL DEFAULT 0,
    active BIT NOT NULL DEFAULT 1,
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT fk_users_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    CONSTRAINT unique_tenant_email UNIQUE (tenant_id, email)
);

-- Credentials table (bearer tokens)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='credentials' AND xtype='U')
CREATE TABLE credentials (
    identifier NVARCHAR(48) PRIMARY KEY,
    tenant_id NVARCHAR(48) NOT NULL,
    user_id NVARCHAR(48) NOT NULL,
    bearer_token NVARCHAR(64) NOT NULL UNIQUE,
    name NVARCHAR(256),
    active BIT NOT NULL DEFAULT 1,
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT fk_credentials_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE NO ACTION,
    CONSTRAINT fk_credentials_user FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE
);

-- Indexes table (tenant-scoped)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='indexes' AND xtype='U')
CREATE TABLE indexes (
    identifier NVARCHAR(48) PRIMARY KEY,
    tenant_id NVARCHAR(48) NOT NULL,
    name NVARCHAR(256) NOT NULL,
    description NVARCHAR(MAX),
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT fk_indexes_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    CONSTRAINT unique_tenant_name UNIQUE (tenant_id, name)
);

-- Documents table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='documents' AND xtype='U')
CREATE TABLE documents (
    id NVARCHAR(48) PRIMARY KEY,
    tenant_id NVARCHAR(48) NOT NULL,
    index_id NVARCHAR(48) NOT NULL,
    name NVARCHAR(512) NOT NULL,
    content_sha256 NVARCHAR(64),
    document_length INT NOT NULL DEFAULT 0,
    term_count INT NOT NULL DEFAULT 0,
    indexed_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT fk_documents_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier),
    CONSTRAINT fk_documents_index FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE,
    CONSTRAINT unique_index_name UNIQUE (index_id, name)
);

-- Terms table (vocabulary)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='terms' AND xtype='U')
CREATE TABLE terms (
    id NVARCHAR(48) PRIMARY KEY,
    tenant_id NVARCHAR(48) NOT NULL,
    index_id NVARCHAR(48) NOT NULL,
    term NVARCHAR(512) NOT NULL,
    document_frequency INT NOT NULL DEFAULT 0,
    total_frequency BIGINT NOT NULL DEFAULT 0,
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT fk_terms_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(identifier),
    CONSTRAINT fk_terms_index FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE,
    CONSTRAINT unique_index_term UNIQUE (index_id, term)
);

-- Document-Terms table (inverted index)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='document_terms' AND xtype='U')
CREATE TABLE document_terms (
    id NVARCHAR(48) PRIMARY KEY,
    document_id NVARCHAR(48) NOT NULL,
    term_id NVARCHAR(48) NOT NULL,
    term_frequency INT NOT NULL DEFAULT 0,
    character_positions NVARCHAR(MAX),
    term_positions NVARCHAR(MAX),
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT fk_document_terms_document FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    CONSTRAINT fk_document_terms_term FOREIGN KEY (term_id) REFERENCES terms(id),
    CONSTRAINT unique_document_term UNIQUE (document_id, term_id)
);

-- Labels table (document or index level)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='labels' AND xtype='U')
CREATE TABLE labels (
    id NVARCHAR(48) PRIMARY KEY,
    document_id NVARCHAR(48),
    index_id NVARCHAR(48) NOT NULL,
    label NVARCHAR(256) NOT NULL,
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT fk_labels_document FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    CONSTRAINT fk_labels_index FOREIGN KEY (index_id) REFERENCES indexes(identifier)
);

-- Tags table (document or index level key-value pairs)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tags' AND xtype='U')
CREATE TABLE tags (
    id NVARCHAR(48) PRIMARY KEY,
    document_id NVARCHAR(48),
    index_id NVARCHAR(48) NOT NULL,
    [key] NVARCHAR(256) NOT NULL,
    value NVARCHAR(MAX),
    last_update_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    created_utc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT fk_tags_document FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    CONSTRAINT fk_tags_index FOREIGN KEY (index_id) REFERENCES indexes(identifier)
);

-- Schema metadata table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='schema_metadata' AND xtype='U')
CREATE TABLE schema_metadata (
    [key] NVARCHAR(256) PRIMARY KEY,
    value NVARCHAR(MAX) NOT NULL
);
";

        /// <summary>
        /// Creates indexes for common queries. Run separately from table creation.
        /// Uses IF NOT EXISTS pattern for idempotent index creation.
        /// </summary>
        public static readonly string CreateIndexes = @"
-- Tenant indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tenants_name')
    CREATE INDEX idx_tenants_name ON tenants(name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tenants_active')
    CREATE INDEX idx_tenants_active ON tenants(active);

-- Administrator indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_administrators_email')
    CREATE INDEX idx_administrators_email ON administrators(email);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_administrators_active')
    CREATE INDEX idx_administrators_active ON administrators(active);

-- User indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_tenant')
    CREATE INDEX idx_users_tenant ON users(tenant_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_email')
    CREATE INDEX idx_users_email ON users(tenant_id, email);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_active')
    CREATE INDEX idx_users_active ON users(active);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_tenant_active')
    CREATE INDEX idx_users_tenant_active ON users(tenant_id, active);

-- Credential indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_tenant')
    CREATE INDEX idx_credentials_tenant ON credentials(tenant_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_user')
    CREATE INDEX idx_credentials_user ON credentials(user_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_bearer')
    CREATE INDEX idx_credentials_bearer ON credentials(bearer_token);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_active')
    CREATE INDEX idx_credentials_active ON credentials(active);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_tenant_active')
    CREATE INDEX idx_credentials_tenant_active ON credentials(tenant_id, active);

-- Index (search index) indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_indexes_tenant')
    CREATE INDEX idx_indexes_tenant ON indexes(tenant_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_indexes_name')
    CREATE INDEX idx_indexes_name ON indexes(tenant_id, name);

-- Document indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_documents_tenant')
    CREATE INDEX idx_documents_tenant ON documents(tenant_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_documents_index')
    CREATE INDEX idx_documents_index ON documents(index_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_documents_tenant_index')
    CREATE INDEX idx_documents_tenant_index ON documents(tenant_id, index_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_documents_name')
    CREATE INDEX idx_documents_name ON documents(index_id, name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_documents_content_sha256')
    CREATE INDEX idx_documents_content_sha256 ON documents(content_sha256);

-- Term indexes (critical for search performance)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_terms_tenant')
    CREATE INDEX idx_terms_tenant ON terms(tenant_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_terms_index')
    CREATE INDEX idx_terms_index ON terms(index_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_terms_tenant_index')
    CREATE INDEX idx_terms_tenant_index ON terms(tenant_id, index_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_terms_term')
    CREATE INDEX idx_terms_term ON terms(index_id, term);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_terms_document_frequency')
    CREATE INDEX idx_terms_document_frequency ON terms(document_frequency DESC);

-- Document-term indexes (critical for inverted index lookups)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_document_terms_document')
    CREATE INDEX idx_document_terms_document ON document_terms(document_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_document_terms_term')
    CREATE INDEX idx_document_terms_term ON document_terms(term_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_document_terms_frequency')
    CREATE INDEX idx_document_terms_frequency ON document_terms(term_frequency DESC);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_document_terms_term_doc')
    CREATE INDEX idx_document_terms_term_doc ON document_terms(term_id, document_id);

-- Label indexes (for filtering by labels)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_labels_document')
    CREATE INDEX idx_labels_document ON labels(document_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_labels_index')
    CREATE INDEX idx_labels_index ON labels(index_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_labels_label')
    CREATE INDEX idx_labels_label ON labels(label);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_labels_document_label')
    CREATE INDEX idx_labels_document_label ON labels(document_id, label);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_labels_index_label')
    CREATE INDEX idx_labels_index_label ON labels(index_id, label);

-- Tag indexes (for filtering by key-value pairs)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tags_document')
    CREATE INDEX idx_tags_document ON tags(document_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tags_index')
    CREATE INDEX idx_tags_index ON tags(index_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tags_key')
    CREATE INDEX idx_tags_key ON tags([key]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tags_document_key')
    CREATE INDEX idx_tags_document_key ON tags(document_id, [key]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tags_index_key')
    CREATE INDEX idx_tags_index_key ON tags(index_id, [key]);
";

        /// <summary>
        /// Drops all tables in reverse order of creation.
        /// </summary>
        public static readonly string DropTables = @"
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
";
    }
}
