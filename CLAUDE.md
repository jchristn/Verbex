# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Verbex is a C# .NET 8.0 solution containing five projects:
- **Verbex**: Main library project with a comprehensive `InvertedIndex` implementation using SQLite for storage
- **Test**: Comprehensive automated test suite covering all functionality
- **TestConsole**: Interactive shell application for manual testing and exploration
- **Verbex.Server**: REST API server for the InvertedIndex (separate web service)
- **VerbexCli**: Professional command-line interface (`vbx`) for index management and search

## Development Commands

### Building the Solution
```bash
dotnet build                                    # Build entire solution
dotnet build src/Verbex/Verbex.csproj          # Build main library
dotnet build src/Test/Test.csproj              # Build automated test suite
dotnet build src/TestConsole                   # Build interactive test console
dotnet build src/Verbex.Server                 # Build REST API server
dotnet build src/VerbexCli                     # Build CLI tool
```

### Running Applications
```bash
dotnet run --project src/Test             # Run comprehensive automated test suite
dotnet run --project src/TestConsole      # Run interactive shell for manual testing
dotnet run --project src/Verbex.Server    # Run REST API server
dotnet run --project src/VerbexCli        # Run CLI tool
```

### Cleaning Build Artifacts
```bash
dotnet clean                    # Clean all build outputs
```

### Restoring Dependencies
```bash
dotnet restore                  # Restore NuGet packages
```

## Project Structure

```
Verbex/
├── CLAUDE.md                 # Claude Code instructions
├── README.md                 # Project documentation
├── REST_API.md               # REST API documentation
├── VBX_CLI.md                # CLI documentation
├── SCORING.md                # Scoring algorithm documentation
├── STORAGE.md                # Storage architecture documentation
├── src/                      # Source code directory
│   ├── Verbex.sln            # Visual Studio solution file
│   ├── verbex.json           # Server configuration
│   ├── Verbex/               # Main library project
│   │   ├── Verbex.csproj     # Project file (class library)
│   │   ├── InvertedIndex.cs  # High-level inverted index API
│   │   ├── VerbexConfiguration.cs # Configuration settings
│   │   ├── DocumentMetadata.cs  # Document metadata with labels/tags
│   │   ├── MetadataFilter.cs # Search filter for labels/tags
│   │   ├── SearchResult.cs   # Search result structures
│   │   ├── SearchResults.cs  # Search results container
│   │   ├── IndexStatistics.cs # Index statistics
│   │   ├── StorageMode.cs    # InMemory/OnDisk enum
│   │   ├── ITokenizer.cs     # Tokenizer interface
│   │   ├── DefaultTokenizer.cs # Default tokenizer implementation
│   │   ├── ILemmatizer.cs    # Lemmatizer interface
│   │   ├── BasicLemmatizer.cs # Basic lemmatizer implementation
│   │   ├── IStopWordRemover.cs # Stop word remover interface
│   │   ├── BasicStopWordRemover.cs # Basic stop word remover
│   │   └── Repositories/     # SQLite-based storage layer
│   │       ├── IIndexRepository.cs       # Repository interface
│   │       ├── SqliteIndexRepository.cs  # SQLite implementation
│   │       ├── MemoryIndexRepository.cs  # In-memory SQLite wrapper
│   │       ├── DiskIndexRepository.cs    # File-based SQLite wrapper
│   │       └── Queries/                  # SQL query builders
│   ├── Test/                 # Comprehensive automated test suite
│   │   ├── Test.csproj       # Project file (console app)
│   │   ├── Program.cs        # Test runner
│   │   └── [test classes]    # Test coverage by category
│   ├── TestConsole/          # Interactive test console application
│   │   ├── TestConsole.csproj # Project file (console app)
│   │   ├── Program.cs        # Interactive shell entry point
│   │   ├── CommandProcessor.cs # Command handling
│   │   └── IndexManager.cs   # Index management
│   ├── Verbex.Server/        # REST API server
│   │   ├── Verbex.Server.csproj # Project file (web app)
│   │   ├── REST_API.md       # API cURL examples
│   │   └── Classes/          # Request/response models
│   └── VerbexCli/            # Command-line interface
│       ├── VerbexCli.csproj  # Project file (console app)
│       ├── BUILD.md          # Build instructions
│       └── Commands/         # CLI command implementations
├── sdk/                      # Client SDKs
│   ├── csharp/               # C# SDK (Verbex.Sdk)
│   ├── js/                   # JavaScript SDK
│   └── python/               # Python SDK
└── dashboard/                # React/Vite web dashboard
```

## Architecture Notes

### Storage Architecture
- **SQLite-based persistence**: Both in-memory and on-disk modes use SQLite
- **Two storage modes**: `InMemory` (SQLite in-memory) and `OnDisk` (SQLite file)
- **Repository pattern**: `IIndexRepository` interface with `SqliteIndexRepository` base implementation
- **Thread safety**: `ReaderWriterLockSlim` for read-heavy workload optimization

### Key Components
- `InvertedIndex`: High-level API for document indexing and search with metadata filtering
- `VerbexConfiguration`: Configuration for storage mode, tokenization, and text processing
- `DocumentMetadata`: Document metadata including labels (string list) and tags (key-value pairs)
- `MetadataFilter`: Filter for searching documents by labels and/or tags
- `IIndexRepository`: Interface for all storage operations
- `SqliteIndexRepository`: Handles all SQLite operations
- `MemoryIndexRepository`: Wraps SQLite in-memory mode
- `DiskIndexRepository`: Wraps SQLite file mode

