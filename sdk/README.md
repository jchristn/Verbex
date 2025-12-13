# Verbex SDKs

This folder contains official SDKs for interacting with the Verbex Inverted Index REST API.

## Available SDKs

| Language | Folder | Requirements |
|----------|--------|--------------|
| Python | `python/` | Python 3.8+, requests library |
| JavaScript | `js/` | Node.js 18+ |
| C# | `csharp/` | .NET 8.0 SDK |

## Quick Start

### Python

```bash
cd sdk/python
pip install -r requirements.txt
python test_harness.py http://localhost:8080 verbexadmin
```

### JavaScript

```bash
cd sdk/js
node test-harness.js http://localhost:8080 verbexadmin
```

### C#

```bash
cd sdk/csharp
dotnet build
dotnet run --project Verbex.Sdk.TestHarness -- http://localhost:8080 verbexadmin
```

## Test Harness Output Format

All test harnesses produce consistent output in the following format:

```
============================================================
  Verbex SDK Test Harness - [Language]
============================================================
  Endpoint: http://localhost:8080
  Test Index: test-index-xxxxxxxx
  Started: 2024-01-15T10:30:00.000Z

--- Health Checks ---
  [PASS] Root health check (12.34ms)
  [PASS] Health endpoint (10.56ms)

--- Authentication ---
  [PASS] Login with valid credentials (15.23ms)
  [PASS] Login with invalid credentials (8.45ms)
  ...

============================================================
  Test Summary
============================================================
  Total Tests: 32
  Passed: 32
  Failed: 0
  Duration: 1.23s
  Result: SUCCESS
```

## API Coverage

All SDKs cover the complete Verbex REST API:

### Health Endpoints
- `GET /` - Root health check
- `GET /v1.0/health` - Health endpoint

### Authentication
- `POST /v1.0/auth/login` - Login with credentials
- `GET /v1.0/auth/validate` - Validate bearer token

### Index Management
- `GET /v1.0/indices` - List all indices
- `POST /v1.0/indices` - Create a new index
- `GET /v1.0/indices/{id}` - Get index details
- `DELETE /v1.0/indices/{id}` - Delete an index

### Document Management
- `GET /v1.0/indices/{id}/documents` - List documents
- `POST /v1.0/indices/{id}/documents` - Add a document
- `GET /v1.0/indices/{id}/documents/{docId}` - Get document
- `DELETE /v1.0/indices/{id}/documents/{docId}` - Delete document

### Search
- `POST /v1.0/indices/{id}/search` - Search documents

## Test Cases

Each test harness validates 32 test cases:

| Category | Tests |
|----------|-------|
| Health Checks | 2 |
| Authentication | 4 |
| Index Management | 6 |
| Document Management | 7 |
| Search | 6 |
| Cleanup | 7 |

Tests validate:
- Response status codes
- Response success flags
- Response data structure
- Response field values
- Error handling for invalid inputs
- Error handling for not found resources
- Error handling for duplicate resources
