# Verbex REST API Documentation

This document describes the REST API endpoints available in the Verbex inverted index server.

## Table of Contents

- [Authentication](#authentication)
- [Pagination](#pagination)
- [Data Structures](#data-structures)
- [API Endpoints Overview](#api-endpoints-overview)
- [Health and Status](#health-and-status)
- [Authentication APIs](#authentication-apis)
- [Index Management APIs](#index-management-apis)
- [Document Management APIs](#document-management-apis)
- [Search APIs](#search-apis)
- [Terms APIs](#terms-apis)
- [Admin - Tenant APIs](#admin---tenant-apis)
- [Admin - User APIs](#admin---user-apis)
- [Admin - Credential APIs](#admin---credential-apis)
- [Backup & Restore APIs](#backup--restore-apis)
- [Error Handling](#error-handling)

## Authentication

The Verbex REST API uses Bearer token authentication. Most endpoints require authentication except for health checks and login.

### Authentication Header
```
Authorization: Bearer <token>
```

### Getting an Authentication Token
Use the `/v1.0/auth/login` endpoint to obtain a token by providing valid credentials.

## Pagination

Collection endpoints (list indices, list documents, list tenants, list users, list credentials) support standardized pagination using `EnumerationQuery` parameters and return `EnumerationResult` responses.

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | integer | 100 | Maximum results per page (1-1000) |
| `skip` | integer | 0 | Number of records to skip |
| `continuationToken` | string | null | Opaque token from previous response for pagination |
| `ordering` | string | CreatedDescending | Sort order: `CreatedAscending` or `CreatedDescending` |

### EnumerationResult Response

```json
{
  "Success": true,
  "Timestamp": { "DateTime": "2024-01-15T10:30:00Z", "UnixTimestamp": 1705314600000 },
  "MaxResults": 100,
  "Skip": 0,
  "IterationsRequired": 1,
  "ContinuationToken": "c2tpcDoxMDA=",
  "EndOfResults": false,
  "TotalRecords": 250,
  "RecordsRemaining": 150,
  "Objects": [...]
}
```

| Field | Type | Description |
|-------|------|-------------|
| Success | boolean | Always true for successful requests |
| Timestamp | object | When the result was generated |
| MaxResults | integer | Echoed from request |
| Skip | integer | Echoed from request |
| IterationsRequired | integer | Always 1 for simple queries |
| ContinuationToken | string | Token for fetching next page (null when EndOfResults is true) |
| EndOfResults | boolean | True when this is the last page |
| TotalRecords | long | Total count of records before pagination |
| RecordsRemaining | long | Records remaining after this page |
| Objects | array | The paginated items |

### Pagination Example

```bash
# First page
curl -X GET "http://localhost:8080/v1.0/indices?maxResults=10" \
  -H "Authorization: Bearer <token>"

# Next page using continuation token
curl -X GET "http://localhost:8080/v1.0/indices?maxResults=10&continuationToken=c2tpcDoxMA==" \
  -H "Authorization: Bearer <token>"

# With ordering
curl -X GET "http://localhost:8080/v1.0/indices?maxResults=50&ordering=CreatedAscending" \
  -H "Authorization: Bearer <token>"
```

### Breaking Changes from Previous API

- The `limit=0` pattern (return all documents) is no longer supported
- `maxResults` must be between 1 and 1000
- Collection endpoints now return `EnumerationResult` wrapper instead of direct arrays
- Response structure has changed: items are in `Objects` array, not `Indices`, `Documents`, etc.

## Data Structures

### CreateIndexRequest
```json
{
  "Identifier": "string (optional, auto-generated if not provided)",
  "TenantId": "string (required for global admin, ignored for tenant users)",
  "Name": "string (required)",
  "Description": "string (optional)",
  "InMemory": "boolean (optional, default: false)",
  "EnableLemmatizer": "boolean (optional, default: false)",
  "EnableStopWordRemover": "boolean (optional, default: false)",
  "MinTokenLength": "integer (optional, default: 0)",
  "MaxTokenLength": "integer (optional, default: 0)",
  "Labels": ["string (optional)"],
  "Tags": {"key": "value (optional)"},
  "CustomMetadata": "any JSON value (optional)"
}
```

### IndexMetadata (Response)
```json
{
  "Identifier": "string",
  "TenantId": "string",
  "Name": "string",
  "Description": "string",
  "Enabled": "boolean",
  "InMemory": "boolean",
  "CreatedUtc": "datetime",
  "Labels": ["string"],
  "Tags": {"key": "value"},
  "CustomMetadata": "any JSON value"
}
```

### SearchRequest
```json
{
  "Query": "string (required)",
  "MaxResults": "integer (optional, default: 100)",
  "UseAndLogic": "boolean (optional, default: false)",
  "Labels": ["string (optional)"],
  "Tags": {"key": "value (optional)"},
  "IncludeMatchedTerms": "boolean (optional, default: false)",
  "IncludeTermDetails": "boolean (optional, default: false)",
  "IncludeDocumentTermStats": "boolean (optional, default: false)"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Query | string | Yes | Search query text |
| MaxResults | integer | No | Maximum number of results (default: 100) |
| UseAndLogic | boolean | No | If true, documents must contain ALL terms (AND). If false, documents can contain ANY term (OR). Default: false |
| Labels | string[] | No | Filter documents by labels (AND logic, case-insensitive) |
| Tags | object | No | Filter documents by tags (AND logic, exact match) |
| IncludeMatchedTerms | boolean | No | Include `MatchedTerms` on each result. Default: false |
| IncludeTermDetails | boolean | No | Include `TermDetails` on each result and also include `MatchedTerms`. Default: false |
| IncludeDocumentTermStats | boolean | No | Include whole-document `DocumentTermStats` with one grouped database query. Default: false |

### AddDocumentRequest
```json
{
  "Id": "string (optional, auto-generated if not provided)",
  "Content": "string (required)",
  "Labels": ["string (optional)"],
  "Tags": {"key": "value (optional)"},
  "CustomMetadata": "any JSON value (optional)"
}
```

### DocumentMetadata (Response)
```json
{
  "DocumentId": "string",
  "DocumentPath": "string",
  "OriginalFileName": "string (nullable)",
  "DocumentLength": "integer",
  "IndexedDate": "datetime",
  "LastModified": "datetime",
  "ContentSha256": "string",
  "IndexingRuntimeMs": "decimal (nullable)",
  "Terms": ["string"],
  "IsDeleted": "boolean",
  "Labels": ["string"],
  "Tags": {"key": "value"},
  "CustomMetadata": "any JSON value (nullable)"
}
```

### SearchResult
```json
{
  "DocumentId": "string",
  "Document": "DocumentMetadata (includes Labels and Tags)",
  "Score": "number",
  "MatchedTermCount": "integer",
  "TermScores": {"term": "score"},
  "TermFrequencies": {"term": "count"},
  "TotalTermMatches": "integer",
  "MatchedTerms": ["string (only when requested)"],
  "TermDetails": [
    {
      "Term": "string",
      "Score": "number",
      "Frequency": "integer"
    }
  ],
  "DocumentTermStats": {
    "UniqueTermCount": "integer",
    "TotalTermOccurrences": "integer"
  }
}
```

### UpdateLabelsRequest
```json
{
  "Labels": ["string"]
}
```

### UpdateTagsRequest
```json
{
  "Tags": {"key": "value"}
}
```

### UpdateCustomMetadataRequest
```json
{
  "CustomMetadata": "any JSON value"
}
```

### ResponseWrapper
All API responses are wrapped in a standard format:
```json
{
  "Guid": "string",
  "Success": "boolean",
  "TimestampUtc": "datetime",
  "StatusCode": "integer",
  "ErrorMessage": "string (optional)",
  "Data": "object",
  "Headers": {"key": "value"},
  "TotalCount": "integer (optional, for pagination)",
  "Skip": "integer (optional, for pagination)",
  "ProcessingTimeMs": "number"
}
```

## API Endpoints Overview

| Category | Method | Endpoint | Description | Auth Required |
|----------|--------|----------|-------------|---------------|
| Health | GET | `/` | Health check | No |
| Health | HEAD | `/` | Health check (no body) | No |
| Health | GET | `/v1.0/health` | Detailed health status | No |
| Auth | POST | `/v1.0/auth/login` | Login and get token | No |
| Auth | GET | `/v1.0/auth/validate` | Validate token | No |
| Index | GET | `/v1.0/indices` | List all indices | Yes |
| Index | POST | `/v1.0/indices` | Create new index | Yes |
| Index | GET | `/v1.0/indices/{id}` | Get index statistics | Yes |
| Index | HEAD | `/v1.0/indices/{id}` | Check if index exists | Yes |
| Index | DELETE | `/v1.0/indices/{id}` | Delete index | Yes |
| Index | PUT | `/v1.0/indices/{id}/labels` | Update index labels | Yes |
| Index | PUT | `/v1.0/indices/{id}/tags` | Update index tags | Yes |
| Index | PUT | `/v1.0/indices/{id}/customMetadata` | Update index custom metadata | Yes |
| Document | GET | `/v1.0/indices/{id}/documents` | List documents (max 1000) or batch retrieve by IDs | Yes |
| Document | DELETE | `/v1.0/indices/{id}/documents` | Batch delete documents by IDs | Yes |
| Document | POST | `/v1.0/indices/{id}/documents` | Add document | Yes |
| Document | GET | `/v1.0/indices/{id}/documents/{docId}` | Get document with metadata | Yes |
| Document | HEAD | `/v1.0/indices/{id}/documents/{docId}` | Check if document exists | Yes |
| Document | DELETE | `/v1.0/indices/{id}/documents/{docId}` | Delete document | Yes |
| Document | PUT | `/v1.0/indices/{id}/documents/{docId}/labels` | Update document labels | Yes |
| Document | PUT | `/v1.0/indices/{id}/documents/{docId}/tags` | Update document tags | Yes |
| Document | PUT | `/v1.0/indices/{id}/documents/{docId}/customMetadata` | Update document custom metadata | Yes |
| Search | POST | `/v1.0/indices/{id}/search` | Search documents | Yes |
| Terms | GET | `/v1.0/indices/{id}/terms/top` | Get top terms by frequency | Yes |
| Tenant | GET | `/v1.0/tenants` | List tenants | Yes (Admin) |
| Tenant | POST | `/v1.0/tenants` | Create tenant | Yes (Global Admin) |
| Tenant | GET | `/v1.0/tenants/{id}` | Get tenant with statistics | Yes (Admin) |
| Tenant | PUT | `/v1.0/tenants/{id}` | Update tenant | Yes (Global Admin) |
| Tenant | DELETE | `/v1.0/tenants/{id}` | Delete tenant | Yes (Global Admin) |
| Tenant | PUT | `/v1.0/tenants/{id}/labels` | Update tenant labels | Yes (Admin) |
| Tenant | PUT | `/v1.0/tenants/{id}/tags` | Update tenant tags | Yes (Admin) |
| User | GET | `/v1.0/tenants/{id}/users` | List users | Yes (Admin) |
| User | POST | `/v1.0/tenants/{id}/users` | Create user | Yes (Admin) |
| User | GET | `/v1.0/tenants/{id}/users/{userId}` | Get user | Yes (Admin) |
| User | PUT | `/v1.0/tenants/{id}/users/{userId}` | Update user | Yes (Admin) |
| User | DELETE | `/v1.0/tenants/{id}/users/{userId}` | Delete user | Yes (Admin) |
| User | PUT | `/v1.0/tenants/{id}/users/{userId}/labels` | Update user labels | Yes (Admin) |
| User | PUT | `/v1.0/tenants/{id}/users/{userId}/tags` | Update user tags | Yes (Admin) |
| Credential | GET | `/v1.0/tenants/{id}/credentials` | List credentials | Yes (Admin) |
| Credential | POST | `/v1.0/tenants/{id}/credentials` | Create credential | Yes (Admin) |
| Credential | PUT | `/v1.0/tenants/{id}/credentials/{credId}` | Update credential | Yes (Admin) |
| Credential | DELETE | `/v1.0/tenants/{id}/credentials/{credId}` | Delete credential | Yes (Admin) |
| Credential | PUT | `/v1.0/tenants/{id}/credentials/{credId}/labels` | Update credential labels | Yes (Admin) |
| Credential | PUT | `/v1.0/tenants/{id}/credentials/{credId}/tags` | Update credential tags | Yes (Admin) |
| Backup | POST | `/v1.0/indices/{id}/backup` | Create index backup | Yes |
| Restore | POST | `/v1.0/indices/restore` | Restore backup to new index | Yes |
| Restore | POST | `/v1.0/indices/{id}/restore` | Restore backup to existing index | Yes |

## Health and Status

### GET `/`
**Description:** Basic health check endpoint

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Status": "Healthy",
    "Version": "1.0.0",
    "Timestamp": "2025-01-01T12:00:00Z"
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 1.23
}
```

### HEAD `/`
**Description:** Health check endpoint (returns 200 with no body)

### GET `/v1.0/health`
**Description:** Detailed health status

**Response:** Same as GET `/`

## Authentication APIs

### POST `/v1.0/auth/login`
**Description:** Authenticate user and receive access token

**Request Body:**
```json
{
  "TenantId": "string (optional - for tenant-scoped authentication)",
  "Username": "admin",
  "Password": "password"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| TenantId | string | No | Tenant identifier for tenant-scoped authentication. If omitted, authenticates as global admin. |
| Username | string | Yes | User's email or username |
| Password | string | Yes | User's password |

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Token": "base64-encoded-token-here",
    "Email": "admin@example.com",
    "FirstName": "string (for tenant users)",
    "LastName": "string (for tenant users)",
    "TenantId": "tenant-id-if-applicable",
    "IsAdmin": true,
    "IsGlobalAdmin": true
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 5.67
}
```

### GET `/v1.0/auth/validate`
**Description:** Validate authentication token

**Headers:**
```
Authorization: Bearer <token>
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Valid": true,
    "IsGlobalAdmin": false,
    "IsTenantAdmin": true,
    "TenantId": "tenant-123",
    "UserId": "user-456",
    "CredentialId": "cred-789",
    "Email": "user@example.com"
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 1.23
}
```

## Index Management APIs

### GET `/v1.0/indices`
**Description:** Retrieve list of all indices for the authenticated tenant

**Headers:**
```
Authorization: Bearer <token>
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Indices": [
      {
        "Identifier": "idx_01JFXA1234567890ABCDEF",
        "TenantId": "default",
        "Name": "Sample Index",
        "Description": "A sample inverted index",
        "Enabled": true,
        "InMemory": false,
        "CreatedUtc": "2025-01-01T12:00:00Z",
        "Labels": [],
        "Tags": {}
      }
    ],
    "Count": 1
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 2.34
}
```

### POST `/v1.0/indices`
**Description:** Create a new index

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "TenantId": "tenant-id (required for global admin)",
  "Name": "My Index",
  "Description": "My custom index for documents",
  "InMemory": false,
  "EnableLemmatizer": true,
  "EnableStopWordRemover": true,
  "MinTokenLength": 2,
  "MaxTokenLength": 50,
  "Labels": ["production", "search"],
  "Tags": {"environment": "prod", "team": "engineering"},
  "CustomMetadata": {"any": "json value"}
}
```

Note: The `Identifier` is optional. If not provided, a unique k-sortable ID will be auto-generated by the server. If a custom identifier is provided and an index with that identifier already exists, a 409 Conflict error is returned. For tenant users, the index is associated with the tenant from your authentication context. Global admins must specify `TenantId`.

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 201,
  "ErrorMessage": null,
  "Data": {
    "Message": "Index created successfully",
    "Index": {
      "Identifier": "idx_01JFXA1234567890ABCDEF",
      "TenantId": "default",
      "Name": "My Index",
      "Description": "My custom index for documents",
      "InMemory": false,
      "CreatedUtc": "2025-01-01T12:00:00Z",
      "Labels": ["production", "search"],
      "Tags": {"environment": "prod", "team": "engineering"},
      "CustomMetadata": {"any": "json value"}
    }
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 10.12
}
```

### GET `/v1.0/indices/{id}`
**Description:** Get statistics for a specific index

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "DocumentCount": 150,
    "TermCount": 5000,
    "PostingCount": 12500,
    "AverageDocumentLength": 250.5,
    "TotalDocumentSize": 37575,
    "TotalTermOccurrences": 50000,
    "AverageTermsPerDocument": 83.3,
    "AverageDocumentFrequency": 2.5,
    "MaxDocumentFrequency": 150,
    "MinDocumentLength": 50,
    "MaxDocumentLength": 500,
    "GeneratedAt": "2025-01-01T12:00:00Z"
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 3.45
}
```

### HEAD `/v1.0/indices/{id}`
**Description:** Check if an index exists

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier

**Response:**
- Returns `200 OK` with no body if the index exists
- Returns `404 Not Found` with no body if the index does not exist

### DELETE `/v1.0/indices/{id}`
**Description:** Delete an index permanently

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Message": "Index deleted successfully",
    "IndexId": "idx_01JFXA1234567890ABCDEF"
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 5.67
}
```

### PUT `/v1.0/indices/{id}/labels`
**Description:** Replace all labels on an index (full replacement, not additive)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Index identifier

**Request Body:**
```json
{
  "Labels": ["production", "active"]
}
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Message": "Labels updated successfully",
    "Index": {
      "Identifier": "idx_01JFXA1234567890ABCDEF",
      "Name": "My Index",
      "Labels": ["production", "active"],
      "Tags": {"environment": "prod"}
    }
  },
  "ProcessingTimeMs": 3.45
}
```

### PUT `/v1.0/indices/{id}/tags`
**Description:** Replace all tags on an index (full replacement, not additive)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Index identifier

**Request Body:**
```json
{
  "Tags": {"environment": "production", "region": "us-west"}
}
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Message": "Tags updated successfully",
    "Index": {
      "Identifier": "idx_01JFXA1234567890ABCDEF",
      "Name": "My Index",
      "Labels": ["production"],
      "Tags": {"environment": "production", "region": "us-west"}
    }
  },
  "ProcessingTimeMs": 3.45
}
```

### PUT `/v1.0/indices/{id}/customMetadata`
**Description:** Replace custom metadata on an index

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Index identifier

**Request Body:**
```json
{
  "CustomMetadata": {"any": "json value", "nested": {"objects": "allowed"}}
}
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Message": "Custom metadata updated successfully",
    "Index": {
      "Identifier": "idx_01JFXA1234567890ABCDEF",
      "Name": "My Index",
      "Labels": ["production"],
      "Tags": {"environment": "prod"},
      "CustomMetadata": {"any": "json value", "nested": {"objects": "allowed"}}
    }
  },
  "ProcessingTimeMs": 3.45
}
```

## Document Management APIs

### GET `/v1.0/indices/{id}/documents`
**Description:** List all documents in an index (limited to 1000 documents), or retrieve specific documents by IDs using the `ids` query parameter.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| ids | string | No | Comma-separated list of document IDs to retrieve. When provided, returns only the specified documents with full metadata. |
| labels | string | No | Comma-separated list of labels to filter by. Documents must have ALL specified labels (AND logic). Case-insensitive matching. |
| tag.{key} | string | No | Tag filter in format `tag.key=value`. Documents must have ALL specified tags with matching values (AND logic). Can specify multiple tag parameters. |

#### List All Documents (Default Behavior)

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Documents": [
      {
        "DocumentId": "doc_01JFXA1234567890ABCDEF",
        "DocumentPath": "doc_01JFXA1234567890ABCDEF",
        "DocumentLength": 1234,
        "IndexedDate": "2025-01-01T12:00:00Z",
        "LastModified": "2025-01-01T12:00:00Z",
        "ContentSha256": "abc123...",
        "IndexingRuntimeMs": 12.34,
        "Labels": ["important"],
        "Tags": {"category": "tech"}
      }
    ],
    "Count": 1
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 3.45
}
```

#### Batch Document Retrieval

**Request:** `GET /v1.0/indices/{id}/documents?ids=doc1,doc2,doc3`

Retrieve multiple documents by ID in a single request. This is more efficient than making multiple individual document requests.

**Headers:**
```
Authorization: Bearer <token>
```

**Query Parameters:**
- `ids` (string): Comma-separated list of document IDs to retrieve

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Documents": [
      {
        "DocumentId": "doc1",
        "DocumentPath": "doc1",
        "DocumentLength": 1234,
        "IndexedDate": "2025-01-01T12:00:00Z",
        "LastModified": "2025-01-01T12:00:00Z",
        "ContentSha256": "abc123...",
        "IndexingRuntimeMs": 12.34,
        "Labels": ["important"],
        "Tags": {"category": "tech"},
        "CustomMetadata": null
      },
      {
        "DocumentId": "doc2",
        "DocumentPath": "doc2",
        "DocumentLength": 5678,
        "IndexedDate": "2025-01-01T12:00:00Z",
        "LastModified": "2025-01-01T12:00:00Z",
        "ContentSha256": "def456...",
        "IndexingRuntimeMs": 23.45,
        "Labels": [],
        "Tags": {},
        "CustomMetadata": null
      }
    ],
    "NotFound": ["doc3"],
    "Count": 2,
    "RequestedCount": 3
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 5.67
}
```

| Field | Type | Description |
|-------|------|-------------|
| Documents | array | List of documents that were found, with full metadata |
| NotFound | array | List of document IDs that were not found |
| Count | integer | Number of documents returned |
| RequestedCount | integer | Total number of document IDs that were requested |

#### Filtered Document Enumeration

Filter the document list by labels and/or tags. When filters are provided, only documents matching ALL specified criteria are returned (AND logic).

**Request:** `GET /v1.0/indices/{id}/documents?labels=important,reviewed`

**Headers:**
```
Authorization: Bearer <token>
```

**Query Parameters:**
- `labels` (string): Comma-separated list of labels. Documents must have ALL specified labels (case-insensitive).
- `tag.{key}` (string): Tag filter. Documents must match ALL specified tag key-value pairs. Specify multiple `tag.*` parameters for multiple tags.

**curl Examples:**
```bash
# List documents with label filter
curl -X GET "http://localhost:8000/v1.0/indices/{id}/documents?labels=important,reviewed" \
  -H "Authorization: Bearer {token}"

# List documents with tag filter
curl -X GET "http://localhost:8000/v1.0/indices/{id}/documents?tag.category=tech&tag.status=published" \
  -H "Authorization: Bearer {token}"

# List documents with both label and tag filters
curl -X GET "http://localhost:8000/v1.0/indices/{id}/documents?labels=important&tag.category=tech" \
  -H "Authorization: Bearer {token}"
```

The response format is the same as the default document list response.

#### Batch Document Deletion

**Request:** `POST /v1.0/indices/{id}/documents/delete`

Delete multiple documents by ID in a single request. This is more efficient than making multiple individual delete requests.

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier

**Request Body:**
```json
{
  "DocumentIds": ["doc1", "doc2", "doc3"]
}
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Deleted": ["doc1", "doc3"],
    "NotFound": ["doc2"],
    "DeletedCount": 2,
    "NotFoundCount": 1,
    "RequestedCount": 3
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 15.67
}
```

| Field | Type | Description |
|-------|------|-------------|
| Deleted | array | List of document IDs that were successfully deleted |
| NotFound | array | List of document IDs that were not found in the index |
| DeletedCount | integer | Number of documents that were deleted |
| NotFoundCount | integer | Number of document IDs that were not found |
| RequestedCount | integer | Total number of document IDs that were requested for deletion |

**Notes:**
- The operation is partially successful if some documents exist and some don't
- Successfully deleted documents are listed in `Deleted`, missing ones in `NotFound`
- Returns 400 Bad Request if the request body is missing or contains no document IDs

### POST `/v1.0/indices/{id}/documents`
**Description:** Add a new document to an index

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Index identifier

**Request Body:**
```json
{
  "Id": "my-document-id",
  "Content": "This is the content of my document that will be indexed for search.",
  "Labels": ["important", "review"],
  "Tags": {"category": "tech", "author": "Alice"},
  "CustomMetadata": {"source": "api", "version": 1}
}
```

Note: `Id` is optional. If omitted, a k-sortable unique ID will be auto-generated.

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 201,
  "ErrorMessage": null,
  "Data": {
    "DocumentId": "doc_01JFXA1234567890ABCDEF",
    "Message": "Document added successfully"
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 15.23
}
```

### GET `/v1.0/indices/{id}/documents/{docId}`
**Description:** Retrieve a specific document from an index, including labels and tags

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier
- `docId` (string): Document identifier

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "DocumentId": "doc_01JFXA1234567890ABCDEF",
    "DocumentPath": "doc_01JFXA1234567890ABCDEF",
    "OriginalFileName": "my-document",
    "DocumentLength": 1234,
    "IndexedDate": "2025-01-01T12:00:00Z",
    "LastModified": "2025-01-01T12:00:00Z",
    "ContentSha256": "abc123...",
    "IndexingRuntimeMs": 12.34,
    "Labels": ["important", "review"],
    "Tags": {"category": "tech", "author": "Alice"},
    "CustomMetadata": {"source": "api", "version": 1}
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 3.45
}
```

### HEAD `/v1.0/indices/{id}/documents/{docId}`
**Description:** Check if a document exists in an index

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier
- `docId` (string): Document identifier

**Response:**
- Returns `200 OK` with no body if the document exists
- Returns `404 Not Found` with no body if the index or document does not exist

### DELETE `/v1.0/indices/{id}/documents/{docId}`
**Description:** Remove a document from an index

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier
- `docId` (string): Document identifier

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "DocumentId": "doc_01JFXA1234567890ABCDEF",
    "Message": "Document deleted successfully"
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 8.90
}
```

### PUT `/v1.0/indices/{id}/documents/{docId}/labels`
**Description:** Replace all labels on a document (full replacement, not additive)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Index identifier
- `docId` (string): Document identifier

**Request Body:**
```json
{
  "Labels": ["reviewed", "approved"]
}
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Message": "Labels updated successfully",
    "Document": {
      "DocumentId": "doc_01JFXA1234567890ABCDEF",
      "Labels": ["reviewed", "approved"],
      "Tags": {"category": "tech"}
    }
  },
  "ProcessingTimeMs": 3.45
}
```

### PUT `/v1.0/indices/{id}/documents/{docId}/tags`
**Description:** Replace all tags on a document (full replacement, not additive)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Index identifier
- `docId` (string): Document identifier

**Request Body:**
```json
{
  "Tags": {"category": "finance", "priority": "high"}
}
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Message": "Tags updated successfully",
    "Document": {
      "DocumentId": "doc_01JFXA1234567890ABCDEF",
      "Labels": ["important"],
      "Tags": {"category": "finance", "priority": "high"}
    }
  },
  "ProcessingTimeMs": 3.45
}
```

### PUT `/v1.0/indices/{id}/documents/{docId}/customMetadata`
**Description:** Replace custom metadata on a document

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Index identifier
- `docId` (string): Document identifier

**Request Body:**
```json
{
  "CustomMetadata": {"source": "manual", "reviewed_by": "admin", "score": 95}
}
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Message": "Custom metadata updated successfully",
    "Document": {
      "DocumentId": "doc_01JFXA1234567890ABCDEF",
      "Labels": ["important"],
      "Tags": {"category": "tech"},
      "CustomMetadata": {"source": "manual", "reviewed_by": "admin", "score": 95}
    }
  },
  "ProcessingTimeMs": 3.45
}
```

## Search APIs

### POST `/v1.0/indices/{id}/search`
**Description:** Search for documents within an index. Each search result includes the full document metadata with labels and tags.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Index identifier

**Request Body:**
```json
{
  "Query": "machine learning algorithms",
  "MaxResults": 10,
  "UseAndLogic": false,
  "Labels": ["important"],
  "Tags": {"category": "tech"}
}
```

Note: `Labels` and `Tags` are optional filters. When provided, documents must match ALL specified labels (AND logic, case-insensitive) and ALL specified tags (AND logic, exact match). Filtering is performed via SQL JOINs during document retrieval for optimal performance.

Search enrichment is opt-in and additive. Default search responses preserve the legacy result fields. `IncludeMatchedTerms` and `IncludeTermDetails` use data already collected during scoring and do not add database calls. `IncludeDocumentTermStats` adds at most one grouped query for the result document IDs after `MaxResults` is applied.

#### Wildcard Search

Use `"*"` as the `Query` value to return all documents without term matching. This is useful for browsing documents by metadata filters.

- Wildcard results have a score of 0 and are ordered by creation date
- Can be combined with `Labels` and `Tags` filters to browse matching documents
- Respects the `MaxResults` parameter
- If requested, `MatchedTerms` and `TermDetails` are empty arrays for wildcard results
- If requested, `DocumentTermStats` is populated for wildcard results

**curl Examples:**
```bash
# Wildcard search - return all documents
curl -X POST "http://localhost:8000/v1.0/indices/{id}/search" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"Query": "*"}'

# Wildcard search with filters
curl -X POST "http://localhost:8000/v1.0/indices/{id}/search" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"Query": "*", "Labels": ["important"], "Tags": {"category": "tech"}}'
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Query": "machine learning algorithms",
    "Results": [
      {
        "DocumentId": "doc_01JFXA1234567890ABCDEF",
        "Document": {
          "DocumentId": "doc_01JFXA1234567890ABCDEF",
          "DocumentPath": "doc_01JFXA1234567890ABCDEF",
          "DocumentLength": 5000,
          "IndexedDate": "2025-01-01T12:00:00Z",
          "IndexingRuntimeMs": 15.67,
          "Labels": ["important"],
          "Tags": {"category": "tech"},
          "CustomMetadata": null
        },
        "Score": 0.85,
        "MatchedTermCount": 2,
        "TermScores": {
          "machine": 0.42,
          "learning": 0.43
        },
        "TermFrequencies": {
          "machine": 2,
          "learning": 1
        },
        "TotalTermMatches": 3
      }
    ],
    "TotalCount": 1,
    "MaxResults": 10,
    "SearchTime": 12.34,
    "TimingInfo": {
      "TermLookupMs": 1,
      "TermsFound": 2,
      "MainSearchMs": 2,
      "MatchesFound": 1,
      "TermFrequenciesMs": 1,
      "TermFrequencyRecords": 2,
      "DocumentMetadataMs": 2,
      "DocumentsFetched": 1,
      "DocumentCountMs": 1,
      "TotalDocuments": 100
    }
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 12.34
}
```

**Enriched Request:**
```json
{
  "Query": "machine learning algorithms",
  "MaxResults": 10,
  "IncludeMatchedTerms": true,
  "IncludeTermDetails": true,
  "IncludeDocumentTermStats": true
}
```

**Enriched Result Fields:**
```json
{
  "MatchedTerms": ["learning", "machine"],
  "TermDetails": [
    {
      "Term": "learning",
      "Score": 0.43,
      "Frequency": 1
    },
    {
      "Term": "machine",
      "Score": 0.42,
      "Frequency": 2
    }
  ],
  "DocumentTermStats": {
    "UniqueTermCount": 1840,
    "TotalTermOccurrences": 78893
  }
}
```

**Wildcard Stats Request:**
```json
{
  "Query": "*",
  "MaxResults": 25,
  "IncludeDocumentTermStats": true
}
```

## Terms APIs

### GET `/v1.0/indices/{id}/terms/top`
**Description:** Get the top terms in an index sorted by document frequency (most common first). This is useful for understanding the vocabulary of an index and identifying the most frequently occurring terms.

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier

**Query Parameters:**
| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| limit | integer | No | 10 | Maximum number of terms to return |

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "the": 150,
    "and": 120,
    "search": 85,
    "document": 72,
    "index": 68,
    "machine": 45,
    "learning": 42,
    "data": 38,
    "algorithm": 35,
    "neural": 30
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 5.23
}
```

**Notes:**
- The response is a dictionary where keys are terms and values are their document frequencies (the number of documents containing each term)
- Terms are sorted by document frequency in descending order
- Returns 404 if the index does not exist

**Example:**
```bash
# Get top 10 terms (default)
curl -X GET http://localhost:8080/v1.0/indices/myindex/terms/top \
  -H "Authorization: Bearer YOUR_TOKEN"

# Get top 5 terms
curl -X GET "http://localhost:8080/v1.0/indices/myindex/terms/top?limit=5" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## Admin - Tenant APIs

### GET `/v1.0/tenants`
**Description:** List all tenants. Global admins see all tenants; tenant admins see only their own.

**Headers:**
```
Authorization: Bearer <token>
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Tenants": [...],
    "Count": 1
  },
  "ProcessingTimeMs": 3.45
}
```

### POST `/v1.0/tenants`
**Description:** Create a new tenant (requires global admin)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "Name": "My Tenant",
  "Description": "Description of the tenant"
}
```

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 201,
  "ErrorMessage": null,
  "Data": {
    "Message": "Tenant created successfully",
    "Tenant": {...}
  },
  "ProcessingTimeMs": 3.45
}
```

### GET `/v1.0/tenants/{id}`
**Description:** Get a specific tenant with statistics

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Tenant identifier

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Tenant": {...},
    "Statistics": {...}
  },
  "ProcessingTimeMs": 3.45
}
```

