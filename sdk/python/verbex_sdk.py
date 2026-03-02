"""
Verbex Python SDK

A comprehensive SDK for interacting with the Verbex Inverted Index REST API.
All methods return domain objects directly rather than wrapped responses.
"""

import requests
import json
from typing import Optional, List, Dict, Any
from dataclasses import dataclass, field
from enum import Enum


class AuthenticationResult(Enum):
    """Enumeration of authentication result values."""
    SUCCESS = "Success"
    NOT_AUTHENTICATED = "NotAuthenticated"
    MISSING_CREDENTIALS = "MissingCredentials"
    NOT_FOUND = "NotFound"
    INACTIVE = "Inactive"
    INVALID_CREDENTIALS = "InvalidCredentials"
    TENANT_NOT_FOUND = "TenantNotFound"
    TENANT_INACTIVE = "TenantInactive"
    TENANT_ACCESS_DENIED = "TenantAccessDenied"


class AuthorizationResult(Enum):
    """Enumeration of authorization result values."""
    AUTHORIZED = "Authorized"
    UNAUTHORIZED = "Unauthorized"
    INSUFFICIENT_PRIVILEGES = "InsufficientPrivileges"
    RESOURCE_NOT_FOUND = "ResourceNotFound"
    ACCESS_DENIED = "AccessDenied"


class VerbexError(Exception):
    """Exception raised for Verbex API errors."""
    def __init__(self, message: str, status_code: int = 0, response: Optional[Dict[str, Any]] = None):
        super().__init__(message)
        self.message = message
        self.status_code = status_code
        self.response = response


@dataclass
class LoginResult:
    """Result of a login attempt."""
    success: bool = False
    authentication_result: AuthenticationResult = AuthenticationResult.NOT_AUTHENTICATED
    authorization_result: AuthorizationResult = AuthorizationResult.UNAUTHORIZED
    error_message: Optional[str] = None
    token: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    email: Optional[str] = None
    is_admin: bool = False
    is_global_admin: bool = False

    @staticmethod
    def successful(
        token: str,
        tenant_id: Optional[str] = None,
        user_id: Optional[str] = None,
        email: Optional[str] = None,
        is_admin: bool = False,
        is_global_admin: bool = False
    ) -> 'LoginResult':
        """Create a successful login result."""
        return LoginResult(
            success=True,
            authentication_result=AuthenticationResult.SUCCESS,
            authorization_result=AuthorizationResult.AUTHORIZED,
            token=token,
            tenant_id=tenant_id,
            user_id=user_id,
            email=email,
            is_admin=is_admin,
            is_global_admin=is_global_admin
        )

    @staticmethod
    def failed(
        authentication_result: AuthenticationResult,
        authorization_result: AuthorizationResult,
        error_message: Optional[str] = None
    ) -> 'LoginResult':
        """Create a failed login result."""
        return LoginResult(
            success=False,
            authentication_result=authentication_result,
            authorization_result=authorization_result,
            error_message=error_message
        )


def _to_snake_case(name: str) -> str:
    """Convert PascalCase or camelCase to snake_case."""
    result = []
    for i, char in enumerate(name):
        if char.isupper() and i > 0:
            result.append('_')
        result.append(char.lower())
    return ''.join(result)


def _convert_keys_to_snake_case(obj: Any) -> Any:
    """Recursively convert dictionary keys from PascalCase/camelCase to snake_case."""
    if obj is None:
        return None
    if isinstance(obj, list):
        return [_convert_keys_to_snake_case(item) for item in obj]
    if isinstance(obj, dict):
        return {_to_snake_case(k): _convert_keys_to_snake_case(v) for k, v in obj.items()}
    return obj


@dataclass
class CacheConfiguration:
    """Cache configuration for an index."""
    enabled: bool = False
    enable_term_cache: bool = True
    term_cache_capacity: int = 10000
    term_cache_evict_count: int = 100
    term_cache_ttl_seconds: int = 300
    term_cache_sliding_expiration: bool = True
    enable_document_cache: bool = True
    document_cache_capacity: int = 5000
    document_cache_evict_count: int = 50
    document_cache_ttl_seconds: int = 600
    document_cache_sliding_expiration: bool = True
    enable_statistics_cache: bool = True
    statistics_cache_ttl_seconds: int = 60

    @staticmethod
    def create_enabled() -> 'CacheConfiguration':
        """Create a CacheConfiguration with caching enabled and default settings."""
        return CacheConfiguration(enabled=True)


@dataclass
class CacheStats:
    """Statistics for a single cache instance."""
    enabled: bool = False
    hit_count: int = 0
    miss_count: int = 0
    hit_rate: float = 0.0
    current_count: int = 0
    capacity: int = 0
    eviction_count: int = 0
    expired_count: int = 0


@dataclass
class VerbexCacheStatistics:
    """Aggregate cache statistics for an index."""
    enabled: bool = False
    term_cache: Optional[CacheStats] = None
    document_cache: Optional[CacheStats] = None
    statistics_cache: Optional[CacheStats] = None
    cached_document_count: Optional[int] = None


@dataclass
class HealthData:
    """Health check response data."""
    status: Optional[str] = None
    version: Optional[str] = None
    timestamp: Optional[str] = None


@dataclass
class ValidationData:
    """Token validation response data."""
    valid: bool = False
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    email: Optional[str] = None
    is_admin: bool = False
    is_global_admin: bool = False


@dataclass
class IndexStatistics:
    """Index statistics."""
    document_count: int = 0
    term_count: int = 0
    posting_count: int = 0
    average_document_length: float = 0.0
    total_document_size: int = 0
    total_term_occurrences: int = 0
    average_terms_per_document: float = 0.0
    average_document_frequency: float = 0.0
    max_document_frequency: int = 0
    min_document_length: int = 0
    max_document_length: int = 0
    cache_statistics: Optional[VerbexCacheStatistics] = None
    generated_at: Optional[str] = None
    total_term_frequency: int = 0  # Legacy, kept for backwards compatibility


@dataclass
class IndexInfo:
    """Index information model."""
    identifier: str = ""
    tenant_id: Optional[str] = None
    name: Optional[str] = None
    description: Optional[str] = None
    enabled: Optional[bool] = None
    in_memory: Optional[bool] = None
    created_utc: Optional[str] = None
    statistics: Optional[IndexStatistics] = None
    custom_metadata: Optional[Any] = None
    labels: Optional[List[str]] = None
    tags: Optional[Dict[str, str]] = None
    cache_configuration: Optional[CacheConfiguration] = None

    @property
    def id(self) -> str:
        """Alias for identifier."""
        return self.identifier


@dataclass
class DocumentInfo:
    """Document information model."""
    document_id: str = ""
    document_path: Optional[str] = None
    original_file_name: Optional[str] = None
    document_length: int = 0
    indexed_date: Optional[str] = None
    last_modified: Optional[str] = None
    content_sha256: Optional[str] = None
    terms: Optional[List[str]] = None
    is_deleted: bool = False
    custom_metadata: Optional[Any] = None
    labels: Optional[List[str]] = None
    tags: Optional[Dict[str, str]] = None
    indexing_runtime_ms: Optional[float] = None

    @property
    def id(self) -> str:
        """Alias for document_id."""
        return self.document_id


