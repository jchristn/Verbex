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
    identifier: str
    tenant_id: Optional[str]
    name: Optional[str]
    description: Optional[str]
    enabled: Optional[bool]
    in_memory: Optional[bool]
    created_utc: Optional[str]
    statistics: Optional[Dict[str, Any]]
    labels: Optional[List[str]]
    tags: Optional[Dict[str, str]]
    custom_metadata: Optional[Any] = None

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'IndexInfo':
        """Create IndexInfo from dictionary."""
        return IndexInfo(
            identifier=d.get('identifier', ''),
            tenant_id=d.get('tenantId'),
            name=d.get('name'),
            description=d.get('description'),
            enabled=d.get('enabled'),
            in_memory=d.get('inMemory'),
            created_utc=d.get('createdUtc'),
            statistics=d.get('statistics'),
            labels=d.get('labels'),
            tags=d.get('tags'),
            custom_metadata=d.get('customMetadata')
        )


@dataclass
class DocumentInfo:
    """Document information model."""
    id: str
    name: Optional[str]
    created_utc: Optional[str]
    labels: Optional[List[str]]
    tags: Optional[Dict[str, str]]
    custom_metadata: Optional[Any] = None

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'DocumentInfo':
        """Create DocumentInfo from dictionary."""
        return DocumentInfo(
            id=d.get('id', ''),
            name=d.get('name'),
            created_utc=d.get('createdUtc'),
            labels=d.get('labels'),
            tags=d.get('tags'),
            custom_metadata=d.get('customMetadata')
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


@dataclass
class TenantInfo:
    """Tenant information model."""
    identifier: str
    name: Optional[str]
    active: bool
    created_utc: Optional[str]
    labels: Optional[List[str]]
    tags: Optional[Dict[str, str]]

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'TenantInfo':
        """Create TenantInfo from dictionary."""
        return TenantInfo(
            identifier=d.get('identifier', ''),
            name=d.get('name'),
            active=d.get('active', False),
            created_utc=d.get('createdUtc'),
            labels=d.get('labels'),
            tags=d.get('tags')
        )


@dataclass
class UserInfo:
    """User information model."""
    identifier: str
    tenant_id: str
    email: str
    first_name: Optional[str]
    last_name: Optional[str]
    is_admin: bool
    active: bool
    created_utc: Optional[str]
    labels: Optional[List[str]]
    tags: Optional[Dict[str, str]]

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'UserInfo':
        """Create UserInfo from dictionary."""
        return UserInfo(
            identifier=d.get('identifier', ''),
            tenant_id=d.get('tenantId', ''),
            email=d.get('email', ''),
            first_name=d.get('firstName'),
            last_name=d.get('lastName'),
            is_admin=d.get('isAdmin', False),
            active=d.get('active', False),
            created_utc=d.get('createdUtc'),
            labels=d.get('labels'),
            tags=d.get('tags')
        )


@dataclass
class CredentialInfo:
    """Credential information model."""
    identifier: str
    tenant_id: str
    name: Optional[str]
    bearer_token: Optional[str]
    active: bool
    created_utc: Optional[str]
    labels: Optional[List[str]]
    tags: Optional[Dict[str, str]]

    @staticmethod
    def from_dict(d: Dict[str, Any]) -> 'CredentialInfo':
        """Create CredentialInfo from dictionary."""
        return CredentialInfo(
            identifier=d.get('identifier', ''),
            tenant_id=d.get('tenantId', ''),
            name=d.get('name'),
            bearer_token=d.get('bearerToken'),
            active=d.get('active', False),
            created_utc=d.get('createdUtc'),
            labels=d.get('labels'),
            tags=d.get('tags')
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
            elif method == 'HEAD':
                response = self._session.head(url, headers=headers)
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
        name: str,
        description: Optional[str] = None,
        in_memory: bool = False,
        enable_lemmatizer: bool = False,
        enable_stop_word_remover: bool = False,
        min_token_length: int = 0,
        max_token_length: int = 0,
        labels: Optional[List[str]] = None,
        tags: Optional[Dict[str, str]] = None,
        custom_metadata: Optional[Any] = None
    ) -> ApiResponse:
        """
        Create a new index.

        Args:
            name: Display name for the index (required)
            description: Description of the index
            in_memory: Whether to use in-memory storage only
            enable_lemmatizer: Enable word lemmatization
            enable_stop_word_remover: Enable stop word filtering
            min_token_length: Minimum token length (0 to disable)
            max_token_length: Maximum token length (0 to disable)
            labels: Optional list of labels to associate with the index
            tags: Optional key-value tags to associate with the index
            custom_metadata: Optional custom metadata to associate with the index

        Returns:
            ApiResponse containing the created index information
        """
        data: Dict[str, Any] = {
            'Name': name,
            'InMemory': in_memory,
            'EnableLemmatizer': enable_lemmatizer,
            'EnableStopWordRemover': enable_stop_word_remover,
            'MinTokenLength': min_token_length,
            'MaxTokenLength': max_token_length
        }
        if description:
            data['Description'] = description
        if labels:
            data['Labels'] = labels
        if tags:
            data['Tags'] = tags
        if custom_metadata is not None:
            data['CustomMetadata'] = custom_metadata
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

    def index_exists(self, index_id: str) -> bool:
        """
        Check if an index exists.

        Args:
            index_id: The index identifier

        Returns:
            True if the index exists, False otherwise
        """
        try:
            self._make_request('HEAD', f'/v1.0/indices/{index_id}')
            return True
        except VerbexError as e:
            if e.status_code == 404:
                return False
            raise

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

    def update_index_custom_metadata(self, index_id: str, custom_metadata: Any) -> IndexInfo:
        """
        Update custom metadata for an index.

        Args:
            index_id: The index identifier
            custom_metadata: The custom metadata to set

        Returns:
            IndexInfo with updated index details
        """
        response = self._make_request('PUT', f'/v1.0/indices/{index_id}/customMetadata', data={'customMetadata': custom_metadata})
        return IndexInfo.from_dict(response.data) if response.data else None

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
        tags: Optional[Dict[str, str]] = None,
        custom_metadata: Optional[Any] = None
    ) -> ApiResponse:
        """
        Add a document to an index.

        Args:
            index_id: The index identifier
            content: The document content to index
            document_id: Optional document ID (auto-generated if not provided)
            labels: Optional list of labels to associate with the document
            tags: Optional key-value tags to associate with the document
            custom_metadata: Optional custom metadata to associate with the document

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
        if custom_metadata is not None:
            data['CustomMetadata'] = custom_metadata
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

    def document_exists(self, index_id: str, document_id: str) -> bool:
        """
        Check if a document exists in an index.

        Args:
            index_id: The index identifier
            document_id: The document identifier

        Returns:
            True if the document exists, False otherwise
        """
        try:
            self._make_request('HEAD', f'/v1.0/indices/{index_id}/documents/{document_id}')
            return True
        except VerbexError as e:
            if e.status_code == 404:
                return False
            raise

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

    def update_document_custom_metadata(self, index_id: str, document_id: str, custom_metadata: Any) -> DocumentInfo:
        """
        Update custom metadata for a document.

        Args:
            index_id: The index identifier
            document_id: The document identifier
            custom_metadata: The custom metadata to set

        Returns:
            DocumentInfo with updated document details
        """
        response = self._make_request('PUT', f'/v1.0/indices/{index_id}/documents/{document_id}/customMetadata', data={'customMetadata': custom_metadata})
        return DocumentInfo.from_dict(response.data) if response.data else None

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

    # ==================== Admin - Tenant Management Endpoints ====================

    def list_tenants(self) -> ApiResponse:
        """
        List all tenants.

        Returns:
            ApiResponse containing list of tenants
        """
        return self._make_request('GET', '/v1.0/admin/tenants')

    def get_tenants(self) -> List[TenantInfo]:
        """
        Get all tenants as TenantInfo objects.

        Returns:
            List of TenantInfo objects
        """
        response = self.list_tenants()
        if response.data and 'tenants' in response.data:
            return [TenantInfo.from_dict(t) for t in response.data['tenants']]
        return []

    def get_tenant(self, tenant_id: str) -> ApiResponse:
        """
        Get a specific tenant.

        Args:
            tenant_id: The tenant identifier

        Returns:
            ApiResponse containing tenant details
        """
        return self._make_request('GET', f'/v1.0/admin/tenants/{tenant_id}')

    def create_tenant(
        self,
        name: str,
        description: Optional[str] = None
    ) -> ApiResponse:
        """
        Create a new tenant.

        Args:
            name: Tenant name
            description: Optional description

        Returns:
            ApiResponse containing created tenant
        """
        data: Dict[str, Any] = {'name': name}
        if description:
            data['description'] = description
        return self._make_request('POST', '/v1.0/admin/tenants', data=data)

    def delete_tenant(self, tenant_id: str) -> ApiResponse:
        """
        Delete a tenant.

        Args:
            tenant_id: The tenant identifier

        Returns:
            ApiResponse confirming deletion
        """
        return self._make_request('DELETE', f'/v1.0/admin/tenants/{tenant_id}')

    def update_tenant_labels(self, tenant_id: str, labels: List[str]) -> ApiResponse:
        """
        Update labels on a tenant (full replacement).

        Args:
            tenant_id: The tenant identifier
            labels: The new labels to set

        Returns:
            ApiResponse with update confirmation and updated tenant
        """
        return self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/labels', data={'Labels': labels or []})

    def update_tenant_tags(self, tenant_id: str, tags: Dict[str, str]) -> ApiResponse:
        """
        Update tags on a tenant (full replacement).

        Args:
            tenant_id: The tenant identifier
            tags: The new tags to set

        Returns:
            ApiResponse with update confirmation and updated tenant
        """
        return self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/tags', data={'Tags': tags or {}})

    # ==================== Admin - User Management Endpoints ====================

    def list_users(self, tenant_id: str) -> ApiResponse:
        """
        List all users in a tenant.

        Args:
            tenant_id: The tenant identifier

        Returns:
            ApiResponse containing list of users
        """
        return self._make_request('GET', f'/v1.0/admin/tenants/{tenant_id}/users')

    def get_users(self, tenant_id: str) -> List[UserInfo]:
        """
        Get all users in a tenant as UserInfo objects.

        Args:
            tenant_id: The tenant identifier

        Returns:
            List of UserInfo objects
        """
        response = self.list_users(tenant_id)
        if response.data and 'users' in response.data:
            return [UserInfo.from_dict(u) for u in response.data['users']]
        return []

    def get_user(self, tenant_id: str, user_id: str) -> ApiResponse:
        """
        Get a specific user.

        Args:
            tenant_id: The tenant identifier
            user_id: The user identifier

        Returns:
            ApiResponse containing user details
        """
        return self._make_request('GET', f'/v1.0/admin/tenants/{tenant_id}/users/{user_id}')

    def create_user(
        self,
        tenant_id: str,
        email: str,
        password: str,
        first_name: Optional[str] = None,
        last_name: Optional[str] = None,
        is_admin: bool = False
    ) -> ApiResponse:
        """
        Create a new user in a tenant.

        Args:
            tenant_id: The tenant identifier
            email: User email
            password: User password
            first_name: Optional first name
            last_name: Optional last name
            is_admin: Whether user is tenant admin

        Returns:
            ApiResponse containing created user
        """
        data: Dict[str, Any] = {
            'email': email,
            'password': password
        }
        if first_name:
            data['firstName'] = first_name
        if last_name:
            data['lastName'] = last_name
        if is_admin:
            data['isAdmin'] = is_admin
        return self._make_request('POST', f'/v1.0/admin/tenants/{tenant_id}/users', data=data)

    def delete_user(self, tenant_id: str, user_id: str) -> ApiResponse:
        """
        Delete a user.

        Args:
            tenant_id: The tenant identifier
            user_id: The user identifier

        Returns:
            ApiResponse confirming deletion
        """
        return self._make_request('DELETE', f'/v1.0/admin/tenants/{tenant_id}/users/{user_id}')

    def update_user_labels(self, tenant_id: str, user_id: str, labels: List[str]) -> ApiResponse:
        """
        Update labels on a user (full replacement).

        Args:
            tenant_id: The tenant identifier
            user_id: The user identifier
            labels: The new labels to set

        Returns:
            ApiResponse with update confirmation and updated user
        """
        return self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/users/{user_id}/labels', data={'Labels': labels or []})

    def update_user_tags(self, tenant_id: str, user_id: str, tags: Dict[str, str]) -> ApiResponse:
        """
        Update tags on a user (full replacement).

        Args:
            tenant_id: The tenant identifier
            user_id: The user identifier
            tags: The new tags to set

        Returns:
            ApiResponse with update confirmation and updated user
        """
        return self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/users/{user_id}/tags', data={'Tags': tags or {}})

    # ==================== Admin - Credential Management Endpoints ====================

    def list_credentials(self, tenant_id: str) -> ApiResponse:
        """
        List all credentials in a tenant.

        Args:
            tenant_id: The tenant identifier

        Returns:
            ApiResponse containing list of credentials
        """
        return self._make_request('GET', f'/v1.0/admin/tenants/{tenant_id}/credentials')

    def get_credentials(self, tenant_id: str) -> List[CredentialInfo]:
        """
        Get all credentials in a tenant as CredentialInfo objects.

        Args:
            tenant_id: The tenant identifier

        Returns:
            List of CredentialInfo objects
        """
        response = self.list_credentials(tenant_id)
        if response.data and 'credentials' in response.data:
            return [CredentialInfo.from_dict(c) for c in response.data['credentials']]
        return []

    def get_credential(self, tenant_id: str, credential_id: str) -> ApiResponse:
        """
        Get a specific credential.

        Args:
            tenant_id: The tenant identifier
            credential_id: The credential identifier

        Returns:
            ApiResponse containing credential details
        """
        return self._make_request('GET', f'/v1.0/admin/tenants/{tenant_id}/credentials/{credential_id}')

    def create_credential(
        self,
        tenant_id: str,
        description: Optional[str] = None
    ) -> ApiResponse:
        """
        Create a new credential (API key) in a tenant.

        Args:
            tenant_id: The tenant identifier
            description: Optional description

        Returns:
            ApiResponse containing created credential (includes bearer token)
        """
        data: Dict[str, Any] = {}
        if description:
            data['description'] = description
        return self._make_request('POST', f'/v1.0/admin/tenants/{tenant_id}/credentials', data=data)

    def delete_credential(self, tenant_id: str, credential_id: str) -> ApiResponse:
        """
        Delete a credential.

        Args:
            tenant_id: The tenant identifier
            credential_id: The credential identifier

        Returns:
            ApiResponse confirming deletion
        """
        return self._make_request('DELETE', f'/v1.0/admin/tenants/{tenant_id}/credentials/{credential_id}')

    def update_credential_labels(self, tenant_id: str, credential_id: str, labels: List[str]) -> ApiResponse:
        """
        Update labels on a credential (full replacement).

        Args:
            tenant_id: The tenant identifier
            credential_id: The credential identifier
            labels: The new labels to set

        Returns:
            ApiResponse with update confirmation and updated credential
        """
        return self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/credentials/{credential_id}/labels', data={'Labels': labels or []})

    def update_credential_tags(self, tenant_id: str, credential_id: str, tags: Dict[str, str]) -> ApiResponse:
        """
        Update tags on a credential (full replacement).

        Args:
            tenant_id: The tenant identifier
            credential_id: The credential identifier
            tags: The new tags to set

        Returns:
            ApiResponse with update confirmation and updated credential
        """
        return self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/credentials/{credential_id}/tags', data={'Tags': tags or {}})

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