### PUT `/v1.0/tenants/{id}`
**Description:** Update a tenant (requires global admin)

### DELETE `/v1.0/tenants/{id}`
**Description:** Delete a tenant and all its data (requires global admin)

### PUT `/v1.0/tenants/{id}/labels`
**Description:** Update labels on a tenant (full replacement)

**Request Body:**
```json
{
  "Labels": ["production", "active"]
}
```

### PUT `/v1.0/tenants/{id}/tags`
**Description:** Update tags on a tenant (full replacement)

**Request Body:**
```json
{
  "Tags": {"environment": "production", "region": "us-west"}
}
```

## Admin - User APIs

### GET `/v1.0/tenants/{id}/users`
**Description:** List all users for a tenant

### POST `/v1.0/tenants/{id}/users`
**Description:** Create a new user for a tenant

### GET `/v1.0/tenants/{id}/users/{userId}`
**Description:** Get a specific user by ID

### PUT `/v1.0/tenants/{id}/users/{userId}`
**Description:** Update a user

### DELETE `/v1.0/tenants/{id}/users/{userId}`
**Description:** Delete a user

### PUT `/v1.0/tenants/{id}/users/{userId}/labels`
**Description:** Update labels on a user (full replacement)

**Request Body:**
```json
{
  "Labels": ["admin", "developer"]
}
```

