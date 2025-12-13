# Verbex Python SDK

A comprehensive Python SDK for interacting with the Verbex Inverted Index REST API.

## Installation

```bash
pip install -r requirements.txt
```

## Usage

```python
from verbex_sdk import VerbexClient

# Create client
client = VerbexClient("http://localhost:8080", "verbexadmin")

# Health check
health = client.health_check()
print(f"Server status: {health.data['status']}")

# Create an index
client.create_index(
    id="my-index",
    name="My Index",
    description="A test index",
    in_memory=True
)

# Add documents
client.add_document("my-index", "The quick brown fox jumps over the lazy dog.")
client.add_document("my-index", "Machine learning is transforming industries.")

# Search
results = client.search_documents("my-index", "fox")
for result in results.results:
    print(f"Document: {result.document_id}, Score: {result.score}")

# Cleanup
client.delete_index("my-index")
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
- `health_check()` - Check server health via /v1.0/health
- `root_health_check()` - Check server health via root endpoint

#### Authentication
- `login(username: str, password: str)` - Authenticate and get token
- `validate_token()` - Validate current token

#### Index Management
- `list_indices()` - List all indices
- `get_indices()` - Get all indices as IndexInfo objects
- `create_index(...)` - Create a new index
- `get_index(index_id: str)` - Get index details
- `get_index_info(index_id: str)` - Get index as IndexInfo object
- `delete_index(index_id: str)` - Delete an index

#### Document Management
- `list_documents(index_id: str)` - List all documents
- `get_documents(index_id: str)` - Get all documents as DocumentInfo objects
- `add_document(index_id: str, content: str, document_id: str = None)` - Add document
- `get_document(index_id: str, document_id: str)` - Get document details
- `delete_document(index_id: str, document_id: str)` - Delete document

#### Search
- `search(index_id: str, query: str, max_results: int = 100)` - Search documents
- `search_documents(...)` - Search and return SearchResponse object
