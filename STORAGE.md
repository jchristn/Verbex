# Verbex Storage Architecture

This document describes how Verbex manages data storage. Verbex uses SQLite as the underlying storage engine for both in-memory and on-disk modes, providing robust data management and ACID compliance.

## Table of Contents

- [Storage Modes Overview](#storage-modes-overview)
- [SQLite-Based Architecture](#sqlite-based-architecture)
- [Database Schema](#database-schema)
- [Directory Structure](#directory-structure)
- [Default Storage Locations](#default-storage-locations)
- [Configuration Options](#configuration-options)
- [Backup and Restore](#backup-and-restore)
- [Troubleshooting](#troubleshooting)

## Storage Modes Overview

Verbex supports two storage modes that determine how data is persisted:

| Mode | Storage | Use Case |
|------|---------|----------|
| `InMemory` | SQLite in-memory database | Fast operations, temporary data, development/testing. Data is lost when application terminates. |
| `OnDisk` | SQLite file database | Persistent storage, production systems, large datasets. Data is stored in a `.db` file. |

Both modes use SQLite as the storage engine, ensuring consistent behavior and reliable data management.

## SQLite-Based Architecture

Verbex uses SQLite for all storage operations:

- **Repository Pattern**: `IIndexRepository` interface with `SqliteIndexRepository` base implementation
- **Memory Repository**: `MemoryIndexRepository` wraps SQLite in-memory mode
- **Disk Repository**: `DiskIndexRepository` wraps SQLite file mode
- **Thread Safety**: Uses `ReaderWriterLockSlim` for read-heavy workload optimization

## Database Schema

The SQLite database contains the following tables:

### Documents Table
```sql
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
```

| Column | Type | Description |
|--------|------|-------------|
| `id` | TEXT | K-sortable unique ID (PrettyId with `doc_` prefix) |
| `name` | TEXT | Document name (unique) |
| `content_sha256` | TEXT | SHA-256 hash of content |
| `document_length` | INTEGER | Length of original content in characters |
| `term_count` | INTEGER | Number of unique terms in document |
| `indexed_utc` | TEXT | ISO8601 timestamp when indexed |
| `last_modified_utc` | TEXT | ISO8601 timestamp of last modification |
| `created_utc` | TEXT | ISO8601 timestamp of creation |

### Terms Table
```sql
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
```

| Column | Type | Description |
|--------|------|-------------|
| `id` | TEXT | K-sortable unique ID (PrettyId with `term_` prefix) |
| `term` | TEXT | The term/token (unique, lowercase) |
| `document_frequency` | INTEGER | Number of documents containing this term |
| `total_frequency` | INTEGER | Total occurrences across all documents |
| `last_updated_utc` | TEXT | ISO8601 timestamp of last update |
| `created_utc` | TEXT | ISO8601 timestamp of creation |

### Document Terms Table
```sql
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
```

| Column | Type | Description |
|--------|------|-------------|
| `id` | TEXT | K-sortable unique ID (PrettyId with `docterm_` prefix) |
| `document_id` | TEXT | Reference to documents.id |
| `term_id` | TEXT | Reference to terms.id |
| `term_frequency` | INTEGER | Frequency of term in this document |
| `character_positions` | TEXT | JSON array of absolute byte offsets `[0,15,45,...]` |
| `term_positions` | TEXT | JSON array of word indices `[0,3,10,...]` |
| `last_modified_utc` | TEXT | ISO8601 timestamp of last modification |
| `created_utc` | TEXT | ISO8601 timestamp of creation |

### Labels Table
```sql
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
```

| Column | Type | Description |
|--------|------|-------------|
| `id` | TEXT | K-sortable unique ID (PrettyId with `label_` prefix) |
| `document_id` | TEXT | Reference to documents.id (NULL for index-level labels) |
| `label` | TEXT | Label string (case-insensitive matching) |
| `last_modified_utc` | TEXT | ISO8601 timestamp of last modification |
| `created_utc` | TEXT | ISO8601 timestamp of creation |

### Tags Table
```sql
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
```

| Column | Type | Description |
|--------|------|-------------|
| `id` | TEXT | K-sortable unique ID (PrettyId with `tag_` prefix) |
| `document_id` | TEXT | Reference to documents.id (NULL for index-level tags) |
| `key` | TEXT | Tag key |
| `value` | TEXT | Tag value (exact match) |
| `last_modified_utc` | TEXT | ISO8601 timestamp of last modification |
| `created_utc` | TEXT | ISO8601 timestamp of creation |

### Index Metadata Table
```sql
CREATE TABLE IF NOT EXISTS index_metadata (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    last_modified_utc TEXT NOT NULL,
    created_utc TEXT NOT NULL
);
```

| Column | Type | Description |
|--------|------|-------------|
| `id` | TEXT | K-sortable unique ID (PrettyId with `idx_` prefix) |
| `name` | TEXT | Index name |
| `last_modified_utc` | TEXT | ISO8601 timestamp of last modification |
| `created_utc` | TEXT | ISO8601 timestamp of creation |

### SQLite PRAGMA Settings

Verbex applies the following PRAGMA settings for optimal performance:

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = -64000;
PRAGMA temp_store = MEMORY;
PRAGMA mmap_size = 268435456;
PRAGMA busy_timeout = 5000;
PRAGMA foreign_keys = ON;
```

### Schema Notes

- All primary keys use k-sortable unique IDs generated by PrettyId with entity-type prefixes
- Position data is stored as two separate JSON arrays:
  - `character_positions`: Absolute byte offsets where the term appears in the document
  - `term_positions`: Word indices (0-based) indicating which word position the term occupies
- Labels support case-insensitive matching during search
- Tags support exact key-value matching during search
- Foreign key constraints with ON DELETE CASCADE ensure referential integrity

## Directory Structure

For `OnDisk` storage mode, Verbex stores the SQLite database in a configured directory:

```
{storage_directory}/
└── index.db                  # SQLite database file
```

For CLI (`vbx`) managed indices:

```
~/.vbx/
├── cli-config.json           # CLI settings and index registry
└── indices/
    ├── myindex/
    │   └── index.db          # SQLite database for 'myindex'
    └── production/
        └── index.db          # SQLite database for 'production'
```

## Default Storage Locations

### CLI Managed Indices

When using the `vbx` CLI tool, indices are stored in the user's home directory:

```
{user_profile}/.vbx/indices/{indexName}/
```

**Platform Examples**:
- **Windows**: `C:\Users\{username}\.vbx\indices\myindex\`
- **Linux**: `/home/{username}/.vbx/indices/myindex/`
- **macOS**: `/Users/{username}/.vbx/indices/myindex/`

### Programmatic Usage

Use `VerbexConfiguration.GetDefaultStorageDirectory(indexName)` or set `StorageDirectory` explicitly:

```csharp
// Using default location
var config = VerbexConfiguration.CreateOnDisk("myindex");

// Or specify custom location
var config = VerbexConfiguration.CreateOnDisk(@"C:\Data\Indices\myindex");
```

## Configuration Options

| Property | Default | Description |
|----------|---------|-------------|
| `StorageMode` | `InMemory` | Storage mode: `InMemory` or `OnDisk` |
| `StorageDirectory` | `null` | Directory for database file (required for OnDisk mode) |
| `DatabaseFilename` | `"index.db"` | SQLite database filename |
| `MinTokenLength` | `0` | Minimum token length (0 = disabled) |
| `MaxTokenLength` | `0` | Maximum token length (0 = disabled) |
| `DefaultMaxSearchResults` | `100` | Default maximum search results |
| `Lemmatizer` | `null` | Optional lemmatizer instance |
| `StopWordRemover` | `null` | Optional stop word remover instance |
| `Tokenizer` | `null` | Optional custom tokenizer instance |

### Example Configuration

```csharp
// In-memory index (data lost on dispose)
var memoryConfig = VerbexConfiguration.CreateInMemory();

// On-disk index (persistent)
var diskConfig = VerbexConfiguration.CreateOnDisk("myindex");

// Custom configuration
var config = new VerbexConfiguration
{
    StorageMode = StorageMode.OnDisk,
    StorageDirectory = @"C:\Data\MyIndex",
    DatabaseFilename = "search.db",
    MinTokenLength = 2,
    MaxTokenLength = 50,
    Lemmatizer = new BasicLemmatizer(),
    StopWordRemover = new BasicStopWordRemover()
};
```

## Disk Usage

### SQLite Database Size

The database size scales with:
- Number of unique terms
- Number of documents
- Number of term-document relationships
- Labels and tags metadata

### Monitoring Storage

Use the statistics API to monitor the index:

```csharp
var stats = await index.GetIndexStatisticsAsync();
Console.WriteLine($"Documents: {stats.DocumentCount}");
Console.WriteLine($"Terms: {stats.TermCount}");
```

Or via CLI:

```bash
vbx stats myindex
```

## Backup and Restore

### Backup Strategy

For `OnDisk` storage mode:

1. **Close the index** or ensure no active transactions
2. **Copy the database file**: Copy the `index.db` file (or your configured database filename)
3. **Optional**: Use SQLite's `.backup` command for a consistent copy

```bash
# Copy the database file
cp ~/.vbx/indices/myindex/index.db /backup/myindex.db

# Or use SQLite backup command
sqlite3 ~/.vbx/indices/myindex/index.db ".backup /backup/myindex.db"
```

### Restore Strategy

1. Stop any running index instances
2. Copy the backup database file to the storage directory
3. Load the index:
   ```csharp
   var config = VerbexConfiguration.CreateOnDisk(@"C:\Data\MyIndex");
   var index = new InvertedIndex(config);
   ```

## Troubleshooting

### Database Locked Error

**Symptom**: "database is locked" error when accessing the index.

**Cause**: Another process or thread has an exclusive lock on the database.

**Solution**:
- Ensure only one InvertedIndex instance accesses the database at a time
- Dispose of index instances properly before creating new ones
- Check for orphaned SQLite processes

### Permission Errors

**Symptom**: Cannot create or write to database file.

**Causes and Solutions**:
- Ensure application has write access to `StorageDirectory`
- On Windows, check folder permissions and antivirus exclusions
- On Linux/macOS, check file ownership and permissions

### Database Corruption

**Symptom**: SQLite errors during operations.

**Cause**: Incomplete write due to crash or disk issue.

**Solution**:
- Run SQLite integrity check: `sqlite3 index.db "PRAGMA integrity_check;"`
- Restore from backup if corruption is detected
- For minor issues, try `VACUUM` command to rebuild the database

### Storage Directory Not Found

**Symptom**: Index fails to load, directory not found error.

**Cause**: Storage directory was deleted or path is incorrect.

**Solution**:
- Verify the `StorageDirectory` path exists
- For `OnDisk` mode, the directory must exist or be created
- Use `VerbexConfiguration.CreateOnDisk()` which creates directories automatically

---

For additional help, see the main [README](README.md) or submit issues to the [project repository](https://github.com/jchristn/verbex/issues).