### PUT `/v1.0/tenants/{id}/users/{userId}/tags`
**Description:** Update tags on a user (full replacement)

**Request Body:**
```json
{
  "Tags": {"department": "engineering", "role": "senior"}
}
```

## Admin - Credential APIs

### GET `/v1.0/tenants/{id}/credentials`
**Description:** List all API credentials for a tenant

### POST `/v1.0/tenants/{id}/credentials`
**Description:** Create a new API credential for a tenant

### PUT `/v1.0/tenants/{id}/credentials/{credId}`
**Description:** Update an API credential (activate/deactivate)

### DELETE `/v1.0/tenants/{id}/credentials/{credId}`
**Description:** Revoke an API credential

### PUT `/v1.0/tenants/{id}/credentials/{credId}/labels`
**Description:** Update labels on a credential (full replacement)

**Request Body:**
```json
{
  "Labels": ["production", "api-key"]
}
```

### PUT `/v1.0/tenants/{id}/credentials/{credId}/tags`
**Description:** Update tags on a credential (full replacement)

**Request Body:**
```json
{
  "Tags": {"environment": "production", "service": "backend"}
}
```

## Backup & Restore APIs

### POST `/v1.0/indices/{id}/backup`
**Description:** Create a backup of an index. Returns a ZIP archive containing the index database and metadata.

