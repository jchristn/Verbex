"""
Verbex Python SDK
A comprehensive SDK for interacting with the Verbex Inverted Index REST API.
"""

import requests
import json
from typing import Optional, List, Dict, Any
from dataclasses import dataclass
from datetime import datetime


def _to_camel_case_keys(obj: Any) -> Any:
    """
    Convert PascalCase keys to camelCase recursively.
    Also adds convenience aliases for common fields.
    """
    if obj is None:
        return None
    if isinstance(obj, list):
        return [_to_camel_case_keys(item) for item in obj]
    if not isinstance(obj, dict):
        return obj

    result = {}
    for key, value in obj.items():
        # Convert first character to lowercase
        camel_key = key[0].lower() + key[1:] if key else key
        result[camel_key] = _to_camel_case_keys(value)

    # Add convenience aliases
    if 'documentId' in result and 'id' not in result:
        result['id'] = str(result['documentId'])

    return result


@dataclass
class ApiResponse:
    """Standard API response wrapper."""
    guid: Optional[str]
    success: bool
    timestamp_utc: Optional[str]
    status_code: int
    error_message: Optional[str]
    data: Optional[Any]
    total_count: Optional[int]
    processing_time_ms: Optional[float]
    raw_response: Dict[str, Any]

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'ApiResponse':
        """Create ApiResponse from dictionary."""
        # Server returns PascalCase - support both
        raw_data = d.get('Data') or d.get('data')
        converted_data = _to_camel_case_keys(raw_data) if raw_data else None
        return ApiResponse(
            guid=d.get('Guid') or d.get('guid'),
            success=d.get('Success') or d.get('success', False),
            timestamp_utc=d.get('TimestampUtc') or d.get('timestampUtc'),
            status_code=d.get('StatusCode') or d.get('statusCode', 0),
            error_message=d.get('ErrorMessage') or d.get('errorMessage'),
            data=converted_data,
            total_count=d.get('TotalCount') or d.get('totalCount'),
            processing_time_ms=d.get('ProcessingTimeMs') or d.get('processingTimeMs'),
            raw_response=d
        )


@dataclass
class IndexInfo:
    """Index information model."""
    id: str
    name: Optional[str]
    description: Optional[str]
    enabled: Optional[bool]
    in_memory: Optional[bool]
    created_utc: Optional[str]
    statistics: Optional[Dict[str, Any]]
    labels: Optional[List[str]]
    tags: Optional[Dict[str, str]]

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'IndexInfo':
        """Create IndexInfo from dictionary."""
        return IndexInfo(
            id=d.get('id', ''),
            name=d.get('name'),
            description=d.get('description'),
            enabled=d.get('enabled'),
            in_memory=d.get('inMemory'),
            created_utc=d.get('createdUtc'),
            statistics=d.get('statistics'),
            labels=d.get('labels'),
            tags=d.get('tags')
        )


@dataclass
class DocumentInfo:
    """Document information model."""
    id: str
    name: Optional[str]
    created_utc: Optional[str]
    labels: Optional[List[str]]
    tags: Optional[Dict[str, str]]

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'DocumentInfo':
        """Create DocumentInfo from dictionary."""
        return DocumentInfo(
            id=d.get('id', ''),
            name=d.get('name'),
            created_utc=d.get('createdUtc'),
            labels=d.get('labels'),
            tags=d.get('tags')
        )


@dataclass
class SearchResult:
    """Search result model."""
    document_id: str
    score: float
    content: Optional[str]

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'SearchResult':
        """Create SearchResult from dictionary."""
        return SearchResult(
            document_id=d.get('documentId', ''),
            score=d.get('score', 0.0),
            content=d.get('content')
        )


@dataclass
class SearchResponse:
    """Search response model."""
    query: str
    results: List[SearchResult]
    total_count: int
    max_results: int

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'SearchResponse':
        """Create SearchResponse from dictionary."""
        results = [SearchResult.from_dict(r) for r in d.get('results', [])]
        return SearchResponse(
            query=d.get('query', ''),
            results=results,
            total_count=d.get('totalCount', 0),
            max_results=d.get('maxResults', 100)
        )


