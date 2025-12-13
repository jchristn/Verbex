# Verbex.Server REST API Documentation

This document provides comprehensive examples for exercising the Verbex.Server REST API using cURL commands.

## Base URL

The server runs on `http://localhost:8080` by default.

## API Version

All endpoints are versioned under `/v1.0/` unless otherwise specified.

## Authentication

The API uses Bearer token authentication. The default admin token is `verbexadmin` but can be configured via the `VERBEX_ADMIN_TOKEN` environment variable.

---

## Health Check Endpoints

### GET / (Root Health Check)

Basic health check endpoint.

```bash
curl -X GET http://localhost:8080/
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "status": "Healthy",
    "version": "1.0.0",
    "timestamp": "2024-01-15T10:30:00.000Z"
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 1.2345
}
```

### GET /v1.0/health

Detailed health check endpoint.

```bash
curl -X GET http://localhost:8080/v1.0/health
```

**Expected Response:** Same as root health check.

---

## Authentication Endpoints

### POST /v1.0/auth/login

Authenticate with username and password to receive a token.

```bash
curl -X POST http://localhost:8080/v1.0/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "password"
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "token": "base64-encoded-token-here",
    "username": "admin"
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 5.6789
}
```

**Error Response (Invalid Credentials):**
```bash
curl -X POST http://localhost:8080/v1.0/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "invalid",
    "password": "wrong"
  }'
```

```json
{
  "success": false,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 401,
  "errorMessage": "Invalid credentials",
  "data": null,
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 2.3456
}
```

### GET /v1.0/auth/validate

Validate a Bearer token.

```bash
curl -X GET http://localhost:8080/v1.0/auth/validate \
  -H "Authorization: Bearer verbexadmin"
```

**Expected Response (Valid Token):**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "valid": true
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 1.2345
}
```

**Expected Response (Invalid Token):**
```bash
curl -X GET http://localhost:8080/v1.0/auth/validate \
  -H "Authorization: Bearer invalid-token"