**Headers:**
```
Authorization: Bearer <token>
```

**Path Parameters:**
- `id` (string): Index identifier

**Response:**
- Content-Type: `application/zip`
- Content-Disposition: `attachment; filename="backup-{indexId}-{timestamp}.vbx"`
- Returns binary ZIP archive on success
- Returns JSON error response on failure

**Example:**
```bash
curl -X POST http://localhost:8080/v1.0/indices/myindex/backup \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -o myindex-backup.vbx
```

### POST `/v1.0/indices/restore`
**Description:** Restore a backup to create a new index. Upload a backup archive (.vbx file) via multipart form data.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

**Form Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| file | file | Yes | The backup archive (.vbx file) |
| name | string | No | Name for the restored index (uses original name if not specified) |
| indexId | string | No | Custom identifier for the restored index (auto-generated if not specified) |

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 201,
  "ErrorMessage": null,
  "Data": {
    "Success": true,
    "IndexId": "idx_01JFXA1234567890ABCDEF",
    "Message": "Index restored successfully",
    "Warnings": []
  },
  "ProcessingTimeMs": 150.5
}
```

**Example:**
```bash
curl -X POST http://localhost:8080/v1.0/indices/restore \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@myindex-backup.vbx" \
  -F "name=restored-index"
```

### POST `/v1.0/indices/{id}/restore`
**Description:** Restore a backup by replacing an existing index. The existing index will be deleted and replaced with the contents of the backup.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

**Path Parameters:**
- `id` (string): Index identifier of the existing index to replace

**Form Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| file | file | Yes | The backup archive (.vbx file) |

**Response:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": true,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 200,
  "ErrorMessage": null,
  "Data": {
    "Success": true,
    "IndexId": "idx_01JFXA1234567890ABCDEF",
    "Message": "Index restored successfully",
    "Warnings": []
  },
  "ProcessingTimeMs": 200.3
}
```

