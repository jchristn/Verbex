# Verbex JavaScript SDK

A comprehensive JavaScript SDK for interacting with the Verbex Inverted Index REST API.

All methods return domain objects directly rather than wrapped responses.

## Requirements

- Node.js 18.0.0 or higher (uses native fetch)

## Usage

```javascript
const { VerbexClient } = require('./verbex-sdk.js');

async function main() {
    // Create client
    const client = new VerbexClient('http://localhost:8080', 'verbexadmin');

    // Health check - returns HealthData directly
    const health = await client.healthCheck();
    console.log(`Server status: ${health.status}`);

    // Create an index - returns IndexInfo directly
    const index = await client.createIndex({
        name: 'My Index',
        description: 'A test index',
        inMemory: true
    });
    console.log(`Created index: ${index.identifier}`);

    // Add documents - returns AddDocumentData directly
    const doc1 = await client.addDocument(index.identifier, 'The quick brown fox jumps over the lazy dog.');
    const doc2 = await client.addDocument(index.identifier, 'Machine learning is transforming industries.');

    // Search - returns SearchData directly
    const results = await client.search(index.identifier, 'fox');
    for (const result of results.results) {
        console.log(`Document: ${result.documentId}, Score: ${result.score}`);
    }

    // Cleanup
    await client.deleteIndex(index.identifier);
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
- `healthCheck()` - Returns `HealthData`
- `rootHealthCheck()` - Returns `HealthData`

#### Authentication
- `loginWithCredentials(tenantId, email, password)` - Returns `LoginResult`
- `loginWithToken(bearerToken)` - Returns `LoginResult`
- `validateToken()` - Returns `ValidationData`

#### Index Management
- `listIndices()` - Returns `IndexInfo[]`
- `createIndex(options)` - Returns `IndexInfo`
- `getIndex(indexId)` - Returns `IndexInfo`
- `indexExists(indexId)` - Returns `boolean`
- `deleteIndex(indexId)` - Returns `void`
- `updateIndexLabels(indexId, labels)` - Returns `void`
- `updateIndexTags(indexId, tags)` - Returns `void`
- `updateIndexCustomMetadata(indexId, customMetadata)` - Returns `IndexInfo`

#### Document Management
- `listDocuments(indexId)` - Returns `DocumentInfo[]`
- `addDocument(indexId, content, documentId?, labels?, tags?, customMetadata?)` - Returns `AddDocumentData`
- `getDocument(indexId, documentId)` - Returns `DocumentInfo`
- `getDocumentsBatch(indexId, documentIds)` - Returns `BatchDocumentsResult`
- `documentExists(indexId, documentId)` - Returns `boolean`
- `deleteDocument(indexId, documentId)` - Returns `void`
- `updateDocumentLabels(indexId, documentId, labels)` - Returns `void`
- `updateDocumentTags(indexId, documentId, tags)` - Returns `void`
- `updateDocumentCustomMetadata(indexId, documentId, customMetadata)` - Returns `DocumentInfo`

#### Search
- `search(indexId, query, maxResults?, labels?, tags?)` - Returns `SearchData`

#### Admin - Tenant Management
- `listTenants()` - Returns `TenantInfo[]`
- `getTenant(tenantId)` - Returns `TenantInfo`
- `createTenant(options)` - Returns `TenantInfo`
- `deleteTenant(tenantId)` - Returns `void`

#### Admin - User Management
- `listUsers(tenantId)` - Returns `UserInfo[]`
- `getUser(tenantId, userId)` - Returns `UserInfo`
- `createUser(tenantId, options)` - Returns `UserInfo`
- `deleteUser(tenantId, userId)` - Returns `void`

#### Admin - Credential Management
- `listCredentials(tenantId)` - Returns `CredentialInfo[]`
- `getCredential(tenantId, credentialId)` - Returns `CredentialInfo`
- `createCredential(tenantId, options?)` - Returns `CredentialInfo`
- `deleteCredential(tenantId, credentialId)` - Returns `void`

## Model Classes

- `HealthData` - Health check response (status, version, timestamp)
- `ValidationData` - Token validation result
- `LoginResult` - Login attempt result
- `IndexInfo` - Index information with statistics
- `DocumentInfo` - Document information
- `AddDocumentData` - Add document response (documentId, message)
- `SearchData` - Search response with results
- `SearchResult` - Individual search result
- `BatchDocumentsResult` - Batch document retrieval result (documents, notFound, count, requestedCount)
- `TenantInfo` - Tenant information
- `UserInfo` - User information
- `CredentialInfo` - Credential/API key information
- `VerbexError` - Error thrown for API errors
