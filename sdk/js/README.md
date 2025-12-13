# Verbex JavaScript SDK

A comprehensive JavaScript SDK for interacting with the Verbex Inverted Index REST API.

## Requirements

- Node.js 18.0.0 or higher (uses native fetch)

## Usage

```javascript
const { VerbexClient } = require('./verbex-sdk.js');

async function main() {
    // Create client
    const client = new VerbexClient('http://localhost:8080', 'verbexadmin');

    // Health check
    const health = await client.healthCheck();
    console.log(`Server status: ${health.data.status}`);

    // Create an index
    await client.createIndex({
        id: 'my-index',
        name: 'My Index',
        description: 'A test index',
        inMemory: true
    });

    // Add documents
    await client.addDocument('my-index', 'The quick brown fox jumps over the lazy dog.');
    await client.addDocument('my-index', 'Machine learning is transforming industries.');

    // Search
    const results = await client.searchDocuments('my-index', 'fox');
    for (const result of results.results) {
        console.log(`Document: ${result.documentId}, Score: ${result.score}`);
    }

    // Cleanup
    await client.deleteIndex('my-index');
}

main().catch(console.error);
```

## Running the Test Harness

```bash
node test-harness.js <endpoint> <access_key>

# Example:
node test-harness.js http://localhost:8080 verbexadmin
```

## API Reference

### VerbexClient

#### Constructor
- `new VerbexClient(endpoint, accessKey)` - Create a new client

#### Health Endpoints
- `healthCheck()` - Check server health via /v1.0/health
- `rootHealthCheck()` - Check server health via root endpoint

#### Authentication
- `login(username, password)` - Authenticate and get token
- `validateToken()` - Validate current token

#### Index Management
- `listIndices()` - List all indices
- `getIndices()` - Get all indices as IndexInfo objects
- `createIndex(options)` - Create a new index
- `getIndex(indexId)` - Get index details
- `getIndexInfo(indexId)` - Get index as IndexInfo object
- `deleteIndex(indexId)` - Delete an index

#### Document Management
- `listDocuments(indexId)` - List all documents
- `getDocuments(indexId)` - Get all documents as DocumentInfo objects
- `addDocument(indexId, content, documentId)` - Add document
- `getDocument(indexId, documentId)` - Get document details
- `deleteDocument(indexId, documentId)` - Delete document

#### Search
- `search(indexId, query, maxResults)` - Search documents
- `searchDocuments(indexId, query, maxResults)` - Search and return SearchResponse object

## Model Classes

- `ApiResponse` - Standard API response wrapper
- `IndexInfo` - Index information
- `DocumentInfo` - Document information
- `SearchResult` - Individual search result
- `SearchResponse` - Search response with results
- `VerbexError` - Error thrown for API errors