```

```json
{
  "success": false,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 401,
  "errorMessage": null,
  "data": {
    "valid": false
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 1.2345
}
```

---

## Index Management Endpoints

### GET /v1.0/indices

List all available indices.

```bash
curl -X GET http://localhost:8080/v1.0/indices \
  -H "Authorization: Bearer verbexadmin"
```

**Expected Response (Empty State):**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "indices": [],
    "count": 0
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 0.054
}
```

**Expected Response (After Creating Indices):**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "indices": [
      {
        "id": "main-index",
        "name": "Main Index",
        "description": "Main document search index",
        "enabled": true,
        "inMemory": false,
        "createdUtc": "2024-01-15T10:30:00.000Z"
      },
      {
        "id": "test-index",
        "name": "Test Index",
        "description": "Test index for development purposes",
        "enabled": true,
        "inMemory": true,
        "createdUtc": "2024-01-15T10:30:00.000Z"
      }
    ],
    "count": 2
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 2.345
}
```

### POST /v1.0/indices

Create a new index.

```bash
curl -X POST http://localhost:8080/v1.0/indices \
  -H "Authorization: Bearer verbexadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "id": "main-index",
    "name": "Main Document Index",
    "description": "Main document search index",
    "repositoryFilename": "main-documents.db",
    "inMemory": false
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 201,
  "errorMessage": null,
  "data": {
    "message": "Index created successfully",
    "index": {
      "id": "documents",
      "name": "Document Index",
      "description": "Document search index",
      "inMemory": false,
      "createdUtc": "2024-01-15T10:30:00.000Z"
    }
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 10.123
}
```

### GET /v1.0/indices/{id}

Get details about a specific index.

```bash
curl -X GET http://localhost:8080/v1.0/indices/main \
  -H "Authorization: Bearer verbexadmin"
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "id": "main",
    "name": "Main Index",
    "description": "Default main inverted index for general document storage",
    "enabled": true,
    "inMemory": false,
    "createdUtc": "2024-01-15T10:30:00.000Z",
    "statistics": {
      "documentCount": 0,
      "termCount": 0,
      "averageDocumentLength": 0,
      "indexSizeBytes": 0
    }
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 3.456
}
```

### DELETE /v1.0/indices/{id}

Delete a specific index.

```bash
curl -X DELETE http://localhost:8080/v1.0/indices/documents \
  -H "Authorization: Bearer verbexadmin"
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "message": "Index deleted successfully",
    "indexId": "documents"
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 5.678
}
```

---

## Document Management Endpoints

All document operations are now scoped to specific indices using the index ID in the URL path.

### GET /v1.0/indices/{indexId}/documents

Get all documents from a specific index.

```bash
curl -X GET http://localhost:8080/v1.0/indices/main/documents \
  -H "Authorization: Bearer verbexadmin"
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "documents": [],
    "count": 0
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 3.456
}
```

### POST /v1.0/indices/{indexId}/documents

Add a document to a specific index.

```bash
curl -X POST http://localhost:8080/v1.0/indices/main/documents \
  -H "Authorization: Bearer verbexadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "id": "doc-123",
    "content": "This is a sample document with some text to be indexed for searching."
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 201,
  "errorMessage": null,
  "data": {
    "documentId": "doc-123",
    "message": "Document added successfully"
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 15.234
}
```

### GET /v1.0/indices/{indexId}/documents/{docId}

Retrieve a specific document by ID from a specific index.

```bash
curl -X GET http://localhost:8080/v1.0/indices/main/documents/doc-123 \
  -H "Authorization: Bearer verbexadmin"
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "documentId": "doc-123",
    "documentPath": "doc_doc-123",
    "content": "This is a sample document with some text to be indexed for searching.",
    "metadata": {},
    "addedUtc": "2024-01-15T10:30:00.000Z"
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 3.456
}
```

### DELETE /v1.0/indices/{indexId}/documents/{docId}

Delete a specific document by ID from a specific index.

```bash
curl -X DELETE http://localhost:8080/v1.0/indices/main/documents/doc-123 \
  -H "Authorization: Bearer verbexadmin"
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "documentId": "doc-123",
    "message": "Document deleted successfully"
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 8.901
}
```

---

## Search Endpoints

Search operations are now scoped to specific indices using the index ID in the URL path.

### POST /v1.0/indices/{indexId}/search

Search for documents within a specific index.

```bash
curl -X POST http://localhost:8080/v1.0/indices/main/search \
  -H "Authorization: Bearer verbexadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "sample document",
    "maxResults": 10
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 200,
  "errorMessage": null,
  "data": {
    "query": "sample document",
    "results": [
      {
        "documentId": "doc-123",
        "score": 0.85,
        "matchedTerms": {
          "sample": {
            "termFrequency": 2,
            "score": 0.42
          },
          "document": {
            "termFrequency": 1,
            "score": 0.43
          }
        }
      }
    ],
    "totalCount": 1,
    "maxResults": 10
  },
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 12.345
}
```

**Search Across Multiple Indices Example:**
```bash
# Search in main index
curl -X POST http://localhost:8080/v1.0/indices/main/search \
  -H "Authorization: Bearer verbexadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "technical documentation",
    "maxResults": 5
  }'

# Search in test index
curl -X POST http://localhost:8080/v1.0/indices/test/search \
  -H "Authorization: Bearer verbexadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "technical documentation",
    "maxResults": 5
  }'
```

---

## Error Responses

### 400 Bad Request

```json
{
  "success": false,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 400,
  "errorMessage": "Request body is required",
  "data": null,
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 1.0
}
```

### 401 Unauthorized

```json
{
  "success": false,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 401,
  "errorMessage": "Invalid credentials",
  "data": null,
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 1.5
}
```

### 404 Not Found

```json
{
  "success": false,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 404,
  "errorMessage": "Not found",
  "data": null,
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 0.5
}
```

### 500 Internal Server Error

```json
{
  "success": false,
  "timestampUtc": "2024-01-15T10:30:00.000Z",
  "statusCode": 500,
  "errorMessage": "Internal server error message",
  "data": null,
  "headers": {},
  "totalCount": null,
  "skip": null,
  "processingTimeMs": 2.0
}
```

---

## Environment Variables

Configure the server using these environment variables:

- `VERBEX_ADMIN_TOKEN` - Admin bearer token (default: "verbexadmin")
- `VERBEX_DB_FILE` - Database filename (default: "verbex.db")
- `VERBEX_MAX_CONCURRENT_OPS` - Maximum concurrent operations (default: 4)
- `VERBEX_IN_MEMORY` - Use in-memory mode (default: false)

---

## Testing Notes

1. **Start the Server**: Run `dotnet run --project Verbex.Server` to start the server
2. **Authentication**: Most endpoints require the `Authorization: Bearer verbexadmin` header
3. **Content-Type**: POST endpoints require `Content-Type: application/json` header
4. **JSON Validation**: Request bodies must be valid JSON format
5. **Response Format**: All responses follow the same JSON structure with success, statusCode, data, etc.

For automated testing, see the `test.bat` file in the same directory.