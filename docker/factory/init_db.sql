PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA foreign_keys = ON;
PRAGMA cache_size = -64000;
PRAGMA temp_store = MEMORY;
PRAGMA mmap_size = 268435456;
PRAGMA busy_timeout = 30000;

-- Schema v3.3 tables

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

CREATE TABLE IF NOT EXISTS schema_metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Schema metadata
INSERT OR REPLACE INTO schema_metadata (key, value) VALUES ('schema_version', '3.3');
INSERT OR REPLACE INTO schema_metadata (key, value) VALUES ('created_utc', datetime('now'));

-- Database indexes

CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants(name);
CREATE INDEX IF NOT EXISTS idx_tenants_active ON tenants(active);

CREATE INDEX IF NOT EXISTS idx_administrators_email ON administrators(email);
CREATE INDEX IF NOT EXISTS idx_administrators_active ON administrators(active);

CREATE INDEX IF NOT EXISTS idx_users_tenant ON users(tenant_id);
CREATE INDEX IF NOT EXISTS idx_users_email ON users(tenant_id, email);
CREATE INDEX IF NOT EXISTS idx_users_active ON users(active);
CREATE INDEX IF NOT EXISTS idx_users_tenant_active ON users(tenant_id, active);

CREATE INDEX IF NOT EXISTS idx_credentials_tenant ON credentials(tenant_id);
CREATE INDEX IF NOT EXISTS idx_credentials_user ON credentials(user_id);
CREATE INDEX IF NOT EXISTS idx_credentials_bearer ON credentials(bearer_token);
CREATE INDEX IF NOT EXISTS idx_credentials_active ON credentials(active);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_active ON credentials(tenant_id, active);

CREATE INDEX IF NOT EXISTS idx_indexes_tenant ON indexes(tenant_id);
CREATE INDEX IF NOT EXISTS idx_indexes_name ON indexes(tenant_id, name);

CREATE INDEX IF NOT EXISTS idx_documents_tenant ON documents(tenant_id);
CREATE INDEX IF NOT EXISTS idx_documents_index ON documents(index_id);
CREATE INDEX IF NOT EXISTS idx_documents_tenant_index ON documents(tenant_id, index_id);
CREATE INDEX IF NOT EXISTS idx_documents_name ON documents(index_id, name);
CREATE INDEX IF NOT EXISTS idx_documents_content_sha256 ON documents(content_sha256);

CREATE INDEX IF NOT EXISTS idx_terms_tenant ON terms(tenant_id);
CREATE INDEX IF NOT EXISTS idx_terms_index ON terms(index_id);
CREATE INDEX IF NOT EXISTS idx_terms_tenant_index ON terms(tenant_id, index_id);
CREATE INDEX IF NOT EXISTS idx_terms_term ON terms(index_id, term);
CREATE INDEX IF NOT EXISTS idx_terms_tenant_index_term ON terms(tenant_id, index_id, term);
CREATE INDEX IF NOT EXISTS idx_terms_document_frequency ON terms(document_frequency DESC);
CREATE INDEX IF NOT EXISTS idx_terms_orphan_cleanup ON terms(tenant_id, index_id, document_frequency);

CREATE INDEX IF NOT EXISTS idx_document_terms_document ON document_terms(document_id);
CREATE INDEX IF NOT EXISTS idx_document_terms_term ON document_terms(term_id);
CREATE INDEX IF NOT EXISTS idx_document_terms_frequency ON document_terms(term_frequency DESC);
CREATE INDEX IF NOT EXISTS idx_document_terms_term_doc ON document_terms(term_id, document_id);

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

-- Default records

INSERT INTO tenants (identifier, name, description, active, created_utc, last_update_utc)
VALUES ('default', 'Default Tenant', 'Default tenant created during initial setup', 1, datetime('now'), datetime('now'));

INSERT INTO users (identifier, tenant_id, email, password_sha256, first_name, last_name, is_admin, active, created_utc, last_update_utc)
VALUES ('default', 'default', 'default@user.com', '5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8', 'Default', 'User', 1, 1, datetime('now'), datetime('now'));

INSERT INTO credentials (identifier, tenant_id, user_id, bearer_token, name, active, created_utc, last_update_utc)
VALUES ('default', 'default', 'default', 'default', 'Default API Key', 1, datetime('now'), datetime('now'));

INSERT INTO indexes (identifier, tenant_id, name, description, created_utc, last_update_utc)
VALUES ('default', 'default', 'Default Index', 'Default index created during initial setup', datetime('now'), datetime('now'));
