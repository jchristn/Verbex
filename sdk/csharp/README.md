# Verbex C# SDK

A comprehensive .NET SDK for interacting with the Verbex Inverted Index REST API.

## Requirements

- .NET 8.0 SDK

## Building

```bash
cd sdk/csharp
dotnet build
```

## Usage

```csharp
using Verbex.Sdk;

// Create client
using var client = new VerbexClient("http://localhost:8080", "verbexadmin");

// Health check
var health = await client.HealthCheckAsync();
Console.WriteLine($"Server status: {health.Data?.Status}");

// Create an index
await client.CreateIndexAsync(
    id: "my-index",
    name: "My Index",
    description: "A test index",
    inMemory: true
);

// Add documents
await client.AddDocumentAsync("my-index", "The quick brown fox jumps over the lazy dog.");
await client.AddDocumentAsync("my-index", "Machine learning is transforming industries.");

// Search
var results = await client.SearchDocumentsAsync("my-index", "fox");
foreach (var result in results?.Results ?? new List<SearchResult>())
{
    Console.WriteLine($"Document: {result.DocumentId}, Score: {result.Score}");
}

// Cleanup
await client.DeleteIndexAsync("my-index");
```

## Running the Test Harness

```bash
cd sdk/csharp/Verbex.Sdk.TestHarness
dotnet run -- <endpoint> <access_key>

# Example:
dotnet run -- http://localhost:8080 verbexadmin
```

## API Reference

### VerbexClient

#### Constructor
- `VerbexClient(string endpoint, string accessKey)` - Create a new client

#### Health Endpoints
- `HealthCheckAsync(CancellationToken)` - Check server health via /v1.0/health
- `RootHealthCheckAsync(CancellationToken)` - Check server health via root endpoint

#### Authentication
- `LoginAsync(string username, string password, CancellationToken)` - Authenticate and get token
- `ValidateTokenAsync(CancellationToken)` - Validate current token

#### Index Management
- `ListIndicesAsync(CancellationToken)` - List all indices
- `GetIndicesAsync(CancellationToken)` - Get all indices as List<IndexInfo>
- `CreateIndexAsync(...)` - Create a new index
- `GetIndexAsync(string indexId, CancellationToken)` - Get index details
- `GetIndexInfoAsync(string indexId, CancellationToken)` - Get IndexInfo object
- `DeleteIndexAsync(string indexId, CancellationToken)` - Delete an index

#### Document Management
- `ListDocumentsAsync(string indexId, CancellationToken)` - List all documents
- `GetDocumentsAsync(string indexId, CancellationToken)` - Get List<DocumentInfo>
- `AddDocumentAsync(string indexId, string content, string? documentId, CancellationToken)` - Add document
- `GetDocumentAsync(string indexId, string documentId, CancellationToken)` - Get document details
- `DeleteDocumentAsync(string indexId, string documentId, CancellationToken)` - Delete document

#### Search
- `SearchAsync(string indexId, string query, int maxResults, CancellationToken)` - Search documents
- `SearchDocumentsAsync(...)` - Search and return SearchData object

## Model Classes

- `ApiResponse` / `ApiResponse<T>` - Standard API response wrapper
- `HealthData` - Health check response data
- `LoginData` - Login response with token
- `ValidationData` - Token validation result
- `IndexInfo` - Index information
- `IndexStatistics` - Index statistics
- `IndicesListData` - List of indices response
- `CreateIndexData` - Create index response
- `DeleteIndexData` - Delete index response
- `DocumentInfo` - Document information
- `DocumentsListData` - List of documents response
- `AddDocumentData` - Add document response
- `DeleteDocumentData` - Delete document response
- `SearchResult` - Individual search result
- `SearchData` - Search response with results
- `VerbexException` - Exception thrown for API errors
