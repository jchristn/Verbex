# VBX CLI - Verbex Command Line Interface

The Verbex CLI (`vbx`) is a professional command-line interface for the Verbex inverted index library. It provides enterprise-grade functionality for creating, managing, and searching document indices with advanced text processing capabilities.

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Command Reference](#command-reference)
  - [Index Management](#index-management)
  - [Document Operations](#document-operations)
  - [Search Operations](#search-operations)
  - [Statistics & Analytics](#statistics--analytics)
  - [Maintenance](#maintenance)
  - [Configuration](#configuration)
- [Global Options](#global-options)
- [Output Formats](#output-formats)
- [Configuration Files](#configuration-files)
- [Examples](#examples)
- [Performance Tuning](#performance-tuning)
- [Troubleshooting](#troubleshooting)

## Installation

### Pre-built Binaries

Download the latest release for your platform:

- **Windows x64**: `vbx-win-x64.exe`
- **Windows ARM64**: `vbx-win-arm64.exe`
- **Linux x64**: `vbx-linux-x64`
- **Linux ARM64**: `vbx-linux-arm64`
- **macOS x64**: `vbx-osx-x64`
- **macOS ARM64**: `vbx-osx-arm64`

### Build from Source

```bash
# Clone repository
git clone https://github.com/jchristn/verbex.git
cd verbex

# Build CLI
dotnet publish VerbexCli -c Release -r win-x64 --self-contained

# Binary location
./VerbexCli/bin/Release/net8.0/win-x64/publish/vbx.exe
```

## Quick Start

```bash
# Create your first index
vbx index create myindex --storage disk --lemmatizer --stopwords

# Add some documents
vbx doc add doc1 --content "This is my first document about artificial intelligence"
vbx doc add doc2 --content "Machine learning and AI are transforming technology"

# Add document with labels and tags
vbx doc add doc3 --content "Deep learning neural networks" --label tech --tag category=ml --tag author=Alice

# Search documents
vbx search "artificial intelligence"
vbx search "machine learning" --and

# Search with tag filter
vbx search "learning" --filter category=ml

# View statistics
vbx stats
```

## Command Reference

### Index Management

#### `vbx index create <name> [options]`

Creates a new inverted index with specified configuration.

**Arguments:**
- `<name>` - Name of the index to create

**Options:**
- `--storage <mode>`, `-s` - Storage mode: `memory` or `disk` (default: `memory`)
- `--lemmatizer`, `-l` - Enable lemmatization for better matching
- `--stopwords`, `-w` - Enable stop word removal
- `--min-length <n>` - Minimum token length (default: 0)
- `--max-length <n>` - Maximum token length (default: unlimited)
- `--tag <key=value>`, `-t` - Tags in key=value format (can be repeated)
- `--label <label>`, `-L` - Labels to associate with the index (can be repeated)

**Examples:**
```bash
# Memory-only index for fast operations
vbx index create fast --storage memory

# Production index with full text processing
vbx index create production --storage disk --lemmatizer --stopwords --min-length 2

# Index with labels and tags
vbx index create myindex --storage disk --label production --label search --tag environment=prod

# Large corpus index optimized for disk storage
vbx index create archive --storage disk --lemmatizer --min-length 3 --max-length 50
```

#### `vbx index ls`

Lists all available indices with their status and document counts.

**Output Columns:**
- `Name` - Index name
- `Storage` - Storage mode (memory/disk)
- `Documents` - Number of indexed documents
- `Status` - Current status (active/inactive)

#### `vbx index use <name>`

Switches the active index context for subsequent operations.

#### `vbx index delete <name> [--force]`

Deletes an index and all its data.

**Options:**
- `--force` - Skip confirmation prompt

#### `vbx index info [name]`

Shows detailed information about an index (current index if name not specified).

**Output includes:**
- Configuration settings
- Performance statistics
- Memory usage
- Creation and modification dates

#### `vbx index export <name> <file>`

Exports index metadata and statistics to a JSON file.

### Document Operations

#### `vbx doc add <name> [options]`

Adds a document to the current index.

**Arguments:**
- `<name>` - Document identifier

**Options:**
- `--content <text>`, `-c` - Document content as inline text (mutually exclusive with `--file`)
- `--file <path>`, `-f` - Load content from file (mutually exclusive with `--content`)
- `--meta <key>=<value>`, `-m`, `--tag <key>=<value>`, `-t` - Add tag key-value pair (can be repeated)
- `--label <label>`, `-L` - Add label to the document (can be repeated)
- `--index <name>`, `-i` - Target index (uses active index if not specified)

**Examples:**
```bash
# Add document with inline content
vbx doc add doc1 --content "Hello world, this is my document"

# Add document from file
vbx doc add doc2 --file ./document.txt

# Add document with labels and tags
vbx doc add doc3 --content "Machine learning basics" --label tech --tag category=ml --tag author=Alice

# Add document from file with metadata
vbx doc add doc4 --file ./report.txt --label important --meta department=engineering --meta year=2024

# Add to specific index
vbx doc add doc5 --content "Some content" --index production
```

**Supported file types:**
- Plain text (`.txt`)
- Markdown (`.md`)
- Any UTF-8 encoded text file

#### `vbx doc remove <name>`

Removes a specific document from the index.

**Options:**
- `--index <name>`, `-i` - Target index (uses active index if not specified)

#### `vbx doc ls`

Lists all documents in the current index.

**Options:**
- `--index <name>`, `-i` - Target index (uses active index if not specified)

**Output Columns:**
- `Name` - Document identifier
- `Size` - Content size in characters
- `Terms` - Number of unique terms
- `Added` - Date/time when document was indexed

#### `vbx doc clear [--force]`

Removes all documents from the index.

**Options:**
- `--force` - Skip confirmation prompt
- `--index <name>`, `-i` - Target index (uses active index if not specified)

### Search Operations

#### `vbx search "<query>" [options]`

Performs full-text search on the current index.

**Arguments:**
- `<query>` - Search query (multiple terms supported)

**Options:**
- `--and` - Use AND logic (all terms must match). Default is OR logic
- `--filter <key>=<value>`, `-f` - Metadata filter (can be repeated, filters combined with AND)
- `--limit <n>`, `-l` - Maximum number of results (default: 10)
- `--index <name>`, `-i` - Index to search (uses active index if not specified)

**Output Columns:**
- `Document` - Document name
- `Score` - Relevance score (0.0 - 1.0)
- `Matches` - Terms that matched in the document

**Examples:**
```bash
# OR search (any term matches)
vbx search "machine learning AI"

# AND search (all terms must match)
vbx search "machine learning" --and

# Limit results
vbx search "technology" --limit 5

# Search with metadata filter
vbx search "learning" --filter category=tech

# Multiple metadata filters (AND logic)
vbx search "neural networks" --filter category=tech --filter author=Alice

# Combined options
vbx search "deep learning" --and --filter year=2024 --limit 20
```

### Statistics & Analytics

#### `vbx stats <index> [options]`

Shows comprehensive statistics for an index.

**Options:**
- `--term <term>` - Show statistics for a specific term
- `--cache` - Show cache performance metrics

**General Statistics Include:**
- Document count and term count
- Average document length
- Memory usage breakdown
- Top frequent terms
- Cache performance metrics

**Term-Specific Statistics Include:**
- Document frequency (how many docs contain the term)
- Collection frequency (total occurrences)
- Average frequency per document
- Cache status

**Examples:**
```bash
# General index statistics
vbx stats production

# Specific term analysis
vbx stats production --term "machine"

# Cache performance metrics
vbx stats production --cache
```

### Maintenance

#### `vbx maint flush <index>`

Forces the write buffer to flush all pending changes to disk.

#### `vbx maint gc <index>`

Runs garbage collection to optimize memory usage and clean up deleted documents.

#### `vbx maint benchmark <index> [--documents <n>]`

Runs performance benchmarks to measure indexing and search speed.

**Options:**
- `--documents <n>` - Number of test documents to generate (default: 100)

**Output Metrics:**
- Indexing throughput (documents/second)
- Average search response time
- Memory usage during operations

#### `vbx maint stress <index> [--documents <n>]`

Runs stress testing with high document volumes to test stability.

**Options:**
- `--documents <n>` - Number of test documents (default: 1000)

**Output Metrics:**
- Total processing time
- Error count
- Peak memory usage
- Pass/fail status

### Configuration

#### `vbx config show`

Displays current CLI configuration settings.

#### `vbx config set <key> <value>`

Sets a configuration value.

**Available Keys:**
- `output` - Default output format (`table`, `json`, `csv`, `yaml`)
- `color` - Enable colored output (`true`, `false`)
- `verbose` - Enable verbose logging (`true`, `false`)

#### `vbx config unset <key>`

Resets a configuration value to its default.

**Examples:**
```bash
# Set JSON as default output
vbx config set output json

# Disable colors
vbx config set color false

# Enable verbose mode
vbx config set verbose true

# Reset to defaults
vbx config unset output
```

## Global Options

These options can be used with any command:

- `--output <format>` - Output format: `table` (default), `json`, `csv`, `yaml`
- `--no-color` - Disable colored output
- `--verbose` - Enable verbose output with detailed operations
- `--quiet` - Minimize output (errors only)
- `--debug` - Enable debug output with stack traces
- `--help` - Show help for any command

## Output Formats

### Table Format (Default)

Human-readable tabular output with aligned columns:

```
Name        Storage  Documents  Status
----------  -------  ---------  ------
production  disk     1250       active
archive     disk     50000      inactive
```

### JSON Format

Machine-readable JSON for programmatic processing:

```json
[
  {
    "Name": "production",
    "Storage": "disk",
    "Documents": 1250,
    "Status": "active"
  }
]
```

### CSV Format

Comma-separated values for data analysis:

```csv
Name,Storage,Documents,Status
production,disk,1250,active
archive,disk,50000,inactive
```

### YAML Format

YAML output for configuration-style data:

```yaml
- name: production
  storage: disk
  documents: 1250
  status: active
```

## Configuration Files

VBX stores configuration in `~/.vbx/` directory:

- `cli-config.json` - CLI settings and index registry
- `indices/` - Directory containing persistent index data

## Examples

### Basic Workflow

```bash
# Create a production index
vbx index create docs --storage disk --lemmatizer --stopwords

# Set as active index
vbx index use docs

# Add documentation files
vbx doc add readme --file ./README.md
vbx doc add api-guide --file ./docs/api.md --meta type=reference
vbx doc add tutorial --file ./docs/tutorial.md --meta type=guide

# Search for specific topics
vbx search "authentication API"
vbx search "getting started" --and

# Search only guides
vbx search "introduction" --filter type=guide

# Monitor performance
vbx stats
vbx maint benchmark --documents 500
```

### Batch Document Processing

```bash
# Create index for large corpus
vbx index create corpus --storage disk --lemmatizer --min-length 3

# Set as active
vbx index use corpus

# Process directory of files
for file in ./documents/*.txt; do
    filename=$(basename "$file" .txt)
    vbx doc add "$filename" --file "$file"
done

# Analyze results
vbx stats corpus
vbx search "important keywords" --limit 20 --output json
```

### Working with Labels and Tags

```bash
# Create index
vbx index create library --storage disk

# Add documents with labels and tags
vbx doc add paper1 --content "Neural network architectures for NLP" \
    --label research --tag category=nlp --tag author=Smith --tag year=2024

vbx doc add paper2 --content "Deep learning in computer vision" \
    --label research --tag category=vision --tag author=Jones --tag year=2023

vbx doc add blog1 --content "Introduction to machine learning" \
    --label blog --tag author=Smith --tag year=2024

# Search by content
vbx search "neural network"

# Search by content + tags
vbx search "learning" --filter category=nlp
vbx search "learning" --filter author=Smith
vbx search "learning" --filter author=Smith --filter year=2024
```

### Performance Analysis

```bash
# Create test index
vbx index create perf-test --storage memory

# Run comprehensive benchmarks
vbx index use perf-test
vbx maint benchmark --documents 1000
vbx maint stress --documents 5000

# Compare storage modes
vbx index create perf-disk --storage disk

# Test each configuration
for idx in perf-test perf-disk; do
    echo "Testing $idx..."
    vbx index use "$idx"
    vbx maint benchmark --documents 500
done
```

## Performance Tuning

### Storage Mode Selection

- **Memory**: Fastest performance (SQLite in-memory), data lost on exit, limited by RAM
- **Disk**: Persistent SQLite file storage, handles large datasets, slightly slower than in-memory

### Index Configuration

- **Lemmatizer**: Improves recall but increases index size
- **Stop Words**: Reduces index size and noise
- **Token Lengths**: Filter out very short/long tokens

### Optimal Settings by Use Case

**Real-time Search (Low Latency)**:
```bash
vbx index create realtime --storage memory --min-length 2
```

**Large Document Collection**:
```bash
vbx index create archive --storage disk --lemmatizer --stopwords --min-length 3
```

**High Precision Search**:
```bash
vbx index create precise --storage disk --lemmatizer --min-length 2 --max-length 50
```

## Troubleshooting

### Common Issues

**"Index does not exist" Error**:
- Check available indices: `vbx index ls`
- Verify index name spelling
- Ensure index was created successfully

**Out of Memory Errors**:
- Use `disk` storage mode
- Increase system RAM
- Process documents in smaller batches

**Slow Search Performance**:
- Check index statistics: `vbx stats`
- Run garbage collection: `vbx maint gc`
- Consider rebuilding with optimized settings

**File Permission Errors**:
- Ensure write access to `~/.vbx/` directory
- Check file permissions for document files
- Run with appropriate user privileges

### Debug Mode

Enable debug output for detailed error information:

```bash
vbx --debug command args
# or
export VBX_DEBUG=1
vbx command args
```

### Performance Diagnostics

```bash
# Check system resources
vbx stats myindex --cache

# Run diagnostics
vbx maint benchmark myindex --documents 100

# Memory usage analysis
vbx index info myindex
```

### Getting Help

```bash
# General help
vbx --help

# Command-specific help
vbx index --help
vbx search --help

# Version information
vbx --version
```

For additional support, consult the main Verbex documentation or submit issues to the project repository.