@dataclass
class AddDocumentData:
    """Add document response data."""
    document_id: str = ""
    message: Optional[str] = None


@dataclass
class BatchDocumentsResult:
    """Result of batch document retrieval operation."""
    documents: List[DocumentInfo] = field(default_factory=list)
    not_found: List[str] = field(default_factory=list)
    count: int = 0
    requested_count: int = 0


@dataclass
class BatchDeleteResult:
    """Result of batch document deletion operation."""
    deleted: List[str] = field(default_factory=list)
    not_found: List[str] = field(default_factory=list)
    deleted_count: int = 0
    not_found_count: int = 0
    requested_count: int = 0


@dataclass
class BatchAddDocumentItem:
    """A document to add in a batch operation."""
    name: str = ""
    content: str = ""
    id: Optional[str] = None
    labels: Optional[List[str]] = None
    tags: Optional[Dict[str, str]] = None
    custom_metadata: Optional[Any] = None


@dataclass
class BatchAddResultItem:
    """Result item for a single document in a batch add operation."""
    document_id: str = ""
    name: str = ""
    success: bool = False
    error_message: Optional[str] = None


@dataclass
class BatchAddResult:
    """Result of batch document add operation."""
    added: List[BatchAddResultItem] = field(default_factory=list)
    failed: List[BatchAddResultItem] = field(default_factory=list)
    added_count: int = 0
    failed_count: int = 0
    requested_count: int = 0


@dataclass
class BatchExistenceResult:
    """Result of batch existence check operation."""
    exists: List[str] = field(default_factory=list)
    not_found: List[str] = field(default_factory=list)
    exists_count: int = 0
    not_found_count: int = 0
    requested_count: int = 0


@dataclass
class SearchResult:
    """Search result model."""
    document_id: str = ""
    score: float = 0.0
    content: Optional[str] = None
    total_term_matches: int = 0
    term_scores: Optional[Dict[str, float]] = None
    term_frequencies: Optional[Dict[str, int]] = None


@dataclass
class SearchData:
    """Search response model."""
    query: str = ""
    results: List[SearchResult] = field(default_factory=list)
    total_count: int = 0
    max_results: int = 100
    search_time: float = 0.0


@dataclass
class TenantInfo:
    """Tenant information model."""
    identifier: str = ""
    name: Optional[str] = None
    active: bool = False
    created_utc: Optional[str] = None
    labels: Optional[List[str]] = None
    tags: Optional[Dict[str, str]] = None


@dataclass
class UserInfo:
    """User information model."""
    identifier: str = ""
    tenant_id: str = ""
    email: str = ""
    first_name: Optional[str] = None
    last_name: Optional[str] = None
    is_admin: bool = False
    active: bool = False
    created_utc: Optional[str] = None
    labels: Optional[List[str]] = None
    tags: Optional[Dict[str, str]] = None


@dataclass
class CredentialInfo:
    """Credential information model."""
    identifier: str = ""
    tenant_id: str = ""
    name: Optional[str] = None
    bearer_token: Optional[str] = None
    active: bool = False
    created_utc: Optional[str] = None
    labels: Optional[List[str]] = None
    tags: Optional[Dict[str, str]] = None


class EnumerationOrder(Enum):
    """Specifies the ordering for enumeration results."""
    CREATED_ASCENDING = "CreatedAscending"
    CREATED_DESCENDING = "CreatedDescending"


@dataclass
class EnumerationOptions:
    """Options for paginated enumeration requests."""
    max_results: int = 100
    skip: int = 0
    continuation_token: Optional[str] = None
    ordering: EnumerationOrder = EnumerationOrder.CREATED_DESCENDING
    labels: Optional[List[str]] = None
    tags: Optional[Dict[str, str]] = None

    def to_query_params(self) -> Dict[str, str]:
        """Convert to query parameters dictionary."""
        params = {}
        if self.max_results != 100:
            params['maxResults'] = str(self.max_results)
        if self.skip > 0:
            params['skip'] = str(self.skip)
        if self.continuation_token:
            params['continuationToken'] = self.continuation_token
        if self.ordering != EnumerationOrder.CREATED_DESCENDING:
            params['ordering'] = self.ordering.value
        if self.labels:
            params['labels'] = ','.join(self.labels)
        if self.tags:
            for key, value in self.tags.items():
                params[f'tag.{key}'] = value
        return params


@dataclass
class EnumerationResult:
    """Result container for paginated enumeration of collections."""
    success: bool = True
    timestamp: Optional[str] = None
    max_results: int = 100
    skip: int = 0
    iterations_required: int = 1
    continuation_token: Optional[str] = None
    end_of_results: bool = False
    total_records: int = 0
    records_remaining: int = 0
    objects: List[Any] = field(default_factory=list)

    def __iter__(self):
        """Iterate over the objects in this result."""
        return iter(self.objects)

    def __len__(self) -> int:
        """Return the number of objects in this result page."""
        return len(self.objects)

    @property
    def has_more(self) -> bool:
        """Returns True if there are more records available to fetch."""
        return not self.end_of_results and self.continuation_token is not None

    def get_next_page_options(self) -> Optional[EnumerationOptions]:
        """Creates EnumerationOptions to fetch the next page."""
        if self.end_of_results or not self.continuation_token:
            return None
        return EnumerationOptions(
            max_results=self.max_results,
            continuation_token=self.continuation_token
        )


