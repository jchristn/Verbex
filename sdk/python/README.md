# Verbex Python SDK

A comprehensive Python SDK for interacting with the Verbex Inverted Index REST API.

All methods return domain objects directly rather than wrapped responses.

## Installation

```bash
pip install -r requirements.txt
```

## Usage

```python
from verbex_sdk import VerbexClient

# Create client
client = VerbexClient("http://localhost:8080", "verbexadmin")

# Health check - returns HealthData directly
health = client.health_check()
print(f"Server status: {health.status}")

# Create an index - returns IndexInfo directly
index = client.create_index(
    name="My Index",
    description="A test index",
    in_memory=True
)
print(f"Created index: {index.identifier}")

# Add documents - returns AddDocumentData directly
doc1 = client.add_document(index.identifier, "The quick brown fox jumps over the lazy dog.")
doc2 = client.add_document(index.identifier, "Machine learning is transforming industries.")

# Search - returns SearchData directly
results = client.search(index.identifier, "fox")
for result in results.results:
    print(f"Document: {result.document_id}, Score: {result.score}")

# Cleanup
client.delete_index(index.identifier)
client.close()
```

## Running the Test Harness

```bash
python test_harness.py <endpoint> <access_key>

# Example:
python test_harness.py http://localhost:8080 verbexadmin
```

## API Reference

### VerbexClient

#### Constructor
- `VerbexClient(endpoint: str, access_key: str)` - Create a new client

#### Health Endpoints
- `health_check()` - Returns `HealthData`
- `root_health_check()` - Returns `HealthData`

#### Authentication
- `login_with_credentials(tenant_id, email, password)` - Returns `LoginResult`
- `login_with_token(bearer_token)` - Returns `LoginResult`
- `validate_token()` - Returns `ValidationData`

#### Index Management
- `list_indices()` - Returns `List[IndexInfo]`
- `create_index(...)` - Returns `IndexInfo`
- `get_index(index_id)` - Returns `IndexInfo`
- `index_exists(index_id)` - Returns `bool`
- `delete_index(index_id)` - Returns `None`
- `update_index_labels(index_id, labels)` - Returns `None`
- `update_index_tags(index_id, tags)` - Returns `None`
- `update_index_custom_metadata(index_id, custom_metadata)` - Returns `IndexInfo`

#### Document Management
- `list_documents(index_id)` - Returns `List[DocumentInfo]`
- `add_document(index_id, content, document_id?, labels?, tags?, custom_metadata?)` - Returns `AddDocumentData`
- `get_document(index_id, document_id)` - Returns `DocumentInfo`
- `get_documents_batch(index_id, document_ids)` - Returns `BatchDocumentsResult`
- `document_exists(index_id, document_id)` - Returns `bool`
- `delete_document(index_id, document_id)` - Returns `None`
- `update_document_labels(index_id, document_id, labels)` - Returns `None`
- `update_document_tags(index_id, document_id, tags)` - Returns `None`
- `update_document_custom_metadata(index_id, document_id, custom_metadata)` - Returns `DocumentInfo`

#### Search
- `search(index_id, query, max_results?, labels?, tags?)` - Returns `SearchData`

#### Admin - Tenant Management
- `list_tenants()` - Returns `List[TenantInfo]`
- `get_tenant(tenant_id)` - Returns `TenantInfo`
- `create_tenant(name, description?)` - Returns `TenantInfo`
- `delete_tenant(tenant_id)` - Returns `None`

#### Admin - User Management
- `list_users(tenant_id)` - Returns `List[UserInfo]`
- `get_user(tenant_id, user_id)` - Returns `UserInfo`
- `create_user(tenant_id, email, password, ...)` - Returns `UserInfo`
- `delete_user(tenant_id, user_id)` - Returns `None`

#### Admin - Credential Management
- `list_credentials(tenant_id)` - Returns `List[CredentialInfo]`
- `get_credential(tenant_id, credential_id)` - Returns `CredentialInfo`
- `create_credential(tenant_id, description?)` - Returns `CredentialInfo`
- `delete_credential(tenant_id, credential_id)` - Returns `None`

## Model Classes

- `HealthData` - Health check response (status, version, timestamp)
- `ValidationData` - Token validation result
- `LoginResult` - Login attempt result
- `IndexInfo` - Index information with statistics
- `IndexStatistics` - Index statistics (document_count, term_count, etc.)
- `DocumentInfo` - Document information
- `AddDocumentData` - Add document response (document_id, message)
- `SearchData` - Search response with results
- `SearchResult` - Individual search result
- `BatchDocumentsResult` - Batch document retrieval result (documents, not_found, count, requested_count)
- `TenantInfo` - Tenant information
- `UserInfo` - User information
- `CredentialInfo` - Credential/API key information
- `VerbexError` - Exception raised for API errors
