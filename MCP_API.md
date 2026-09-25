# Verbex MCP API

Verbex ships an MCP (Model Context Protocol) server, `verbex-mcp` (project `src/Verbex.Mcp`), that exposes
the Verbex inverted index as MCP tools. It gives an LLM full-text search and document management for RAG
(Retrieval Augmented Generation) workflows. The server is built on [Voltaic](https://nuget.org/packages/voltaic).

## Transports and Endpoints

`verbex-mcp` supports three transports, selected with `--transport`:

| Transport | Command | Endpoint | Use with |
| --- | --- | --- | --- |
| stdio (default) | `verbex-mcp` or `verbex-mcp --transport stdio` | stdin/stdout | Clients that launch the server process |
| HTTP | `verbex-mcp --transport http --host 127.0.0.1 --port 8200` | `http://<host>:<port>/mcp` (Streamable HTTP) | Claude Code, Cursor, Codex, Gemini, Mux, web apps |
| WebSocket | `verbex-mcp --transport websocket --host 127.0.0.1 --port 8200` | `ws://<host>:<port>/mcp` | Real-time apps |

The HTTP transport also exposes a legacy JSON-RPC endpoint at `/rpc` and an SSE endpoint at `/events`; the
recommended endpoint for modern MCP clients is the Streamable HTTP endpoint at `/mcp`.

```bash
dotnet run --project src/Verbex.Mcp -- --transport http --host 127.0.0.1 --port 8200
```

## Security Model

The Verbex MCP server exposes **no authentication** and is intended to run in a trusted local or private
environment. No API keys, headers, or bearer tokens are required or accepted.

## Installing into AI Clients

Two equivalent methods register Verbex with supported AI clients (Claude Code, Cursor, Codex, the Gemini CLI,
and Mux). Both write an entry named `verbex` pointing at `http://127.0.0.1:8200/mcp`, preserve every other
configured MCP server, and require the server to be running with `--transport http`.

### Built-in command

```bash
verbex-mcp --install                          # add 'verbex' to every detected client config
verbex-mcp --install --host 127.0.0.1 --port 8200   # advertise a specific host/port
verbex-mcp --uninstall                        # remove 'verbex' from every client config
```

`--install` creates a missing config file at the client's default path so a fresh machine still connects.
`--uninstall` leaves untouched any config that has no `verbex` entry.

### Standalone scripts

Per-agent, per-OS scripts live in [`scripts/`](scripts/README.md):

```bat
scripts\windows\install-cursor.bat
```

```sh
sh scripts/linux/install-cursor.sh
sh scripts/macos/remove-mux.sh
```

## Storage

Indices are persisted under:

- Windows: `%USERPROFILE%\.verbex-mcp\indices\`
- Linux/macOS: `~/.verbex-mcp/indices/`

Set `VERBEX_MCP_STORAGE_DIR` to use a different directory (for example, to isolate a test run).

Each index gets its own directory with an SQLite database (`index.db`). Indices referenced by a tool are
opened on demand, creating an on-disk index if one does not already exist. The default index name is
`default`. Because the name becomes a directory name, index names must be 1-128 characters from
`A-Z`, `a-z`, `0-9`, `-`, `_`, and `.`, and may not be `.` or `..`; any other name is rejected with an
`Invalid index name` error.

## Telemetry

Each tool invocation is instrumented and pushed over OTLP (metrics, traces, logs) via
[Radiant](https://nuget.org/packages/radiant). Controlled by environment variables:

| Variable | Default | Description |
| --- | --- | --- |
| `VERBEX_TELEMETRY_ENABLE` | `true` | Enable/disable the telemetry pipeline |
| `VERBEX_OTLP_ENDPOINT` | `http://localhost:4317` | OTLP collector endpoint |
| `VERBEX_OTLP_PROTOCOL` | `grpc` | `grpc` or `httpprotobuf` |

## Response Conventions

- Every tool returns a single JSON object serialized with camelCase property names.
- Validation, not-found, and other tool failures are returned as a tool result with `isError: true` whose
  text is `{ "error": "<message>" }`, rather than as a JSON-RPC protocol error, so the calling model can see
  the reason. Calling an unknown tool name is a JSON-RPC error.
- Every transport (stdio, HTTP, WebSocket) advertises the same tools through `tools/list`, and only the Verbex
  tools below. Tools are invoked only through `tools/call`; calling a tool name as a bare JSON-RPC method
  returns `-32601`. The protocol `ping` method returns an empty result (`{}`).

- Arguments are validated against each tool's input schema before the tool runs. A schema violation is a
  JSON-RPC `-32602` error, not an `isError` result. In particular, `tags` values must be JSON strings:
  `{ "priority": "5" }` is accepted, `{ "priority": 5 }` is rejected.
- The `index` parameter defaults to `default` on every tool that accepts it.

## Tool Inventory

| Tool | Purpose |
| --- | --- |
| `verbex_search` | Search indexed documents using TF-IDF relevance scoring |
| `verbex_add_document` | Add a document to an index with optional labels and tags |
| `verbex_get_document` | Retrieve one document with full metadata |
| `verbex_list_documents` | List documents in an index with pagination |
| `verbex_delete_document` | Remove a document from an index |
| `verbex_document_exists` | Check whether a document exists in an index |
| `verbex_statistics` | Get index-level statistics |
| `verbex_list_indices` | List all available indices |
| `verbex_create_index` | Create a new index with optional configuration |
| `verbex_delete_index` | Delete an index and all its documents |
| `verbex_index_exists` | Check whether an index exists |
| `verbex_add_labels` | Add labels to a document |
| `verbex_add_tags` | Add key-value tags to a document |

## Tool Reference

### `verbex_search`

Search indexed documents using TF-IDF relevance scoring. Returns matching documents ranked by relevance.
Use this for RAG retrieval.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `query` | string | Yes | n/a | Search query terms |
| `index` | string | No | `default` | Index name to search |
| `maxResults` | integer | No | `10` | Maximum results to return |
| `useAndLogic` | boolean | No | `false` | Require all terms (AND) instead of any term (OR) |
| `labels` | string[] | No | null | Filter to documents carrying all of these labels |
| `tags` | object (string→string) | No | null | Filter to documents carrying all of these key/value tags |
| `includeMatchedTerms` | boolean | No | `false` | Include the matched query terms for each result |
| `includeTermDetails` | boolean | No | `false` | Include per-term score and frequency for each result |
| `includeDocumentTermStats` | boolean | No | `false` | Include whole-document term statistics (one extra grouped query) |

#### Example Request

```json
{
  "index": "default",
  "query": "authentication token",
  "maxResults": 10,
  "useAndLogic": false,
  "labels": ["docs"],
  "tags": { "type": "documentation" },
  "includeMatchedTerms": true,
  "includeTermDetails": true,
  "includeDocumentTermStats": false
}
```

#### Response

```json
{
  "totalCount": 1,
  "searchTimeMs": 1.7,
  "results": [
    {
      "documentId": "doc_01J...",
      "documentName": "Auth Guide",
      "score": 4.21,
      "matchedTermCount": 2,
      "totalTermMatches": 5,
      "termScores": { "authentication": 2.6, "token": 1.61 },
      "matchedTerms": ["authentication", "token"],
      "termDetails": [
        { "term": "authentication", "score": 2.6, "frequency": 3 },
        { "term": "token", "score": 1.61, "frequency": 2 }
      ],
      "documentTermStats": { "uniqueTermCount": 84, "totalTermOccurrences": 210 }
    }
  ]
}
```

`matchedTerms` appears when `includeMatchedTerms` or `includeTermDetails` is true; `termDetails` appears when
`includeTermDetails` is true; `documentTermStats` appears when `includeDocumentTermStats` is true.

#### Errors

```json
{ "error": "Query is required" }
```

### `verbex_add_document`

Add a document to the search index with optional metadata. The document content is tokenized and indexed for
full-text search.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | n/a | Document name/title |
| `content` | string | Yes | n/a | Document content to index |
| `index` | string | No | `default` | Index name |
| `labels` | string[] | No | null | Labels for categorization |
| `tags` | object (string→string) | No | null | Key/value tags for metadata |

#### Example Request

```json
{
  "index": "default",
  "name": "Auth Guide",
  "content": "Authentication uses signed JWT tokens...",
  "labels": ["docs", "api"],
  "tags": { "version": "1.0", "author": "team" }
}
```

#### Response

```json
{ "success": true, "documentId": "doc_01J...", "indexName": "default" }
```

#### Errors

```json
{ "error": "Document name is required" }
```

```json
{ "error": "Document content is required" }
```

### `verbex_get_document`

Retrieve a specific document by ID with full content metadata.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `documentId` | string | Yes | n/a | Document ID to retrieve |
| `index` | string | No | `default` | Index name |

#### Response

```json
{
  "documentId": "doc_01J...",
  "documentPath": "Auth Guide",
  "documentLength": 1024,
  "indexedDate": "2026-09-03T12:00:00Z",
  "lastModified": "2026-09-03T12:00:00Z",
  "labels": ["docs", "api"],
  "tags": { "version": "1.0" },
  "customMetadata": {}
}
```

#### Errors

```json
{ "error": "Document ID is required" }
```

```json
{ "error": "Document not found" }
```

### `verbex_list_documents`

List all documents in an index with pagination.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `index` | string | No | `default` | Index name |
| `limit` | integer | No | `100` | Maximum documents to return |
| `offset` | integer | No | `0` | Offset for pagination |

#### Response

```json
{
  "count": 1,
  "documents": [
    {
      "documentId": "doc_01J...",
      "documentPath": "Auth Guide",
      "documentLength": 1024,
      "indexedDate": "2026-09-03T12:00:00Z"
    }
  ]
}
```

### `verbex_delete_document`

Remove a document from the index.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `documentId` | string | Yes | n/a | Document ID to delete |
| `index` | string | No | `default` | Index name |

#### Response

```json
{ "success": true }
```

`success` is `false` when no document with that ID existed.

#### Errors

```json
{ "error": "Document ID is required" }
```

### `verbex_document_exists`

Check whether a document exists in an index.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `documentId` | string | Yes | n/a | Document ID to check |
| `index` | string | No | `default` | Index name |

#### Response

```json
{ "exists": true, "documentId": "doc_01J...", "indexName": "default" }
```

#### Errors

```json
{ "error": "Document ID is required" }
```

### `verbex_statistics`

Get statistics about an index including document count, term count, and storage details.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `index` | string | No | `default` | Index name |

#### Response

```json
{
  "indexName": "default",
  "documentCount": 120,
  "termCount": 5400,
  "postingCount": 38210,
  "totalDocumentSize": 1048576,
  "averageDocumentLength": 758.3333,
  "totalTermOccurrences": 91000,
  "averageTermsPerDocument": 318.4167
}
```

`postingCount` is the number of term-document pairs. `averageDocumentLength` is term occurrences per
document (`totalTermOccurrences / documentCount`), and `averageTermsPerDocument` is unique terms per
document (`postingCount / documentCount`).

### `verbex_list_indices`

List all available indices. Includes indices open in memory and persistent indices discovered on disk.

#### Input

```json
{}
```

#### Response

```json
{ "indices": ["default", "codebase", "docs"] }
```

### `verbex_create_index`

Create a new search index with optional configuration.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | n/a | Index name |
| `inMemory` | boolean | No | `false` | Use in-memory storage instead of a persistent on-disk index |
| `enableLemmatizer` | boolean | No | `false` | Enable word lemmatization |
| `enableStopWords` | boolean | No | `false` | Enable stop word removal |
| `minTokenLength` | integer | No | `0` | Minimum token length (`0` = disabled) |
| `maxTokenLength` | integer | No | `0` | Maximum token length (`0` = disabled) |

#### Example Request

```json
{
  "name": "codebase",
  "inMemory": false,
  "enableLemmatizer": true,
  "enableStopWords": true,
  "minTokenLength": 2,
  "maxTokenLength": 50
}
```

#### Response

```json
{ "success": true, "indexName": "codebase", "inMemory": false }
```

#### Errors

```json
{ "error": "Index name is required" }
```

```json
{ "error": "Index already exists" }
```

### `verbex_delete_index`

Delete an index and all of its documents. Removes both the open index and its on-disk directory.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | n/a | Index name to delete |

#### Response

```json
{ "success": true }
```

#### Errors

```json
{ "error": "Index name is required" }
```

### `verbex_index_exists`

Check whether an index exists, in memory or as a persistent on-disk index.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | n/a | Index name to check |

#### Response

```json
{ "exists": true, "indexName": "codebase" }
```

#### Errors

```json
{ "error": "Index name is required" }
```

### `verbex_add_labels`

Add labels to a document for categorization.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `documentId` | string | Yes | n/a | Document ID |
| `labels` | string[] | Yes | n/a | Labels to add |
| `index` | string | No | `default` | Index name |

#### Response

```json
{ "success": true }
```

#### Errors

```json
{ "error": "Document ID is required" }
```

```json
{ "error": "Labels are required" }
```

### `verbex_add_tags`

Add key-value tags to a document for metadata.

#### Input

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `documentId` | string | Yes | n/a | Document ID |
| `tags` | object (string→string) | Yes | n/a | Tags to add |
| `index` | string | No | `default` | Index name |

#### Example Request

```json
{
  "index": "default",
  "documentId": "doc_01J...",
  "tags": { "status": "approved", "priority": "high" }
}
```

#### Response

```json
{ "success": true }
```

#### Errors

```json
{ "error": "Document ID is required" }
```

```json
{ "error": "Tags are required" }
```

## Example Workflows

### Code assistant RAG

```
User: "How do we handle authentication?"
LLM  -> verbex_search { "index": "codebase", "query": "authentication login token" }
LLM: "Based on AuthController.cs, authentication uses signed JWT tokens..."
```

### Knowledge base building

```
User: "Save this meeting summary."
LLM  -> verbex_add_document { "name": "Q4 Planning", "content": "...", "labels": ["meetings"] }
```