class VerbexClient:
    """
    Verbex SDK Client for Python.

    Provides methods to interact with all Verbex REST API endpoints.
    All methods return domain objects directly rather than wrapped responses.
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
    ) -> Any:
        """
        Make an HTTP request to the API and return the data directly.

        Args:
            method: HTTP method (GET, POST, PUT, DELETE)
            path: API path (will be appended to endpoint)
            data: Request body data (for POST/PUT requests)
            require_auth: Whether to include authentication headers

        Returns:
            Response data (unwrapped from ApiResponse), converted to snake_case keys

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
                    'Success': response.ok,
                    'StatusCode': response.status_code,
                    'Data': None
                }

            # Handle both PascalCase and camelCase from server
            success = response_data.get('Success') or response_data.get('success', False)
            status_code = response_data.get('StatusCode') or response_data.get('statusCode', response.status_code)
            error_message = response_data.get('ErrorMessage') or response_data.get('errorMessage')
            raw_data = response_data.get('Data') or response_data.get('data')

            if not success and status_code >= 400:
                raise VerbexError(
                    error_message or f"Request failed with status {status_code}",
                    status_code,
                    response_data
                )

            # Return the data directly, converting keys to snake_case
            return _convert_keys_to_snake_case(raw_data)

        except requests.RequestException as e:
            raise VerbexError(f"Request failed: {str(e)}")

    def _make_head_request(self, path: str, require_auth: bool = True) -> bool:
        """
        Make an HTTP HEAD request to check existence.

        Args:
            path: API path
            require_auth: Whether to include auth headers

        Returns:
            True if resource exists, False otherwise
        """
        url = f"{self._endpoint}{path}"
        headers = self._get_auth_headers() if require_auth else {}

        try:
            response = self._session.head(url, headers=headers)
            return response.ok
        except requests.RequestException as e:
            raise VerbexError(f"Request failed: {str(e)}")

    # ==================== Health Endpoints ====================

    def health_check(self) -> HealthData:
        """
        Check server health.

        Returns:
            HealthData with status information
        """
        data = self._make_request('GET', '/v1.0/health', require_auth=False)
        return HealthData(
            status=data.get('status') if data else None,
            version=data.get('version') if data else None,
            timestamp=data.get('timestamp') if data else None
        )

    def root_health_check(self) -> HealthData:
        """
        Check server health via root endpoint.

        Returns:
            HealthData with status information
        """
        data = self._make_request('GET', '/', require_auth=False)
        return HealthData(
            status=data.get('status') if data else None,
            version=data.get('version') if data else None,
            timestamp=data.get('timestamp') if data else None
        )

    # ==================== Authentication Endpoints ====================

    def login_with_credentials(self, tenant_id: str, email: str, password: str) -> LoginResult:
        """
        Authenticate with tenant ID, email, and password.

        Args:
            tenant_id: The tenant identifier
            email: The user's email address
            password: The user's password

        Returns:
            LoginResult indicating success or failure with context
        """
        if not tenant_id:
            raise ValueError("tenant_id is required")
        if not email:
            raise ValueError("email is required")
        if not password:
            raise ValueError("password is required")

        try:
            data = self._make_request(
                'POST',
                '/v1.0/auth/login',
                data={'TenantId': tenant_id, 'Username': email, 'Password': password},
                require_auth=False
            )

            if data and data.get('token'):
                return LoginResult.successful(
                    token=data['token'],
                    tenant_id=tenant_id,
                    email=email,
                    is_admin=data.get('is_admin', False),
                    is_global_admin=data.get('is_global_admin', False)
                )

            return LoginResult.failed(
                AuthenticationResult.INVALID_CREDENTIALS,
                AuthorizationResult.UNAUTHORIZED,
                "Login failed"
            )
        except VerbexError as e:
            if e.status_code == 401:
                auth_result = AuthenticationResult.INVALID_CREDENTIALS
                authz_result = AuthorizationResult.UNAUTHORIZED
            elif e.status_code == 403:
                auth_result = AuthenticationResult.TENANT_ACCESS_DENIED
                authz_result = AuthorizationResult.ACCESS_DENIED
            elif e.status_code == 404:
                auth_result = AuthenticationResult.NOT_FOUND
                authz_result = AuthorizationResult.RESOURCE_NOT_FOUND
            else:
                auth_result = AuthenticationResult.NOT_AUTHENTICATED
                authz_result = AuthorizationResult.UNAUTHORIZED

            return LoginResult.failed(auth_result, authz_result, str(e))

    def login_with_token(self, bearer_token: str) -> LoginResult:
        """
        Authenticate with an existing bearer token by validating it against the server.

        Args:
            bearer_token: The bearer token to validate and use

        Returns:
            LoginResult indicating success or failure with context
        """
        if not bearer_token:
            raise ValueError("bearer_token is required")

        original_access_key = self._access_key

        try:
            self._access_key = bearer_token
            data = self._make_request('GET', '/v1.0/auth/validate', require_auth=True)

            if data and data.get('valid'):
                return LoginResult.successful(
                    token=bearer_token,
                    tenant_id=data.get('tenant_id'),
                    user_id=data.get('user_id'),
                    email=data.get('email'),
                    is_admin=data.get('is_admin', False),
                    is_global_admin=data.get('is_global_admin', False)
                )

            self._access_key = original_access_key
            return LoginResult.failed(
                AuthenticationResult.INVALID_CREDENTIALS,
                AuthorizationResult.UNAUTHORIZED,
                "Bearer token validation failed"
            )
        except VerbexError as e:
            self._access_key = original_access_key

            if e.status_code == 401:
                auth_result = AuthenticationResult.INVALID_CREDENTIALS
            elif e.status_code == 403:
                auth_result = AuthenticationResult.TENANT_ACCESS_DENIED
            else:
                auth_result = AuthenticationResult.NOT_AUTHENTICATED

            return LoginResult.failed(auth_result, AuthorizationResult.UNAUTHORIZED, str(e))

    def validate_token(self) -> ValidationData:
        """
        Validate the current bearer token.

        Returns:
            ValidationData with validation result
        """
        data = self._make_request('GET', '/v1.0/auth/validate', require_auth=True)
        return ValidationData(
            valid=data.get('valid', False) if data else False,
            tenant_id=data.get('tenant_id') if data else None,
            user_id=data.get('user_id') if data else None,
            email=data.get('email') if data else None,
            is_admin=data.get('is_admin', False) if data else False,
            is_global_admin=data.get('is_global_admin', False) if data else False
        )

    # ==================== Index Management Endpoints ====================

    def list_indices(self, options: Optional[EnumerationOptions] = None) -> EnumerationResult:
        """
        List available indices with pagination support.

        Args:
            options: Optional pagination options

        Returns:
            EnumerationResult containing IndexInfo objects and pagination information
        """
        path = '/v1.0/indices'
        if options:
            params = options.to_query_params()
            if params:
                path += '?' + '&'.join(f'{k}={v}' for k, v in params.items())

        data = self._make_request('GET', path)
        return self._parse_enumeration_result(data, self._parse_index_info)

    def list_all_indices(self) -> List[IndexInfo]:
        """
        List all indices by iterating through all pages.

        Returns:
            List of all IndexInfo objects
        """
        all_items = []
        options = EnumerationOptions(max_results=1000)

        while True:
            result = self.list_indices(options)
            all_items.extend(result.objects)

            if result.end_of_results or not result.continuation_token:
                break

            options = result.get_next_page_options()

        return all_items

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
        custom_metadata: Optional[Any] = None,
        cache_configuration: Optional[CacheConfiguration] = None,
        tenant_id: Optional[str] = None
    ) -> IndexInfo:
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
            cache_configuration: Optional cache configuration for the index
            tenant_id: Tenant ID (required for global admin users, optional for tenant users)

        Returns:
            Created IndexInfo
        """
        request_data: Dict[str, Any] = {
            'Name': name,
            'InMemory': in_memory,
            'EnableLemmatizer': enable_lemmatizer,
            'EnableStopWordRemover': enable_stop_word_remover,
            'MinTokenLength': min_token_length,
            'MaxTokenLength': max_token_length
        }
        if tenant_id:
            request_data['TenantId'] = tenant_id
        if description:
            request_data['Description'] = description
        if labels:
            request_data['Labels'] = labels
        if tags:
            request_data['Tags'] = tags
        if custom_metadata is not None:
            request_data['CustomMetadata'] = custom_metadata
        if cache_configuration is not None:
            request_data['CacheConfiguration'] = {
                'Enabled': cache_configuration.enabled,
                'EnableTermCache': cache_configuration.enable_term_cache,
                'TermCacheCapacity': cache_configuration.term_cache_capacity,
                'TermCacheEvictCount': cache_configuration.term_cache_evict_count,
                'TermCacheTtlSeconds': cache_configuration.term_cache_ttl_seconds,
                'TermCacheSlidingExpiration': cache_configuration.term_cache_sliding_expiration,
                'EnableDocumentCache': cache_configuration.enable_document_cache,
                'DocumentCacheCapacity': cache_configuration.document_cache_capacity,
                'DocumentCacheEvictCount': cache_configuration.document_cache_evict_count,
                'DocumentCacheTtlSeconds': cache_configuration.document_cache_ttl_seconds,
                'DocumentCacheSlidingExpiration': cache_configuration.document_cache_sliding_expiration,
                'EnableStatisticsCache': cache_configuration.enable_statistics_cache,
                'StatisticsCacheTtlSeconds': cache_configuration.statistics_cache_ttl_seconds
            }

        data = self._make_request('POST', '/v1.0/indices', data=request_data)
        return self._parse_index_info(data.get('index', {}) if data else {})

    def get_index(self, index_id: str) -> IndexInfo:
        """
        Get detailed information about a specific index.

        Args:
            index_id: The index identifier

        Returns:
            IndexInfo
        """
        data = self._make_request('GET', f'/v1.0/indices/{index_id}')
        return self._parse_index_info(data or {})

    def index_exists(self, index_id: str) -> bool:
        """
        Check if an index exists.

        Args:
            index_id: The index identifier

        Returns:
            True if the index exists, False otherwise
        """
        return self._make_head_request(f'/v1.0/indices/{index_id}')

    def delete_index(self, index_id: str) -> None:
        """
        Delete an index.

        Args:
            index_id: The index identifier
        """
        self._make_request('DELETE', f'/v1.0/indices/{index_id}')

    def update_index_labels(self, index_id: str, labels: List[str]) -> None:
        """
        Update labels on an index (full replacement).

        Args:
            index_id: The index identifier
            labels: The new labels to set
        """
        self._make_request('PUT', f'/v1.0/indices/{index_id}/labels', data={'Labels': labels or []})

    def update_index_tags(self, index_id: str, tags: Dict[str, str]) -> None:
        """
        Update tags on an index (full replacement).

        Args:
            index_id: The index identifier
            tags: The new tags to set
        """
        self._make_request('PUT', f'/v1.0/indices/{index_id}/tags', data={'Tags': tags or {}})

    def update_index_custom_metadata(self, index_id: str, custom_metadata: Any) -> IndexInfo:
        """
        Update custom metadata on an index (full replacement).

        Args:
            index_id: The index identifier
            custom_metadata: The new custom metadata to set

        Returns:
            Updated IndexInfo
        """
        data = self._make_request('PUT', f'/v1.0/indices/{index_id}/customMetadata',
                                  data={'customMetadata': custom_metadata})
        return self._parse_index_info(data or {})

    def _parse_enumeration_result(self, data: Dict, item_parser) -> EnumerationResult:
        """Parse dictionary into EnumerationResult."""
        if not data:
            return EnumerationResult(objects=[])

        objects = []
        raw_objects = data.get('objects', [])
        for obj in raw_objects:
            objects.append(item_parser(obj))

        return EnumerationResult(
            success=data.get('success', True),
            timestamp=data.get('timestamp'),
            max_results=data.get('max_results', data.get('maxResults', 100)),
            skip=data.get('skip', 0),
            iterations_required=data.get('iterations_required', data.get('iterationsRequired', 1)),
            continuation_token=data.get('continuation_token', data.get('continuationToken')),
            end_of_results=data.get('end_of_results', data.get('endOfResults', False)),
            total_records=data.get('total_records', data.get('totalRecords', 0)),
            records_remaining=data.get('records_remaining', data.get('recordsRemaining', 0)),
            objects=objects
        )

    def _parse_index_info(self, data: Dict) -> IndexInfo:
        """Parse dictionary into IndexInfo."""
        statistics = None
        if data.get('statistics'):
            stats = data['statistics']
            cache_stats = None
            if stats.get('cache_statistics'):
                cs = stats['cache_statistics']
                term_cache = None
                if cs.get('term_cache'):
                    tc = cs['term_cache']
                    term_cache = CacheStats(
                        enabled=tc.get('enabled', False),
                        hit_count=tc.get('hit_count', 0),
                        miss_count=tc.get('miss_count', 0),
                        hit_rate=tc.get('hit_rate', 0.0),
                        current_count=tc.get('current_count', 0),
                        capacity=tc.get('capacity', 0),
                        eviction_count=tc.get('eviction_count', 0),
                        expired_count=tc.get('expired_count', 0)
                    )
                document_cache = None
                if cs.get('document_cache'):
                    dc = cs['document_cache']
                    document_cache = CacheStats(
                        enabled=dc.get('enabled', False),
                        hit_count=dc.get('hit_count', 0),
                        miss_count=dc.get('miss_count', 0),
                        hit_rate=dc.get('hit_rate', 0.0),
                        current_count=dc.get('current_count', 0),
                        capacity=dc.get('capacity', 0),
                        eviction_count=dc.get('eviction_count', 0),
                        expired_count=dc.get('expired_count', 0)
                    )
                stat_cache = None
                if cs.get('statistics_cache'):
                    sc = cs['statistics_cache']
                    stat_cache = CacheStats(
                        enabled=sc.get('enabled', False),
                        hit_count=sc.get('hit_count', 0),
                        miss_count=sc.get('miss_count', 0),
                        hit_rate=sc.get('hit_rate', 0.0),
                        current_count=sc.get('current_count', 0),
                        capacity=sc.get('capacity', 0),
                        eviction_count=sc.get('eviction_count', 0),
                        expired_count=sc.get('expired_count', 0)
                    )
                cache_stats = VerbexCacheStatistics(
                    enabled=cs.get('enabled', False),
                    term_cache=term_cache,
                    document_cache=document_cache,
                    statistics_cache=stat_cache,
                    cached_document_count=cs.get('cached_document_count')
                )
            statistics = IndexStatistics(
                document_count=stats.get('document_count', 0),
                term_count=stats.get('term_count', 0),
                posting_count=stats.get('posting_count', 0),
                average_document_length=stats.get('average_document_length', 0.0),
                total_document_size=stats.get('total_document_size', 0),
                total_term_occurrences=stats.get('total_term_occurrences', 0),
                average_terms_per_document=stats.get('average_terms_per_document', 0.0),
                average_document_frequency=stats.get('average_document_frequency', 0.0),
                max_document_frequency=stats.get('max_document_frequency', 0),
                min_document_length=stats.get('min_document_length', 0),
                max_document_length=stats.get('max_document_length', 0),
                cache_statistics=cache_stats,
                generated_at=stats.get('generated_at'),
                total_term_frequency=stats.get('total_term_frequency', 0)
            )

        cache_config = None
        if data.get('cache_configuration'):
            cc = data['cache_configuration']
            cache_config = CacheConfiguration(
                enabled=cc.get('enabled', False),
                enable_term_cache=cc.get('enable_term_cache', True),
                term_cache_capacity=cc.get('term_cache_capacity', 10000),
                term_cache_evict_count=cc.get('term_cache_evict_count', 100),
                term_cache_ttl_seconds=cc.get('term_cache_ttl_seconds', 300),
                term_cache_sliding_expiration=cc.get('term_cache_sliding_expiration', True),
                enable_document_cache=cc.get('enable_document_cache', True),
                document_cache_capacity=cc.get('document_cache_capacity', 5000),
                document_cache_evict_count=cc.get('document_cache_evict_count', 50),
                document_cache_ttl_seconds=cc.get('document_cache_ttl_seconds', 600),
                document_cache_sliding_expiration=cc.get('document_cache_sliding_expiration', True),
                enable_statistics_cache=cc.get('enable_statistics_cache', True),
                statistics_cache_ttl_seconds=cc.get('statistics_cache_ttl_seconds', 60)
            )

        return IndexInfo(
            identifier=data.get('identifier', ''),
            tenant_id=data.get('tenant_id'),
            name=data.get('name'),
            description=data.get('description'),
            enabled=data.get('enabled'),
            in_memory=data.get('in_memory'),
            created_utc=data.get('created_utc'),
            statistics=statistics,
            custom_metadata=data.get('custom_metadata'),
            labels=data.get('labels'),
            tags=data.get('tags'),
            cache_configuration=cache_config
        )

    # ==================== Document Management Endpoints ====================

    def list_documents(self, index_id: str, options: Optional[EnumerationOptions] = None) -> EnumerationResult:
        """
        List documents in an index with pagination support.

        Args:
            index_id: The index identifier
            options: Optional pagination options

        Returns:
            EnumerationResult containing DocumentInfo objects and pagination information
        """
        path = f'/v1.0/indices/{index_id}/documents'
        if options:
            params = options.to_query_params()
            if params:
                path += '?' + '&'.join(f'{k}={v}' for k, v in params.items())

        data = self._make_request('GET', path)
        return self._parse_enumeration_result(data, self._parse_document_info)

    def list_all_documents(self, index_id: str) -> List[DocumentInfo]:
        """
        List all documents in an index by iterating through all pages.

        Args:
            index_id: The index identifier

        Returns:
            List of all DocumentInfo objects
        """
        all_items = []
        options = EnumerationOptions(max_results=1000)

        while True:
            result = self.list_documents(index_id, options)
            all_items.extend(result.objects)

            if result.end_of_results or not result.continuation_token:
                break

            options = result.get_next_page_options()

        return all_items

    def add_document(
        self,
        index_id: str,
        content: str,
        document_id: Optional[str] = None,
        labels: Optional[List[str]] = None,
        tags: Optional[Dict[str, str]] = None,
        custom_metadata: Optional[Any] = None
    ) -> AddDocumentData:
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
            AddDocumentData containing the document ID and confirmation
        """
        request_data: Dict[str, Any] = {'Content': content}
        if document_id:
            request_data['Id'] = document_id
        if labels:
            request_data['Labels'] = labels
        if tags:
            request_data['Tags'] = tags
        if custom_metadata is not None:
            request_data['CustomMetadata'] = custom_metadata

        data = self._make_request('POST', f'/v1.0/indices/{index_id}/documents', data=request_data)
        return AddDocumentData(
            document_id=data.get('document_id', '') if data else '',
            message=data.get('message') if data else None
        )

    def get_document(self, index_id: str, document_id: str) -> DocumentInfo:
        """
        Get a specific document.

        Args:
            index_id: The index identifier
            document_id: The document identifier

        Returns:
            DocumentInfo
        """
        data = self._make_request('GET', f'/v1.0/indices/{index_id}/documents/{document_id}')
        return self._parse_document_info(data or {})

    def get_documents_batch(self, index_id: str, document_ids: List[str]) -> BatchDocumentsResult:
        """
        Get multiple documents by IDs from an index in a single request.

        Args:
            index_id: The index identifier
            document_ids: List of document IDs to retrieve

        Returns:
            BatchDocumentsResult containing found documents and list of not found IDs
        """
        if not document_ids:
            return BatchDocumentsResult()

        # Join IDs with commas - encode individual IDs but not the commas
        from urllib.parse import quote
        ids_param = ','.join(quote(doc_id, safe='') for doc_id in document_ids)
        data = self._make_request('GET', f'/v1.0/indices/{index_id}/documents?ids={ids_param}')

        documents = []
        if data and data.get('documents'):
            documents = [self._parse_document_info(doc) for doc in data['documents']]

        return BatchDocumentsResult(
            documents=documents,
            not_found=data.get('not_found', []) if data else [],
            count=data.get('count', 0) if data else 0,
            requested_count=data.get('requested_count', 0) if data else 0
        )

    def document_exists(self, index_id: str, document_id: str) -> bool:
        """
        Check if a document exists in an index.

        Args:
            index_id: The index identifier
            document_id: The document identifier

        Returns:
            True if the document exists, False otherwise
        """
        return self._make_head_request(f'/v1.0/indices/{index_id}/documents/{document_id}')

    def delete_document(self, index_id: str, document_id: str) -> None:
        """
        Delete a document from an index.

        Args:
            index_id: The index identifier
            document_id: The document identifier
        """
        self._make_request('DELETE', f'/v1.0/indices/{index_id}/documents/{document_id}')

    def delete_documents_batch(self, index_id: str, document_ids: List[str]) -> BatchDeleteResult:
        """
        Delete multiple documents from an index by IDs in a single request.

        Args:
            index_id: The index identifier
            document_ids: List of document IDs to delete

        Returns:
            BatchDeleteResult containing lists of deleted and not found IDs
        """
        if not document_ids:
            return BatchDeleteResult()

        # Join IDs with commas - encode individual IDs but not the commas
        from urllib.parse import quote
        ids_param = ','.join(quote(doc_id, safe='') for doc_id in document_ids)
        data = self._make_request('DELETE', f'/v1.0/indices/{index_id}/documents?ids={ids_param}')

        return BatchDeleteResult(
            deleted=data.get('deleted', []) if data else [],
            not_found=data.get('not_found', []) if data else [],
            deleted_count=data.get('deleted_count', 0) if data else 0,
            not_found_count=data.get('not_found_count', 0) if data else 0,
            requested_count=data.get('requested_count', 0) if data else 0
        )

    def add_documents_batch(self, index_id: str, documents: List[BatchAddDocumentItem]) -> BatchAddResult:
        """
        Add multiple documents to an index in a single request.

        Args:
            index_id: The index identifier
            documents: List of BatchAddDocumentItem objects to add

        Returns:
            BatchAddResult containing lists of added and failed documents
        """
        if not documents:
            return BatchAddResult()

        # Convert documents to request format
        request_docs = []
        for doc in documents:
            request_doc = {
                'Name': doc.name,
                'Content': doc.content
            }
            if doc.id:
                request_doc['Id'] = doc.id
            if doc.labels:
                request_doc['Labels'] = doc.labels
            if doc.tags:
                request_doc['Tags'] = doc.tags
            if doc.custom_metadata is not None:
                request_doc['CustomMetadata'] = doc.custom_metadata
            request_docs.append(request_doc)

        data = self._make_request('POST', f'/v1.0/indices/{index_id}/documents/batch',
                                  data={'Documents': request_docs})

        added_items = []
        for item in (data.get('added', []) if data else []):
            added_items.append(BatchAddResultItem(
                document_id=item.get('document_id', ''),
                name=item.get('name', ''),
                success=item.get('success', False),
                error_message=item.get('error_message')
            ))

        failed_items = []
        for item in (data.get('failed', []) if data else []):
            failed_items.append(BatchAddResultItem(
                document_id=item.get('document_id', ''),
                name=item.get('name', ''),
                success=item.get('success', False),
                error_message=item.get('error_message')
            ))

        return BatchAddResult(
            added=added_items,
            failed=failed_items,
            added_count=data.get('added_count', 0) if data else 0,
            failed_count=data.get('failed_count', 0) if data else 0,
            requested_count=data.get('requested_count', 0) if data else 0
        )

    def check_documents_exist(self, index_id: str, document_ids: List[str]) -> BatchExistenceResult:
        """
        Check if multiple documents exist in an index.

        Args:
            index_id: The index identifier
            document_ids: List of document IDs to check

        Returns:
            BatchExistenceResult containing lists of existing and not found IDs
        """
        if not document_ids:
            return BatchExistenceResult()

        data = self._make_request('POST', f'/v1.0/indices/{index_id}/documents/exists',
                                  data={'Ids': document_ids})

        return BatchExistenceResult(
            exists=data.get('exists', []) if data else [],
            not_found=data.get('not_found', []) if data else [],
            exists_count=data.get('exists_count', 0) if data else 0,
            not_found_count=data.get('not_found_count', 0) if data else 0,
            requested_count=data.get('requested_count', 0) if data else 0
        )

    def update_document_labels(self, index_id: str, document_id: str, labels: List[str]) -> None:
        """
        Update labels on a document (full replacement).

        Args:
            index_id: The index identifier
            document_id: The document identifier
            labels: The new labels to set
        """
        self._make_request('PUT', f'/v1.0/indices/{index_id}/documents/{document_id}/labels',
                          data={'Labels': labels or []})

    def update_document_tags(self, index_id: str, document_id: str, tags: Dict[str, str]) -> None:
        """
        Update tags on a document (full replacement).

        Args:
            index_id: The index identifier
            document_id: The document identifier
            tags: The new tags to set
        """
        self._make_request('PUT', f'/v1.0/indices/{index_id}/documents/{document_id}/tags',
                          data={'Tags': tags or {}})

    def update_document_custom_metadata(self, index_id: str, document_id: str,
                                        custom_metadata: Any) -> DocumentInfo:
        """
        Update custom metadata on a document (full replacement).

        Args:
            index_id: The index identifier
            document_id: The document identifier
            custom_metadata: The new custom metadata to set

        Returns:
            Updated DocumentInfo
        """
        data = self._make_request('PUT', f'/v1.0/indices/{index_id}/documents/{document_id}/customMetadata',
                                  data={'customMetadata': custom_metadata})
        return self._parse_document_info(data or {})

    def _parse_document_info(self, data: Dict) -> DocumentInfo:
        """Parse dictionary into DocumentInfo."""
        return DocumentInfo(
            document_id=data.get('document_id', ''),
            document_path=data.get('document_path'),
            original_file_name=data.get('original_file_name'),
            document_length=data.get('document_length', 0),
            indexed_date=data.get('indexed_date'),
            last_modified=data.get('last_modified'),
            content_sha256=data.get('content_sha256'),
            terms=data.get('terms'),
            is_deleted=data.get('is_deleted', False),
            custom_metadata=data.get('custom_metadata'),
            labels=data.get('labels'),
            tags=data.get('tags'),
            indexing_runtime_ms=data.get('indexing_runtime_ms')
        )

    # ==================== Search Endpoint ====================

    def search(
        self,
        index_id: str,
        query: str,
        max_results: int = 100,
        labels: Optional[List[str]] = None,
        tags: Optional[Dict[str, str]] = None
    ) -> SearchData:
        """
        Search documents in an index with optional label and tag filters.

        Use "*" as the query to return all documents (optionally filtered by labels/tags)
        without term matching. Wildcard results have a score of 0.

        Args:
            index_id: The index identifier
            query: The search query. Use "*" for wildcard (all documents).
            max_results: Maximum number of results to return
            labels: Optional list of labels to filter by (AND logic, case-insensitive)
            tags: Optional dict of tags to filter by (AND logic, exact match)

        Returns:
            SearchData containing search results
        """
        request_data = {
            'Query': query,
            'MaxResults': max_results
        }
        if labels and len(labels) > 0:
            request_data['Labels'] = labels
        if tags and len(tags) > 0:
            request_data['Tags'] = tags

        data = self._make_request('POST', f'/v1.0/indices/{index_id}/search', data=request_data)

        results = []
        if data and data.get('results'):
            for r in data['results']:
                results.append(SearchResult(
                    document_id=r.get('document_id', ''),
                    score=r.get('score', 0.0),
                    content=r.get('content'),
                    total_term_matches=r.get('total_term_matches', 0),
                    term_scores=r.get('term_scores'),
                    term_frequencies=r.get('term_frequencies')
                ))

        return SearchData(
            query=data.get('query', '') if data else '',
            results=results,
            total_count=data.get('total_count', 0) if data else 0,
            max_results=data.get('max_results', 100) if data else 100,
            search_time=data.get('search_time', 0.0) if data else 0.0
        )

    # ==================== Terms Endpoints ====================

    def get_top_terms(self, index_id: str, limit: int = 10) -> Dict[str, int]:
        """
        Get the top terms in an index sorted by document frequency.

        Args:
            index_id: The index identifier
            limit: Maximum number of terms to return (default: 10)

        Returns:
            Dictionary mapping terms to their document frequencies
        """
        endpoint = f'/v1.0/indices/{index_id}/terms/top'
        if limit != 10:
            endpoint += f'?limit={limit}'
        data = self._make_request('GET', endpoint)
        return data if data else {}

    # ==================== Backup & Restore Endpoints ====================

    def backup(self, index_id: str) -> bytes:
        """
        Create a backup of an index.

        Args:
            index_id: The index identifier

        Returns:
            Bytes containing the backup ZIP archive
        """
        if not index_id:
            raise ValueError("index_id is required")

        url = f"{self._endpoint}/v1.0/indices/{index_id}/backup"
        response = requests.post(
            url,
            headers={'Authorization': f'Bearer {self._access_key}'}
        )

        if not response.ok:
            raise VerbexError(f"Backup failed: {response.text}", response.status_code)

        return response.content

    def backup_to_file(self, index_id: str, file_path: str) -> None:
        """
        Create a backup of an index and save it to a file.

        Args:
            index_id: The index identifier
            file_path: The file path to save the backup to
        """
        if not file_path:
            raise ValueError("file_path is required")

        backup_data = self.backup(index_id)
        with open(file_path, 'wb') as f:
            f.write(backup_data)

    def restore(self, file_path: str, name: Optional[str] = None) -> Dict[str, Any]:
        """
        Restore a backup to create a new index.

        Args:
            file_path: The path to the backup file
            name: Optional new name for the restored index

        Returns:
            Dict containing the restore result with indexId
        """
        if not file_path:
            raise ValueError("file_path is required")

        url = f"{self._endpoint}/v1.0/indices/restore"

        with open(file_path, 'rb') as f:
            files = {'file': ('backup.vbx', f, 'application/zip')}
            data = {}
            if name:
                data['name'] = name

            response = requests.post(
                url,
                headers={'Authorization': f'Bearer {self._access_key}'},
                files=files,
                data=data
            )

        if not response.ok:
            try:
                error_data = response.json()
                error_message = error_data.get('ErrorMessage') or error_data.get('errorMessage') or 'Restore failed'
            except:
                error_message = response.text
            raise VerbexError(error_message, response.status_code)

        result = response.json()
        raw_data = result.get('Data') or result.get('data') or {}
        return _convert_keys_to_snake_case(raw_data)

    def restore_from_bytes(self, backup_data: bytes, name: Optional[str] = None) -> Dict[str, Any]:
        """
        Restore a backup from bytes to create a new index.

        Args:
            backup_data: The backup data as bytes
            name: Optional new name for the restored index

        Returns:
            Dict containing the restore result with index_id
        """
        if not backup_data:
            raise ValueError("backup_data is required")

        url = f"{self._endpoint}/v1.0/indices/restore"

        from io import BytesIO
        files = {'file': ('backup.vbx', BytesIO(backup_data), 'application/zip')}
        data = {}
        if name:
            data['name'] = name

        response = requests.post(
            url,
            headers={'Authorization': f'Bearer {self._access_key}'},
            files=files,
            data=data
        )

        if not response.ok:
            try:
                error_data = response.json()
                error_message = error_data.get('ErrorMessage') or error_data.get('errorMessage') or 'Restore failed'
            except:
                error_message = response.text
            raise VerbexError(error_message, response.status_code)

        result = response.json()
        raw_data = result.get('Data') or result.get('data') or {}
        return _convert_keys_to_snake_case(raw_data)

    def restore_replace(self, index_id: str, file_path: str) -> Dict[str, Any]:
        """
        Restore a backup by replacing an existing index.

        Args:
            index_id: The index identifier to replace
            file_path: The path to the backup file

        Returns:
            Dict containing the restore result
        """
        if not index_id:
            raise ValueError("index_id is required")
        if not file_path:
            raise ValueError("file_path is required")

        url = f"{self._endpoint}/v1.0/indices/{index_id}/restore"

        with open(file_path, 'rb') as f:
            files = {'file': ('backup.vbx', f, 'application/zip')}

            response = requests.post(
                url,
                headers={'Authorization': f'Bearer {self._access_key}'},
                files=files
            )

        if not response.ok:
            try:
                error_data = response.json()
                error_message = error_data.get('ErrorMessage') or error_data.get('errorMessage') or 'Restore failed'
            except:
                error_message = response.text
            raise VerbexError(error_message, response.status_code)

        result = response.json()
        raw_data = result.get('Data') or result.get('data') or {}
        return _convert_keys_to_snake_case(raw_data)

    # ==================== Admin - Tenant Management Endpoints ====================

    def list_tenants(self, options: Optional[EnumerationOptions] = None) -> EnumerationResult:
        """
        List tenants with pagination support.

        Args:
            options: Optional pagination options

        Returns:
            EnumerationResult containing TenantInfo objects and pagination information
        """
        path = '/v1.0/tenants'
        if options:
            params = options.to_query_params()
            if params:
                path += '?' + '&'.join(f'{k}={v}' for k, v in params.items())

        data = self._make_request('GET', path)
        return self._parse_enumeration_result(data, self._parse_tenant_info)

    def list_all_tenants(self) -> List[TenantInfo]:
        """
        List all tenants by iterating through all pages.

        Returns:
            List of all TenantInfo objects
        """
        all_items = []
        options = EnumerationOptions(max_results=1000)

        while True:
            result = self.list_tenants(options)
            all_items.extend(result.objects)

            if result.end_of_results or not result.continuation_token:
                break

            options = result.get_next_page_options()

        return all_items

    def get_tenant(self, tenant_id: str) -> TenantInfo:
        """
        Get a specific tenant.

        Args:
            tenant_id: The tenant identifier

        Returns:
            TenantInfo
        """
        data = self._make_request('GET', f'/v1.0/admin/tenants/{tenant_id}')
        return self._parse_tenant_info(data or {})

    def create_tenant(self, name: str, description: Optional[str] = None) -> TenantInfo:
        """
        Create a new tenant.

        Args:
            name: Tenant name
            description: Optional description

        Returns:
            Created TenantInfo
        """
        request_data: Dict[str, Any] = {'name': name}
        if description:
            request_data['description'] = description

        data = self._make_request('POST', '/v1.0/admin/tenants', data=request_data)
        return self._parse_tenant_info(data.get('tenant', {}) if data else {})

    def delete_tenant(self, tenant_id: str) -> None:
        """
        Delete a tenant.

        Args:
            tenant_id: The tenant identifier
        """
        self._make_request('DELETE', f'/v1.0/admin/tenants/{tenant_id}')

    def update_tenant_labels(self, tenant_id: str, labels: List[str]) -> None:
        """
        Update labels on a tenant (full replacement).

        Args:
            tenant_id: The tenant identifier
            labels: The new labels to set
        """
        self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/labels', data={'Labels': labels or []})

    def update_tenant_tags(self, tenant_id: str, tags: Dict[str, str]) -> None:
        """
        Update tags on a tenant (full replacement).

        Args:
            tenant_id: The tenant identifier
            tags: The new tags to set
        """
        self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/tags', data={'Tags': tags or {}})

    def _parse_tenant_info(self, data: Dict) -> TenantInfo:
        """Parse dictionary into TenantInfo."""
        return TenantInfo(
            identifier=data.get('identifier', ''),
            name=data.get('name'),
            active=data.get('active', False),
            created_utc=data.get('created_utc'),
            labels=data.get('labels'),
            tags=data.get('tags')
        )

    # ==================== Admin - User Management Endpoints ====================

    def list_users(self, tenant_id: str, options: Optional[EnumerationOptions] = None) -> EnumerationResult:
        """
        List users in a tenant with pagination support.

        Args:
            tenant_id: The tenant identifier
            options: Optional pagination options

        Returns:
            EnumerationResult containing UserInfo objects and pagination information
        """
        path = f'/v1.0/tenants/{tenant_id}/users'
        if options:
            params = options.to_query_params()
            if params:
                path += '?' + '&'.join(f'{k}={v}' for k, v in params.items())

        data = self._make_request('GET', path)
        return self._parse_enumeration_result(data, self._parse_user_info)

    def list_all_users(self, tenant_id: str) -> List[UserInfo]:
        """
        List all users in a tenant by iterating through all pages.

        Args:
            tenant_id: The tenant identifier

        Returns:
            List of all UserInfo objects
        """
        all_items = []
        options = EnumerationOptions(max_results=1000)

        while True:
            result = self.list_users(tenant_id, options)
            all_items.extend(result.objects)

            if result.end_of_results or not result.continuation_token:
                break

            options = result.get_next_page_options()

        return all_items

    def get_user(self, tenant_id: str, user_id: str) -> UserInfo:
        """
        Get a specific user.

        Args:
            tenant_id: The tenant identifier
            user_id: The user identifier

        Returns:
            UserInfo
        """
        data = self._make_request('GET', f'/v1.0/admin/tenants/{tenant_id}/users/{user_id}')
        return self._parse_user_info(data or {})

    def create_user(
        self,
        tenant_id: str,
        email: str,
        password: str,
        first_name: Optional[str] = None,
        last_name: Optional[str] = None,
        is_admin: bool = False
    ) -> UserInfo:
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
            Created UserInfo
        """
        request_data: Dict[str, Any] = {
            'email': email,
            'password': password
        }
        if first_name:
            request_data['firstName'] = first_name
        if last_name:
            request_data['lastName'] = last_name
        if is_admin:
            request_data['isAdmin'] = is_admin

        data = self._make_request('POST', f'/v1.0/admin/tenants/{tenant_id}/users', data=request_data)
        return self._parse_user_info(data.get('user', {}) if data else {})

    def delete_user(self, tenant_id: str, user_id: str) -> None:
        """
        Delete a user.

        Args:
            tenant_id: The tenant identifier
            user_id: The user identifier
        """
        self._make_request('DELETE', f'/v1.0/admin/tenants/{tenant_id}/users/{user_id}')

    def update_user_labels(self, tenant_id: str, user_id: str, labels: List[str]) -> None:
        """
        Update labels on a user (full replacement).

        Args:
            tenant_id: The tenant identifier
            user_id: The user identifier
            labels: The new labels to set
        """
        self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/users/{user_id}/labels',
                          data={'Labels': labels or []})

    def update_user_tags(self, tenant_id: str, user_id: str, tags: Dict[str, str]) -> None:
        """
        Update tags on a user (full replacement).

        Args:
            tenant_id: The tenant identifier
            user_id: The user identifier
            tags: The new tags to set
        """
        self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/users/{user_id}/tags',
                          data={'Tags': tags or {}})

    def _parse_user_info(self, data: Dict) -> UserInfo:
        """Parse dictionary into UserInfo."""
        return UserInfo(
            identifier=data.get('identifier', ''),
            tenant_id=data.get('tenant_id', ''),
            email=data.get('email', ''),
            first_name=data.get('first_name'),
            last_name=data.get('last_name'),
            is_admin=data.get('is_admin', False),
            active=data.get('active', False),
            created_utc=data.get('created_utc'),
            labels=data.get('labels'),
            tags=data.get('tags')
        )

    # ==================== Admin - Credential Management Endpoints ====================

    def list_credentials(self, tenant_id: str, options: Optional[EnumerationOptions] = None) -> EnumerationResult:
        """
        List credentials in a tenant with pagination support.

        Args:
            tenant_id: The tenant identifier
            options: Optional pagination options

        Returns:
            EnumerationResult containing CredentialInfo objects and pagination information
        """
        path = f'/v1.0/tenants/{tenant_id}/credentials'
        if options:
            params = options.to_query_params()
            if params:
                path += '?' + '&'.join(f'{k}={v}' for k, v in params.items())

        data = self._make_request('GET', path)
        return self._parse_enumeration_result(data, self._parse_credential_info)

    def list_all_credentials(self, tenant_id: str) -> List[CredentialInfo]:
        """
        List all credentials in a tenant by iterating through all pages.

        Args:
            tenant_id: The tenant identifier

        Returns:
            List of all CredentialInfo objects
        """
        all_items = []
        options = EnumerationOptions(max_results=1000)

        while True:
            result = self.list_credentials(tenant_id, options)
            all_items.extend(result.objects)

            if result.end_of_results or not result.continuation_token:
                break

            options = result.get_next_page_options()

        return all_items

    def get_credential(self, tenant_id: str, credential_id: str) -> CredentialInfo:
        """
        Get a specific credential.

        Args:
            tenant_id: The tenant identifier
            credential_id: The credential identifier

        Returns:
            CredentialInfo
        """
        data = self._make_request('GET', f'/v1.0/admin/tenants/{tenant_id}/credentials/{credential_id}')
        return self._parse_credential_info(data or {})

    def create_credential(self, tenant_id: str, description: Optional[str] = None) -> CredentialInfo:
        """
        Create a new credential (API key) in a tenant.

        Args:
            tenant_id: The tenant identifier
            description: Optional description

        Returns:
            Created CredentialInfo (includes bearer token)
        """
        request_data: Dict[str, Any] = {}
        if description:
            request_data['description'] = description

        data = self._make_request('POST', f'/v1.0/admin/tenants/{tenant_id}/credentials', data=request_data)
        return self._parse_credential_info(data.get('credential', {}) if data else {})

    def delete_credential(self, tenant_id: str, credential_id: str) -> None:
        """
        Delete a credential.

        Args:
            tenant_id: The tenant identifier
            credential_id: The credential identifier
        """
        self._make_request('DELETE', f'/v1.0/admin/tenants/{tenant_id}/credentials/{credential_id}')

    def update_credential_labels(self, tenant_id: str, credential_id: str, labels: List[str]) -> None:
        """
        Update labels on a credential (full replacement).

        Args:
            tenant_id: The tenant identifier
            credential_id: The credential identifier
            labels: The new labels to set
        """
        self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/credentials/{credential_id}/labels',
                          data={'Labels': labels or []})

    def update_credential_tags(self, tenant_id: str, credential_id: str, tags: Dict[str, str]) -> None:
        """
        Update tags on a credential (full replacement).

        Args:
            tenant_id: The tenant identifier
            credential_id: The credential identifier
            tags: The new tags to set
        """
        self._make_request('PUT', f'/v1.0/tenants/{tenant_id}/credentials/{credential_id}/tags',
                          data={'Tags': tags or {}})

    def _parse_credential_info(self, data: Dict) -> CredentialInfo:
        """Parse dictionary into CredentialInfo."""
        return CredentialInfo(
            identifier=data.get('identifier', ''),
            tenant_id=data.get('tenant_id', ''),
            name=data.get('name'),
            bearer_token=data.get('bearer_token'),
            active=data.get('active', False),
            created_utc=data.get('created_utc'),
            labels=data.get('labels'),
            tags=data.get('tags')
        )

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
