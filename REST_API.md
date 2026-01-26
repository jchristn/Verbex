# Verbex REST API Documentation

This document describes the REST API endpoints available in the Verbex inverted index server.

## Table of Contents

- [Authentication](#authentication)
- [Data Structures](#data-structures)
- [API Endpoints Overview](#api-endpoints-overview)
- [Health and Status](#health-and-status)
- [Authentication APIs](#authentication-apis)
- [Index Management APIs](#index-management-apis)
- [Document Management APIs](#document-management-apis)
- [Search APIs](#search-apis)
- [Admin - Tenant APIs](#admin---tenant-apis)
- [Admin - User APIs](#admin---user-apis)
- [Admin - Credential APIs](#admin---credential-apis)
- [Error Handling](#error-handling)

## Authentication

The Verbex REST API uses Bearer token authentication. Most endpoints require authentication except for health checks and login.

### Authentication Header
```
Authorization: Bearer <token>
```

### Getting an Authentication Token
Use the `/v1.0/auth/login` endpoint to obtain a token by providing valid credentials.

## Data Structures

### CreateIndexRequest
```json
{
  "Name": "string (required)",
  "Description": "string (optional)",
  "InMemory": "boolean (optional, default: false)",
  "EnableLemmatizer": "boolean (optional, default: false)",
  "EnableStopWordRemover": "boolean (optional, default: false)",
  "MinTokenLength": "integer (optional, default: 0)",
  "MaxTokenLength": "integer (optional, default: 0)",
  "Labels": ["string (optional)"],
  "Tags": {"key": "value (optional)"}
}
```

### IndexMetadata (Response)
```json
{
  "Identifier": "string (auto-generated)",
  "TenantId": "string",
  "Name": "string",
  "Description": "string",
  "Enabled": "boolean",
  "InMemory": "boolean",
  "CreatedUtc": "datetime",
  "Labels": ["string"],
  "Tags": {"key": "value"}
}
```

### SearchRequest
```json
{
  "Query": "string",
  "MaxResults": "integer",
  "Labels": ["string (optional)"],
  "Tags": {"key": "value (optional)"}
}
```

### DocumentRequest (AddDocumentRequest)
```json
{
  "Id": "string (optional)",
  "Content": "string",
  "Labels": ["string"],
  "Tags": {"key": "value"}
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
| Health | GET | `/v1.0/health` | Detailed health status | No |
| Auth | POST | `/v1.0/auth/login` | Login and get token | No |
| Auth | GET | `/v1.0/auth/validate` | Validate token | No |
| Index | GET | `/v1.0/indices` | List all indices | Yes |
| Index | POST | `/v1.0/indices` | Create new index | Yes |
| Index | GET | `/v1.0/indices/{id}` | Get index details | Yes |
| Index | HEAD | `/v1.0/indices/{id}` | Check if index exists | Yes |
| Index | DELETE | `/v1.0/indices/{id}` | Delete index | Yes |
| Document | GET | `/v1.0/indices/{id}/documents` | List documents | Yes |
| Document | POST | `/v1.0/indices/{id}/documents` | Add document | Yes |
| Document | GET | `/v1.0/indices/{id}/documents/{docId}` | Get document | Yes |
| Document | HEAD | `/v1.0/indices/{id}/documents/{docId}` | Check if document exists | Yes |
| Document | DELETE | `/v1.0/indices/{id}/documents/{docId}` | Delete document | Yes |
| Search | POST | `/v1.0/indices/{id}/search` | Search documents | Yes |
| Tenant | GET | `/v1.0/tenants` | List tenants | Yes (Admin) |
| Tenant | POST | `/v1.0/tenants` | Create tenant | Yes (Admin) |
| Tenant | GET | `/v1.0/tenants/{id}` | Get tenant | Yes (Admin) |
| Tenant | PUT | `/v1.0/tenants/{id}` | Update tenant | Yes (Admin) |
| Tenant | DELETE | `/v1.0/tenants/{id}` | Delete tenant | Yes (Admin) |
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
| Credential | GET | `/v1.0/tenants/{id}/credentials/{credId}` | Get credential | Yes (Admin) |
| Credential | PUT | `/v1.0/tenants/{id}/credentials/{credId}` | Update credential | Yes (Admin) |
| Credential | DELETE | `/v1.0/tenants/{id}/credentials/{credId}` | Delete credential | Yes (Admin) |
| Credential | PUT | `/v1.0/tenants/{id}/credentials/{credId}/labels` | Update credential labels | Yes (Admin) |
| Credential | PUT | `/v1.0/tenants/{id}/credentials/{credId}/tags` | Update credential tags | Yes (Admin) |

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

### GET `/v1.0/health`
**Description:** Detailed health status

**Response:** Same as above

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
    "Username": "admin",
    "Email": "admin@example.com",
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
    "Valid": true
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 1.23
}
```

## Index Management APIs

### GET `/v1.0/indices`
**Description:** Retrieve list of all indices

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
  "Name": "My Index",
  "Description": "My custom index for documents",
  "InMemory": false,
  "EnableLemmatizer": true,
  "EnableStopWordRemover": true,
  "MinTokenLength": 2,
  "MaxTokenLength": 50,
  "Labels": ["production", "search"],
  "Tags": {"environment": "prod", "team": "engineering"}
}
```

Note: The `Identifier` is auto-generated by the server. The index is associated with the tenant from your authentication context.

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
      "Tags": {"environment": "prod", "team": "engineering"}
    }
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 10.12
}
```

### GET `/v1.0/indices/{id}`
**Description:** Get detailed information about a specific index

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
    "IndexId": "my-index"
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 5.67
}
```

## Document Management APIs

### GET `/v1.0/indices/{id}/documents`
**Description:** List all documents in an index

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
    "Documents": [],
    "Count": 0
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 3.45
}
```

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
  "Tags": {"category": "tech", "author": "Alice"}
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
**Description:** Retrieve a specific document from an index

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
    "Name": "my-document",
    "ContentHash": "abc123...",
    "DocumentLength": 1234,
    "TermCount": 45,
    "IndexedUtc": "2025-01-01T12:00:00Z",
    "Labels": ["important", "review"],
    "Tags": {"category": "tech", "author": "Alice"}
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

## Search APIs

### POST `/v1.0/indices/{id}/search`
**Description:** Search for documents within an index

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
  "Labels": ["important"],
  "Tags": {"category": "tech"}
}
```

Note: `Labels` and `Tags` are optional. When provided, documents must match ALL specified labels (AND logic, case-insensitive) and ALL specified tags (AND logic, exact match). Filtering is performed via SQL JOINs during document retrieval for optimal performance.

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
          "Name": "ml-paper",
          "DocumentLength": 5000,
          "TermCount": 150,
          "Labels": ["important"],
          "Tags": {"category": "tech"}
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
    "SearchTime": 12.34
  },
  "Headers": {},
  "TotalCount": null,
  "Skip": null,
  "ProcessingTimeMs": 12.34
}
```

## Admin - Tenant APIs

### PUT `/v1.0/tenants/{id}/labels`
**Description:** Update labels on a tenant (full replacement)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Tenant identifier

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
    "Message": "Labels updated successfully"
  },
  "ProcessingTimeMs": 3.45
}
```

### PUT `/v1.0/tenants/{id}/tags`
**Description:** Update tags on a tenant (full replacement)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Tenant identifier

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
    "Message": "Tags updated successfully"
  },
  "ProcessingTimeMs": 3.45
}
```

## Admin - User APIs

### PUT `/v1.0/tenants/{id}/users/{userId}/labels`
**Description:** Update labels on a user (full replacement)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Tenant identifier
- `userId` (string): User identifier

**Request Body:**
```json
{
  "Labels": ["admin", "developer"]
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
    "Message": "Labels updated successfully"
  },
  "ProcessingTimeMs": 3.45
}
```

### PUT `/v1.0/tenants/{id}/users/{userId}/tags`
**Description:** Update tags on a user (full replacement)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Tenant identifier
- `userId` (string): User identifier

**Request Body:**
```json
{
  "Tags": {"department": "engineering", "role": "senior"}
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
    "Message": "Tags updated successfully"
  },
  "ProcessingTimeMs": 3.45
}
```

## Admin - Credential APIs

### PUT `/v1.0/tenants/{id}/credentials/{credId}/labels`
**Description:** Update labels on a credential (full replacement)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Tenant identifier
- `credId` (string): Credential identifier

**Request Body:**
```json
{
  "Labels": ["production", "api-key"]
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
    "Message": "Labels updated successfully"
  },
  "ProcessingTimeMs": 3.45
}
```

### PUT `/v1.0/tenants/{id}/credentials/{credId}/tags`
**Description:** Update tags on a credential (full replacement)

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Path Parameters:**
- `id` (string): Tenant identifier
- `credId` (string): Credential identifier

**Request Body:**
```json
{
  "Tags": {"environment": "production", "service": "backend"}
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
    "Message": "Tags updated successfully"
  },
  "ProcessingTimeMs": 3.45
}
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
- **enableLemmatizer**: Reduces words to their base forms (e.g., "running" → "run")
- **enableStopWordRemover**: Filters out common words (e.g., "the", "and", "of")
- **minTokenLength**: Minimum token length (0 = disabled)
- **maxTokenLength**: Maximum token length (0 = disabled)

### Metadata Features
- **labels**: String array for categorizing documents or indices (e.g., ["important", "review"])
- **tags**: Key-value pairs for custom metadata (e.g., {"category": "tech", "author": "Alice"})
- Searches can filter by labels (AND logic, case-insensitive) and tags (AND logic, exact match)

---

For additional support or questions about the Verbex REST API, please refer to the [main documentation](README.md) or the [CLI documentation](VBX_CLI.md).