### Database Schema (SQLite, Schema v2)
- `documents`: Document metadata (k-sortable ID, name, content_sha256, content length, term count, timestamps)
- `terms`: Unique terms with k-sortable ID, document frequency and total frequency
- `document_terms`: Document-term mappings with character_positions and term_positions (JSON arrays)
- `labels`: String labels for documents (k-sortable ID, document ID or NULL for index-level, label text)
- `tags`: Key-value pairs for documents (k-sortable ID, document ID or NULL for index-level, key, value)
- `index_metadata`: Index configuration with k-sortable ID, name, schema version, timestamps

### Framework
- Target framework: .NET 8.0
- All projects have nullable reference types enabled
- Implicit usings are enabled for all projects
- Uses Microsoft.Data.Sqlite for database operations

## TestConsole Interactive Commands

The TestConsole application provides a comprehensive interactive shell:

### Index Management
- `index create [options]` - Create new index
  - `--mode memory|disk` - Storage mode (default: memory)
  - `--name <name>` - Index name (default: default)
  - `--lemmatizer` - Enable lemmatization
  - `--stopwords` - Enable stop word removal
  - `--min-length <n>` - Minimum token length
  - `--max-length <n>` - Maximum token length
- `index use <name>` - Switch to existing index
- `index list` - List all available indices
- `index show` - Show current index configuration
- `index save <name>` - Save current index to disk
- `index reload <name>` - Reload index from disk
- `index discover` - Scan for existing persistent indices

### Document Operations
- `add <name> [options]` - Add document
  - `--content "<text>"` - Inline content (required if no --file)
  - `--file <path>` - Load from file (required if no --content)
- `remove <name>` - Remove document
- `list` - List all documents
- `clear --force` - Remove all documents

### Search Operations
- `search "<query>" [options]` - Search documents
  - `--and` - Use AND logic (default: OR)
  - `--limit <n>` - Maximum results

### Analysis
- `stats [term]` - Show index or term statistics
- `debug <term> [options]` - Debug term processing
  - `--lemmatizer` - Show lemmatization result
  - `--stopwords` - Check if stop word

### Maintenance
- `flush` - Force flush pending writes (on-disk mode only)

### Testing
- `demo` - Load sample demonstration data
- `benchmark` - Run performance benchmark
- `stress` - Run stress test with 1000+ documents
- `export <file>` - Export index data to JSON

## Development Environment

- Uses .NET 8.0 SDK
- The Test project references the main Verbex project
- Standard C# project structure with separate library and test projects
- All projects are located within the `src/` directory

## Coding Standards and Style Rules

### Code Organization and Structure
- Namespace declaration should always be at the top
- Using statements should be contained INSIDE the namespace block
- Microsoft and standard system library usings first, in alphabetical order
- Other using statements follow, in alphabetical order
- Limit each file to exactly one class or exactly one enum
- No nested classes or enums in a single file

### Documentation Requirements
- All public members, constructors, and public methods must have XML documentation
- No code documentation for private members or private methods
- Document default values, minimum/maximum values where appropriate
- Document which exceptions public methods can throw using `/// <exception>` tags
- Document thread safety guarantees in XML comments
- Document nullability in XML comments

### Variable and Member Naming
- Private class member variable names must start with underscore and be PascalCased: `_FooBar` not `_fooBar`
- Do not use `var` when defining variables - use actual type names
- All public members should have explicit getters/setters with backing variables when validation is required

### Async Programming
- Async calls should use `.ConfigureAwait(false)` where appropriate
- Every async method should accept a CancellationToken parameter unless the class has one as a member
- Check for cancellation at appropriate places in async methods
- When implementing methods returning IEnumerable, also create async variants with CancellationToken

### Exception Handling
- Use specific exception types rather than generic Exception
- Always include meaningful error messages with context
- Consider custom exception types for domain-specific errors
- Use exception filters when appropriate: `catch (SqlException ex) when (ex.Number == 2601)`

### Resource Management
- Implement IDisposable/IAsyncDisposable when holding unmanaged resources
- Use 'using' statements or declarations for IDisposable objects
- Follow full Dispose pattern with `protected virtual void Dispose(bool disposing)`
- Always call `base.Dispose()` in derived classes

### Null Safety and Validation
- Nullable reference types are enabled - use them properly
- Validate input parameters with guard clauses at method start
- Use `ArgumentNullException.ThrowIfNull()` for .NET 6+ or manual null checks
- Consider Result pattern or Option/Maybe types for methods that can fail
- Proactively eliminate null exception scenarios

### Threading and Concurrency
- Document thread safety guarantees
- Use Interlocked operations for simple atomic operations
- Prefer ReaderWriterLockSlim over lock for read-heavy scenarios

### LINQ and Collections
- Prefer LINQ methods over manual loops when readability is maintained
- Use `.Any()` instead of `.Count() > 0` for existence checks
- Be aware of multiple enumeration - consider `.ToList()` when needed
- Use `.FirstOrDefault()` with null checks rather than `.First()`

### Configuration and Flexibility
- Avoid hardcoded constants for values developers may want to configure
- Use public members with backing private members set to reasonable defaults
- Document what different configuration values mean and their effects

### Restrictions and Preferences
- Do not use tuples unless absolutely necessary
- Do not make assumptions about opaque class members/methods - ask for implementations
- When manual SQL strings are used, assume there are good architectural reasons
