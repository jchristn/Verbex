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

### IndexConfiguration
```json
{
  "Id": "string",
  "Name": "string",
  "Description": "string",
  "RepositoryFilename": "string",
  "InMemory": "boolean",
  "StorageMode": "string (MemoryOnly or other)",
  "EnableLemmatizer": "boolean",
  "EnableStopWordRemover": "boolean",
  "MinTokenLength": "integer",
  "MaxTokenLength": "integer",
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
| Index | DELETE | `/v1.0/indices/{id}` | Delete index | Yes |
| Document | GET | `/v1.0/indices/{id}/documents` | List documents | Yes |
| Document | POST | `/v1.0/indices/{id}/documents` | Add document | Yes |
| Document | GET | `/v1.0/indices/{id}/documents/{docId}` | Get document | Yes |
| Document | DELETE | `/v1.0/indices/{id}/documents/{docId}` | Delete document | Yes |
| Search | POST | `/v1.0/indices/{id}/search` | Search documents | Yes |

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
  "Username": "admin",
  "Password": "password"
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
    "Token": "base64-encoded-token-here",
    "Username": "admin"
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
        "Id": "sample-index",
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
  "Id": "my-index",
  "Name": "My Index",
  "Description": "My custom index for documents",
  "RepositoryFilename": "my-index.db",
  "InMemory": false,
  "StorageMode": "MemoryOnly",
  "EnableLemmatizer": true,
  "EnableStopWordRemover": true,
  "MinTokenLength": 2,
  "MaxTokenLength": 50,
  "Labels": ["production", "search"],
  "Tags": {"environment": "prod", "team": "engineering"}
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
    "Message": "Index created successfully",
    "Index": {
      "Id": "my-index",
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
  "ErrorMessage": "Required fields: id, name",
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
  "ErrorMessage": "Index with this ID already exists",
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

### Storage Modes (Core Library)
- **InMemory**: Index stored in an in-memory SQLite database (fastest, data lost when application terminates)
- **OnDisk**: Index stored in a file-based SQLite database (slower, maximum durability)

### Storage Modes (Server Configuration)
- **MemoryOnly**: Corresponds to InMemory storage mode

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