class VerbexError(Exception):
    """Exception raised for Verbex API errors."""
    def __init__(self, message: str, status_code: int = 0, response: Optional[ApiResponse] = None):
        super().__init__(message)
        self.message = message
        self.status_code = status_code
        self.response = response


class VerbexClient:
    """
    Verbex SDK Client for Python.

    Provides methods to interact with all Verbex REST API endpoints.
    """

    def __init__(self, endpoint: str, access_key: str):
        """
        Initialize the Verbex client.

        Args:
            endpoint: The base URL of the Verbex server (e.g., "http://localhost:8080")
            access_key: The bearer token for authentication
        """
        self._endpoint = endpoint.rstrip('/')
        self._access_key = access_key
        self._session = requests.Session()
        self._session.headers.update({
            'Content-Type': 'application/json',
            'Accept': 'application/json'
        })

    def _get_auth_headers(self) -> Dict[str, str]:
        """Get headers with authentication."""
        return {'Authorization': f'Bearer {self._access_key}'}

    def _make_request(
        self,
        method: str,
        path: str,
        data: Optional[Dict[str, Any]] = None,
        require_auth: bool = True
    ) -> ApiResponse:
        """
        Make an HTTP request to the API.

        Args:
            method: HTTP method (GET, POST, DELETE)
            path: API path (will be appended to endpoint)
            data: Request body data (for POST requests)
            require_auth: Whether to include authentication headers

        Returns:
            ApiResponse object with the response data

        Raises:
            VerbexError: If the request fails or returns an error
        """
        url = f"{self._endpoint}{path}"
        headers = self._get_auth_headers() if require_auth else {}

        try:
            if method == 'GET':
                response = self._session.get(url, headers=headers)
            elif method == 'POST':
                response = self._session.post(url, headers=headers, json=data)
            elif method == 'PUT':
                response = self._session.put(url, headers=headers, json=data)
            elif method == 'DELETE':
                response = self._session.delete(url, headers=headers)
            else:
                raise VerbexError(f"Unsupported HTTP method: {method}")

            try:
                response_data = response.json()
            except json.JSONDecodeError:
                response_data = {
                    'success': response.ok,
                    'statusCode': response.status_code,
                    'data': response.text if response.text else None
                }

            api_response = ApiResponse.from_dict(response_data)

            if not api_response.success and api_response.status_code >= 400:
                raise VerbexError(
                    api_response.error_message or f"Request failed with status {api_response.status_code}",
                    api_response.status_code,
                    api_response
                )

            return api_response

        except requests.RequestException as e:
            raise VerbexError(f"Request failed: {str(e)}")

    # ==================== Health Endpoints ====================

    def health_check(self) -> ApiResponse:
        """
        Check server health.

        Returns:
            ApiResponse containing health status, version, and timestamp
        """
        return self._make_request('GET', '/v1.0/health', require_auth=False)

    def root_health_check(self) -> ApiResponse:
        """
        Check server health via root endpoint.

        Returns:
            ApiResponse containing health status
        """
        return self._make_request('GET', '/', require_auth=False)

    # ==================== Authentication Endpoints ====================

    def login(self, username: str, password: str) -> ApiResponse:
        """
        Authenticate and receive a bearer token.

        Args:
            username: The username
            password: The password

        Returns:
            ApiResponse containing the token and username on success
        """
        return self._make_request(
            'POST',
            '/v1.0/auth/login',
            data={'Username': username, 'Password': password},
            require_auth=False
        )

    def validate_token(self) -> ApiResponse:
        """
        Validate the current bearer token.

        Returns:
            ApiResponse containing validation result
        """
        return self._make_request('GET', '/v1.0/auth/validate', require_auth=True)

    # ==================== Index Management Endpoints ====================

    def list_indices(self) -> ApiResponse:
        """
        List all available indices.

        Returns:
            ApiResponse containing list of indices and count
        """
        return self._make_request('GET', '/v1.0/indices')

    def get_indices(self) -> List[IndexInfo]:
        """
        Get all indices as IndexInfo objects.

        Returns:
            List of IndexInfo objects
        """
        response = self.list_indices()
        if response.data and 'indices' in response.data:
            return [IndexInfo.from_dict(idx) for idx in response.data['indices']]
        return []

    def create_index(
        self,
        id: str,
        name: Optional[str] = None,
        description: Optional[str] = None,
        repository_filename: Optional[str] = None,
        in_memory: bool = False,
        storage_mode: str = "MemoryOnly",
        enable_lemmatizer: bool = False,
        enable_stop_word_remover: bool = False,
        min_token_length: int = 0,
        max_token_length: int = 0,
        labels: Optional[List[str]] = None,
        tags: Optional[Dict[str, str]] = None
    ) -> ApiResponse:
        """
        Create a new index.

        Args:
            id: Unique identifier for the index
            name: Display name for the index
            description: Description of the index
            repository_filename: Filename for persistence
            in_memory: Whether to use in-memory storage only
            storage_mode: Storage mode (MemoryOnly, PersistenceOnly, Hybrid)
            enable_lemmatizer: Enable word lemmatization
            enable_stop_word_remover: Enable stop word filtering
            min_token_length: Minimum token length (0 to disable)
            max_token_length: Maximum token length (0 to disable)
            labels: Optional list of labels to associate with the index
            tags: Optional key-value tags to associate with the index

        Returns:
            ApiResponse containing the created index information
        """
        data = {
            'Id': id,
            'Name': name or id,
            'Description': description or '',
            'RepositoryFilename': repository_filename or f'{id}.db',
            'InMemory': in_memory,
            'StorageMode': storage_mode,
            'EnableLemmatizer': enable_lemmatizer,
            'EnableStopWordRemover': enable_stop_word_remover,
            'MinTokenLength': min_token_length,
            'MaxTokenLength': max_token_length
        }
        if labels:
            data['Labels'] = labels
        if tags:
            data['Tags'] = tags
        return self._make_request('POST', '/v1.0/indices', data=data)

    def get_index(self, index_id: str) -> ApiResponse:
        """
        Get detailed information about a specific index.

        Args:
            index_id: The index identifier

        Returns:
            ApiResponse containing index details and statistics
        """
        return self._make_request('GET', f'/v1.0/indices/{index_id}')

    def get_index_info(self, index_id: str) -> IndexInfo:
        """
        Get index information as IndexInfo object.

        Args:
            index_id: The index identifier

        Returns:
            IndexInfo object with index details
        """
        response = self.get_index(index_id)
        return IndexInfo.from_dict(response.data) if response.data else None

    def delete_index(self, index_id: str) -> ApiResponse:
        """
        Delete an index.

        Args:
            index_id: The index identifier

        Returns:
            ApiResponse confirming deletion
        """
        return self._make_request('DELETE', f'/v1.0/indices/{index_id}')

    def update_index_labels(self, index_id: str, labels: List[str]) -> ApiResponse:
        """
        Update labels on an index (full replacement).

        Args:
            index_id: The index identifier
            labels: The new labels to set

        Returns:
            ApiResponse with update confirmation and updated index
        """
        return self._make_request('PUT', f'/v1.0/indices/{index_id}/labels', data={'Labels': labels or []})

    def update_index_tags(self, index_id: str, tags: Dict[str, str]) -> ApiResponse:
        """
        Update tags on an index (full replacement).

        Args:
            index_id: The index identifier
            tags: The new tags to set

        Returns:
            ApiResponse with update confirmation and updated index
        """
        return self._make_request('PUT', f'/v1.0/indices/{index_id}/tags', data={'Tags': tags or {}})

    # ==================== Document Management Endpoints ====================

    def list_documents(self, index_id: str) -> ApiResponse:
        """
        List all documents in an index.

        Args:
            index_id: The index identifier

        Returns:
            ApiResponse containing list of documents and count
        """
        return self._make_request('GET', f'/v1.0/indices/{index_id}/documents')

    def get_documents(self, index_id: str) -> List[DocumentInfo]:
        """
        Get all documents as DocumentInfo objects.

        Args:
            index_id: The index identifier

        Returns:
            List of DocumentInfo objects
        """
        response = self.list_documents(index_id)
        if response.data and 'documents' in response.data:
            return [DocumentInfo.from_dict(doc) for doc in response.data['documents']]
        return []

    def add_document(
        self,
        index_id: str,
        content: str,
        document_id: Optional[str] = None,
        labels: Optional[List[str]] = None,
        tags: Optional[Dict[str, str]] = None
    ) -> ApiResponse:
        """
        Add a document to an index.

        Args:
            index_id: The index identifier
            content: The document content to index
            document_id: Optional document ID (auto-generated if not provided)
            labels: Optional list of labels to associate with the document
            tags: Optional key-value tags to associate with the document

        Returns:
            ApiResponse containing the document ID and confirmation
        """
        data: Dict[str, Any] = {'Content': content}
        if document_id:
            data['Id'] = document_id
        if labels:
            data['Labels'] = labels
        if tags:
            data['Tags'] = tags
        return self._make_request('POST', f'/v1.0/indices/{index_id}/documents', data=data)

    def get_document(self, index_id: str, document_id: str) -> ApiResponse:
        """
        Get a specific document.

        Args:
            index_id: The index identifier
            document_id: The document identifier

        Returns:
            ApiResponse containing document details
        """
        return self._make_request('GET', f'/v1.0/indices/{index_id}/documents/{document_id}')

    def get_document_info(self, index_id: str, document_id: str) -> DocumentInfo:
        """
        Get document as DocumentInfo object.

        Args:
            index_id: The index identifier
            document_id: The document identifier

        Returns:
            DocumentInfo object with document details
        """
        response = self.get_document(index_id, document_id)
        return DocumentInfo.from_dict(response.data) if response.data else None

    def delete_document(self, index_id: str, document_id: str) -> ApiResponse:
        """
        Delete a document from an index.

        Args:
            index_id: The index identifier
            document_id: The document identifier

        Returns:
            ApiResponse confirming deletion
        """
        return self._make_request('DELETE', f'/v1.0/indices/{index_id}/documents/{document_id}')

    def update_document_labels(self, index_id: str, document_id: str, labels: List[str]) -> ApiResponse:
        """
        Update labels on a document (full replacement).

        Args:
            index_id: The index identifier
            document_id: The document identifier
            labels: The new labels to set

        Returns:
            ApiResponse with update confirmation and updated document
        """
        return self._make_request('PUT', f'/v1.0/indices/{index_id}/documents/{document_id}/labels', data={'Labels': labels or []})

    def update_document_tags(self, index_id: str, document_id: str, tags: Dict[str, str]) -> ApiResponse:
        """
        Update tags on a document (full replacement).

        Args:
            index_id: The index identifier
            document_id: The document identifier
            tags: The new tags to set

        Returns:
            ApiResponse with update confirmation and updated document
        """
        return self._make_request('PUT', f'/v1.0/indices/{index_id}/documents/{document_id}/tags', data={'Tags': tags or {}})

    # ==================== Search Endpoint ====================

    def search(
        self,
        index_id: str,
        query: str,
        max_results: int = 100,
        labels: Optional[List[str]] = None,
        tags: Optional[Dict[str, Any]] = None
    ) -> ApiResponse:
        """
        Search documents in an index with optional label and tag filters.

        Args:
            index_id: The index identifier
            query: The search query
            max_results: Maximum number of results to return
            labels: Optional list of labels to filter by (AND logic, case-insensitive)
            tags: Optional dict of tags to filter by (AND logic, exact match)

        Returns:
            ApiResponse containing search results
        """
        data = {
            'Query': query,
            'MaxResults': max_results
        }
        if labels and len(labels) > 0:
            data['Labels'] = labels
        if tags and len(tags) > 0:
            data['Tags'] = tags
        return self._make_request('POST', f'/v1.0/indices/{index_id}/search', data=data)

    def search_documents(
        self,
        index_id: str,
        query: str,
        max_results: int = 100,
        labels: Optional[List[str]] = None,
        tags: Optional[Dict[str, Any]] = None
    ) -> SearchResponse:
        """
        Search documents and return SearchResponse object with optional filters.

        Args:
            index_id: The index identifier
            query: The search query
            max_results: Maximum number of results to return
            labels: Optional list of labels to filter by (AND logic, case-insensitive)
            tags: Optional dict of tags to filter by (AND logic, exact match)

        Returns:
            SearchResponse object with search results
        """
        response = self.search(index_id, query, max_results, labels, tags)
        return SearchResponse.from_dict(response.data) if response.data else None

    def close(self):
        """Close the HTTP session."""
        self._session.close()

    def __enter__(self):
        """Context manager entry."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit."""
        self.close()
        return False
