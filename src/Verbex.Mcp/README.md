# Verbex MCP Server

MCP (Model Context Protocol) server for Verbex, providing RAG (Retrieval Augmented Generation) capabilities to LLM applications.

Built with [Voltaic](https://nuget.org/packages/voltaic) for MCP protocol support.

## Installation

```bash
dotnet build src/Verbex.Mcp
```

## Usage

### Stdio Transport (for Claude Code / Claude Desktop)

```bash
dotnet run --project src/Verbex.Mcp
# or
dotnet run --project src/Verbex.Mcp -- --transport stdio
```

### HTTP Transport (for web applications)

```bash
dotnet run --project src/Verbex.Mcp -- --transport http --host 127.0.0.1 --port 8200
```

### WebSocket Transport (for real-time applications)

```bash
dotnet run --project src/Verbex.Mcp -- --transport websocket --host 127.0.0.1 --port 8200
```

## Installing into AI Clients

Register (or remove) Verbex in the config files of Claude Code, Cursor, Codex, the Gemini CLI, and Mux with
the built-in command. Both operations target the HTTP endpoint, so start the server with
`--transport http` first, and preserve every other configured MCP server.

```bash
verbex-mcp --install                                # add 'verbex' to every detected client config
verbex-mcp --install --host 127.0.0.1 --port 8200   # advertise a specific host/port
verbex-mcp --uninstall                              # remove 'verbex' from every client config
```

Equivalent per-agent, per-OS scripts live in [`../../scripts/`](../../scripts/README.md). Full details,
including the endpoint each client uses, are in [`../../MCP_API.md`](../../MCP_API.md).

## Claude Code Integration (stdio)

To have Claude Code launch the server over stdio instead, add it directly:

```json
{
  "mcpServers": {
    "verbex": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/Code/Verbex/src/Verbex.Mcp", "--", "--transport", "stdio"]
    }
  }
}
```

Or for a published executable:

```json
{
  "mcpServers": {
    "verbex": {
      "command": "C:/Code/Verbex/src/Verbex.Mcp/bin/Debug/net8.0/verbex-mcp.exe",
      "args": ["--transport", "stdio"]
    }
  }
}
```

## Available Tools

### verbex_search
Search indexed documents using TF-IDF relevance scoring.

```json
{
  "index": "default",
  "query": "search terms",
  "maxResults": 10,
  "useAndLogic": false,
  "labels": ["category1"],
  "tags": {"type": "documentation"},
  "includeMatchedTerms": true,
  "includeTermDetails": true,
  "includeDocumentTermStats": false
}
```

The enrichment flags are optional and default to `false`. Matched terms and term details use existing search scoring data. Document term stats add one grouped aggregate query for the returned result documents.

### verbex_add_document
Add a document to the search index.

```json
{
  "index": "default",
  "name": "My Document",
  "content": "Document content to index...",
  "labels": ["docs", "api"],
  "tags": {"version": "1.0", "author": "team"}
}
```

### verbex_get_document
Retrieve a document by ID.

```json
{
  "index": "default",
  "documentId": "doc_abc123"
}
```

### verbex_list_documents
List documents in an index with pagination.

```json
{
  "index": "default",
  "limit": 100,
  "offset": 0
}
```

### verbex_delete_document
Remove a document from the index.

```json
{
  "index": "default",
  "documentId": "doc_abc123"
}
```

### verbex_statistics
Get index statistics.

```json
{
  "index": "default"
}
```

### verbex_list_indices
List all available indices.

```json
{}
```

### verbex_create_index
Create a new search index.

```json
{
  "name": "my-index",
  "inMemory": false,
  "enableLemmatizer": true,
  "enableStopWords": true,
  "minTokenLength": 2,
  "maxTokenLength": 50
}
```

### verbex_delete_index
Delete an index and all its documents.

```json
{
  "name": "my-index"
}
```

### verbex_add_labels
Add labels to a document.

```json
{
  "index": "default",
  "documentId": "doc_abc123",
  "labels": ["important", "reviewed"]
}
```

### verbex_add_tags
Add tags to a document.

```json
{
  "index": "default",
  "documentId": "doc_abc123",
  "tags": {"status": "approved", "priority": "high"}
}
```

### verbex_index_exists
Check whether an index exists (in memory or on disk).

```json
{
  "name": "my-index"
}
```

### verbex_document_exists
Check whether a document exists in an index.

```json
{
  "index": "default",
  "documentId": "doc_abc123"
}
```

See [`../../MCP_API.md`](../../MCP_API.md) for the full input/output schema of every tool.

## Storage

Indices are stored by default in:
- Windows: `%USERPROFILE%\.verbex-mcp\indices\`
- Linux/macOS: `~/.verbex-mcp/indices/`

Each index gets its own directory with an SQLite database.

## Use Cases

### Code Assistant RAG
Index your codebase and let the LLM search for relevant code:

```
User: "How do we handle authentication?"
LLM → verbex_search(query="authentication login token")
LLM: "Based on AuthController.cs, authentication uses JWT tokens..."
```

### Documentation Assistant
Index documentation and retrieve relevant sections:

```
User: "What's the rate limit policy?"
LLM → verbex_search(query="rate limit", labels=["api", "policies"])
LLM: "According to your docs, rate limits are 1000 req/min..."
```

### Knowledge Base Building
Build and maintain a searchable knowledge base:

```
User: "Save this meeting summary"
LLM → verbex_add_document(name="Q4 Planning", content="...", labels=["meetings"])
```

## License

MIT
