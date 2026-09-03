<div align="center">
  <img src="https://raw.githubusercontent.com/jchristn/verbex/main/assets/logo.png" alt="Verbex Logo" width="320">

  # Verbex

  [![NuGet](https://img.shields.io/nuget/v/Verbex.svg)](https://nuget.org/packages/Verbex)
  ![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
  ![License](https://img.shields.io/badge/license-MIT-green.svg)

  **A high-performance inverted index library for full-text search.**

  **Verbex is in ALPHA** - we welcome your feedback, improvements, and bugfixes
</div>

## Screenshots

<div align="center">
  <img src="https://raw.githubusercontent.com/jchristn/verbex/main/assets/screenshot1.png" alt="Screenshot 1" width="800">
</div>

<div align="center">
  <img src="https://raw.githubusercontent.com/jchristn/verbex/main/assets/screenshot2.png" alt="Screenshot 2" width="800">
</div>

<div align="center">
  <img src="https://raw.githubusercontent.com/jchristn/verbex/main/assets/screenshot3.png" alt="Screenshot 3" width="800">
</div>

## Quick Start

### Docker (Recommended)

Get up and running in seconds with Docker:

```bash
# Clone and start
git clone https://github.com/jchristn/verbex.git
cd verbex/docker
docker compose up -d

# Server available at http://localhost:8080
# Dashboard available at http://localhost:8200
```

For detailed Docker configuration, see **[DOCKER.md](DOCKER.md)**.

### From Source

```bash
git clone https://github.com/jchristn/verbex.git
cd verbex
dotnet build
dotnet run --project src/Verbex.Server    # Start REST API
dotnet run --project src/TestConsole      # Interactive shell
```

### Library Usage

```csharp
using Verbex;
using System.Linq;

// Create index
var config = new VerbexConfiguration { StorageMode = StorageMode.InMemory };
using var index = new InvertedIndex(config);

// Add documents
await index.AddDocumentAsync(Guid.NewGuid(), "The quick brown fox", "doc1.txt");
await index.AddDocumentAsync(Guid.NewGuid(), "Machine learning algorithms", "doc2.txt");

// Search
var results = await index.SearchAsync("fox machine");
foreach (var result in results)
    Console.WriteLine($"{result.DocumentId}: {result.Score:F4}");

// Optional aggregate term stats for already-limited result sets
var stats = await index.GetDocumentTermStatsAsync(results.Results.Select(r => r.DocumentId));
```

## Key Features

- **Flexible Storage**: In-memory SQLite, persistent on-disk SQLite, or persistent external Postgres, SQL Server, or MySQL
- **TF-IDF Scoring**: Relevance-ranked search results
- **Text Processing**: Lemmatization, stop word removal, token filtering
- **Metadata Filtering**: Labels and tags for document organization
- **Filtered Enumeration**: Filter document listings by labels and tags
- **Wildcard Search**: Use `*` query to return all documents, optionally filtered by metadata
- **Opt-in Search Enrichment**: REST, SDK, MCP, CLI, and dashboard clients can request matched terms, per-term details, and whole-document term counts without changing default responses
- **Batch Operations**: Retrieve multiple documents in a single request
- **Backup & Restore**: Create portable backups and restore indices
- **Thread-Safe**: Optimized for concurrent read-heavy workloads
- **REST API**: Production-ready HTTP server with authentication
- **CLI Tool**: Professional command-line interface (`vbx`)
- **Web Dashboard**: React-based management UI

## Components

| Component | Description |
|-----------|-------------|
| **Verbex** | Core library (NuGet package) |
| **Verbex.Server** | REST API server |
| **VerbexCli** | Command-line interface |
| **TestConsole** | Interactive testing shell |
| **Dashboard** | React web interface |

## Storage Modes

```csharp
// In-Memory (fast, non-persistent)
var config = VerbexConfiguration.CreateInMemory();

// On-Disk (persistent)
var config = VerbexConfiguration.CreateOnDisk(@"C:\VerbexData");
```

## Database Backends

Verbex supports four database backends: **SQLite**, **PostgreSQL**, **MySQL**, and **SQL Server**.

### Choosing a Backend

| Use Case | Recommended Backend |
|----------|---------------------|
| Development & testing | SQLite (in-memory) |
| Single-server, low ingestion (<100 docs/min) | SQLite (file) |
| Production with concurrent users | PostgreSQL |
| High ingestion throughput (>1K docs/min) | PostgreSQL, MySQL, or SQL Server |
| Existing database infrastructure | Match your infrastructure |

### Why SQLite for Development

SQLite is the default and requires no external database server. It's ideal when:
- Running tests or developing locally
- Ingestion rate is low (documents arrive infrequently)
- Only a single application instance accesses the index
- You want zero-configuration setup

### Why Server-Based Databases for Production

PostgreSQL, MySQL, and SQL Server are preferred for production workloads because:

1. **Connection Pooling**: Server-based databases maintain connection pools (1-100 connections by default) allowing true parallel query execution. SQLite serializes all operations through a single connection.

2. **Write Concurrency**: Server-based databases use row-level locking, enabling multiple concurrent writes. SQLite uses a single-writer model where write operations queue behind each other.

3. **Horizontal Scaling**: Server-based databases support read replicas for distributing query load. SQLite is limited to a single server.

4. **High Ingestion Rates**: When documents arrive faster than a single writer can process, server-based databases handle the concurrent load without queuing delays.

### Configuration Examples

```csharp
// SQLite (development)
var settings = DatabaseSettings.CreateInMemory();

// SQLite (low-volume production)
var settings = DatabaseSettings.CreateSqliteFile("./verbex.db");

// PostgreSQL (recommended for production)
var settings = DatabaseSettings.CreatePostgresql(
    hostname: "localhost",
    databaseName: "verbex",
    username: "verbex_user",
    password: "secret"
);

// MySQL
var settings = DatabaseSettings.CreateMysql(
    hostname: "localhost",
    databaseName: "verbex",
    username: "verbex_user",
    password: "secret"
);

// SQL Server
var settings = DatabaseSettings.CreateSqlServer(
    hostname: "localhost",
    databaseName: "verbex",
    username: "verbex_user",
    password: "secret"
);
```

## Text Processing

```csharp
var config = new VerbexConfiguration
{
    StorageMode = StorageMode.OnDisk,
    StorageDirectory = @"C:\Data\Index",
    MinTokenLength = 3,
    MaxTokenLength = 20,
    Lemmatizer = new BasicLemmatizer(),
    StopWordRemover = new BasicStopWordRemover()
};
```

## CLI Example

```bash
vbx index create docs --storage disk --lemmatizer --stopwords
vbx doc add readme --content "Getting started with Verbex"
vbx search "getting started" --limit 10
vbx backup docs --output docs.vbx
vbx restore docs.vbx --name docs-restored
```

## REST API Example

```bash
# Authenticate
curl -X POST http://localhost:8080/v1.0/auth/login \
  -H "Content-Type: application/json" \
  -d '{"Username": "admin", "Password": "password"}'

# Search
curl -X POST http://localhost:8080/v1.0/indices/myindex/search \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{"Query": "machine learning"}'

# Batch retrieve documents
curl -X GET "http://localhost:8080/v1.0/indices/myindex/documents?ids=doc1,doc2,doc3" \
  -H "Authorization: Bearer YOUR_TOKEN"

# Backup an index
curl -X POST http://localhost:8080/v1.0/indices/myindex/backup \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -o myindex-backup.vbx

# Restore from backup
curl -X POST http://localhost:8080/v1.0/indices/restore \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@myindex-backup.vbx" \
  -F "name=restored-index"
```

## Documentation

- **[DOCKER.md](DOCKER.md)** - Docker deployment guide
- **[REST_API.md](REST_API.md)** - REST API reference (includes backup & restore)
- **[MCP_API.md](MCP_API.md)** - MCP server tools, transports, and AI client installation
- **[VBX_CLI.md](VBX_CLI.md)** - CLI documentation
- **[STORAGE.md](STORAGE.md)** - Storage architecture
- **[SCORING.md](SCORING.md)** - Scoring algorithm details
- **[TELEMETRY.md](TELEMETRY.md)** - Metrics, tracing, and observability integration

## Configuration

| Property | Default | Description |
|----------|---------|-------------|
| `StorageMode` | `InMemory` | `InMemory` or `OnDisk` |
| `StorageDirectory` | `null` | SQLite database location |
| `DefaultMaxSearchResults` | `100` | Search result limit |
| `MinTokenLength` | `0` | Minimum token length (0=disabled) |
| `MaxTokenLength` | `0` | Maximum token length (0=disabled) |
| `Lemmatizer` | `null` | Word lemmatization processor |
| `StopWordRemover` | `null` | Stop word filter |

## Support

- [File a Bug](https://github.com/jchristn/verbex/issues/new?template=bug_report.md)
- [Request a Feature](https://github.com/jchristn/verbex/issues/new?template=feature_request.md)
- [Discussions](https://github.com/jchristn/verbex/discussions)

## Contributing

```bash
git clone https://github.com/jchristn/verbex.git
cd verbex
dotnet build
dotnet run --project src/Test  # Run test suite
```

## License

[MIT License](LICENSE) - free for commercial and personal use.

## Attribution

Logo icon by [Freepik](https://www.flaticon.com/free-icon/index_2037149) from Flaticon