**Notes:**
- Cannot restore to in-memory indices
- The existing index must exist and be accessible
- Returns 423 if the index is locked

**Example:**
```bash
curl -X POST http://localhost:8080/v1.0/indices/myindex/restore \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@myindex-backup.vbx"
```

## Error Handling

All API endpoints return errors in a consistent format:

### Error Response Format
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": false,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 400,
  "ErrorMessage": "Error description",
  "Data": null,
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 5.2
}
```

### Common HTTP Status Codes

| Status Code | Description |
|-------------|-------------|
| 200 | Success |
| 201 | Created |
| 400 | Bad Request - Invalid input |
| 401 | Unauthorized - Authentication required |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource doesn't exist |
| 409 | Conflict - Resource already exists |
| 500 | Internal Server Error |

### Error Examples

**400 Bad Request:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": false,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 400,
  "ErrorMessage": "Name is required",
  "Data": null,
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 1.0
}
```

**401 Unauthorized:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": false,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 401,
  "ErrorMessage": "Invalid credentials",
  "Data": null,
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 1.5
}
```

**403 Forbidden:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": false,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 403,
  "ErrorMessage": "Admin access required",
  "Data": null,
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 1.0
}
```

**404 Not Found:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": false,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 404,
  "ErrorMessage": "Index not found",
  "Data": null,
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 0.5
}
```

**409 Conflict:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": false,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 409,
  "ErrorMessage": "Index with this name already exists in the tenant",
  "Data": null,
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 2.0
}
```

