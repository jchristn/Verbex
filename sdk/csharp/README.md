# Verbex C# SDK

A comprehensive .NET SDK for interacting with the Verbex Inverted Index REST API.

All methods return domain objects directly rather than wrapped responses.

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

// Health check - returns HealthData directly
HealthData health = await client.HealthCheckAsync();
Console.WriteLine($"Server status: {health.Status}");

// Create an index - returns IndexInfo directly
IndexInfo index = await client.CreateIndexAsync(
    name: "My Index",
    description: "A test index",
    inMemory: true
);
Console.WriteLine($"Created index: {index.Identifier}");

// Add documents - returns AddDocumentData directly
AddDocumentData doc1 = await client.AddDocumentAsync(index.Identifier, "The quick brown fox jumps over the lazy dog.");
AddDocumentData doc2 = await client.AddDocumentAsync(index.Identifier, "Machine learning is transforming industries.");

// Search - returns SearchData directly
SearchData results = await client.SearchAsync(index.Identifier, "fox");
foreach (SearchResultItem result in results.Results)
{
    Console.WriteLine($"Document: {result.DocumentId}, Score: {result.Score}");
}

// Cleanup
await client.DeleteIndexAsync(index.Identifier);
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
- `HealthCheckAsync(CancellationToken)` - Returns `HealthData`
- `RootHealthCheckAsync(CancellationToken)` - Returns `HealthData`

#### Authentication
- `LoginAsync(string tenantId, string email, string password, CancellationToken)` - Returns `LoginResult`
- `LoginAsync(string bearerToken, CancellationToken)` - Returns `LoginResult`
- `ValidateTokenAsync(CancellationToken)` - Returns `ValidationData`

#### Index Management
- `ListIndicesAsync(CancellationToken)` - Returns `List<IndexInfo>`
- `CreateIndexAsync(...)` - Returns `IndexInfo`
- `GetIndexAsync(string indexId, CancellationToken)` - Returns `IndexInfo`
- `IndexExistsAsync(string indexId, CancellationToken)` - Returns `bool`
- `DeleteIndexAsync(string indexId, CancellationToken)` - Returns `void`
- `UpdateIndexLabelsAsync(string indexId, List<string> labels, CancellationToken)` - Returns `void`
- `UpdateIndexTagsAsync(string indexId, Dictionary<string, string> tags, CancellationToken)` - Returns `void`
- `UpdateIndexCustomMetadataAsync(string indexId, object customMetadata, CancellationToken)` - Returns `IndexInfo`

#### Document Management
- `ListDocumentsAsync(string indexId, CancellationToken)` - Returns `List<DocumentInfo>`
- `AddDocumentAsync(string indexId, string content, ...)` - Returns `AddDocumentData`
- `GetDocumentAsync(string indexId, string documentId, CancellationToken)` - Returns `DocumentInfo`
- `GetDocumentsBatchAsync(string indexId, IEnumerable<string> documentIds, CancellationToken)` - Returns `BatchDocumentsResult`
- `DocumentExistsAsync(string indexId, string documentId, CancellationToken)` - Returns `bool`
- `DeleteDocumentAsync(string indexId, string documentId, CancellationToken)` - Returns `void`
- `UpdateDocumentLabelsAsync(...)` - Returns `void`
- `UpdateDocumentTagsAsync(...)` - Returns `void`
- `UpdateDocumentCustomMetadataAsync(...)` - Returns `DocumentInfo`

#### Search
- `SearchAsync(string indexId, string query, int maxResults, ...)` - Returns `SearchData`

#### Admin - Tenant Management
- `ListTenantsAsync(CancellationToken)` - Returns `List<TenantInfo>`
- `GetTenantAsync(string tenantId, CancellationToken)` - Returns `TenantInfo`
- `CreateTenantAsync(string name, string? description, CancellationToken)` - Returns `TenantInfo`
- `DeleteTenantAsync(string tenantId, CancellationToken)` - Returns `void`

#### Admin - User Management
- `ListUsersAsync(string tenantId, CancellationToken)` - Returns `List<UserInfo>`
- `GetUserAsync(string tenantId, string userId, CancellationToken)` - Returns `UserInfo`
- `CreateUserAsync(...)` - Returns `UserInfo`
- `DeleteUserAsync(string tenantId, string userId, CancellationToken)` - Returns `void`

#### Admin - Credential Management
- `ListCredentialsAsync(string tenantId, CancellationToken)` - Returns `List<CredentialInfo>`
- `GetCredentialAsync(string tenantId, string credentialId, CancellationToken)` - Returns `CredentialInfo`
- `CreateCredentialAsync(string tenantId, string? description, CancellationToken)` - Returns `CredentialInfo`
- `DeleteCredentialAsync(string tenantId, string credentialId, CancellationToken)` - Returns `void`

## Model Classes

- `HealthData` - Health check response (Status, Version, Timestamp)
- `ValidationData` - Token validation result
- `LoginResult` - Login attempt result
- `IndexInfo` - Index information with statistics
- `DocumentInfo` - Document information
- `AddDocumentData` - Add document response (DocumentId, Message)
- `SearchData` - Search response with results
- `SearchResultItem` - Individual search result
- `BatchDocumentsResult` - Batch document retrieval result (Documents, NotFound, Count, RequestedCount)
- `TenantInfo` - Tenant information
- `UserInfo` - User information
- `CredentialInfo` - Credential/API key information
- `VerbexException` - Exception thrown for API errors