**500 Internal Server Error:**
```json
{
  "Guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Success": false,
  "TimestampUtc": "2025-01-01T12:00:00Z",
  "StatusCode": 500,
  "ErrorMessage": "Error performing search: <details>",
  "Data": null,
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 2.0
}
```

## Configuration Options

### Storage Modes
- **InMemory: true**: Index stored in an in-memory SQLite database (fastest, data lost when application terminates)
- **InMemory: false** (default): Index stored in a file-based SQLite database (persistent)

### Text Processing Options
- **EnableLemmatizer**: Reduces words to their base forms (e.g., "running" -> "run")
- **EnableStopWordRemover**: Filters out common words (e.g., "the", "and", "of")
- **MinTokenLength**: Minimum token length (0 = disabled)
- **MaxTokenLength**: Maximum token length (0 = disabled)

### Metadata Features
- **Labels**: String array for categorizing documents or indices (e.g., ["important", "review"])
- **Tags**: Key-value pairs for custom metadata (e.g., {"category": "tech", "author": "Alice"})
- **CustomMetadata**: Any JSON-serializable value for arbitrary custom data
- Searches can filter by labels (AND logic, case-insensitive) and tags (AND logic, exact match)

---

For additional support or questions about the Verbex REST API, please refer to the [main documentation](README.md) or the [CLI documentation](VBX_CLI.md).
