/**
 * Verbex JavaScript SDK
 * A comprehensive SDK for interacting with the Verbex Inverted Index REST API.
 * All methods return domain objects directly rather than wrapped responses.
 */

/**
 * Authentication result enumeration values.
 * @readonly
 * @enum {string}
 */
const AuthenticationResult = Object.freeze({
    Success: 'Success',
    NotAuthenticated: 'NotAuthenticated',
    MissingCredentials: 'MissingCredentials',
    NotFound: 'NotFound',
    Inactive: 'Inactive',
    InvalidCredentials: 'InvalidCredentials',
    TenantNotFound: 'TenantNotFound',
    TenantInactive: 'TenantInactive',
    TenantAccessDenied: 'TenantAccessDenied'
});

/**
 * Authorization result enumeration values.
 * @readonly
 * @enum {string}
 */
const AuthorizationResult = Object.freeze({
    Authorized: 'Authorized',
    Unauthorized: 'Unauthorized',
    InsufficientPrivileges: 'InsufficientPrivileges',
    ResourceNotFound: 'ResourceNotFound',
    AccessDenied: 'AccessDenied'
});

/**
 * Result of a login attempt.
 */
class LoginResult {
    /**
     * Create a LoginResult.
     * @param {object} data - Login result data
     */
    constructor(data = {}) {
        this.success = data.success || false;
        this.authenticationResult = data.authenticationResult || AuthenticationResult.NotAuthenticated;
        this.authorizationResult = data.authorizationResult || AuthorizationResult.Unauthorized;
        this.errorMessage = data.errorMessage || null;
        this.token = data.token || null;
        this.tenantId = data.tenantId || null;
        this.userId = data.userId || null;
        this.email = data.email || null;
        this.isAdmin = data.isAdmin || false;
        this.isGlobalAdmin = data.isGlobalAdmin || false;
    }

    /**
     * Create a successful login result.
     * @param {string} token - The bearer token
     * @param {object} [options] - Additional options
     * @returns {LoginResult} Successful login result
     */
    static successful(token, options = {}) {
        return new LoginResult({
            success: true,
            authenticationResult: AuthenticationResult.Success,
            authorizationResult: AuthorizationResult.Authorized,
            token,
            tenantId: options.tenantId || null,
            userId: options.userId || null,
            email: options.email || null,
            isAdmin: options.isAdmin || false,
            isGlobalAdmin: options.isGlobalAdmin || false
        });
    }

    /**
     * Create a failed login result.
     * @param {string} authenticationResult - The authentication result
     * @param {string} authorizationResult - The authorization result
     * @param {string} [errorMessage] - Optional error message
     * @returns {LoginResult} Failed login result
     */
    static failed(authenticationResult, authorizationResult, errorMessage = null) {
        return new LoginResult({
            success: false,
            authenticationResult,
            authorizationResult,
            errorMessage
        });
    }
}

/**
 * Error thrown for Verbex API errors.
 */
class VerbexError extends Error {
    /**
     * Create a VerbexError.
     * @param {string} message - Error message
     * @param {number} statusCode - HTTP status code
     * @param {object} response - Full API response
     */
    constructor(message, statusCode = 0, response = null) {
        super(message);
        this.name = 'VerbexError';
        this.statusCode = statusCode;
        this.response = response;
    }
}

/**
 * Helper function to convert PascalCase keys to camelCase recursively.
 * Also adds convenience aliases for common fields.
 * @param {any} obj - Object to convert
 * @returns {any} Object with camelCase keys
 */
function toCamelCaseKeys(obj) {
    if (obj === null || obj === undefined) return obj;
    if (Array.isArray(obj)) {
        return obj.map(item => toCamelCaseKeys(item));
    }
    if (typeof obj !== 'object') return obj;

    const result = {};
    for (const key of Object.keys(obj)) {
        // Convert first character to lowercase
        const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
        result[camelKey] = toCamelCaseKeys(obj[key]);
    }

    // Add convenience aliases
    if (result.documentId && !result.id) {
        result.id = result.documentId.toString();
    }

    return result;
}

/**
 * Index information model.
 */
class IndexInfo {
    /**
     * Create an IndexInfo.
     * @param {object} data - Index data (camelCase from toCamelCaseKeys)
     */
    constructor(data) {
        this.identifier = data.identifier || '';
        this.tenantId = data.tenantId || null;
        this.name = data.name || null;
        this.description = data.description || null;
        this.enabled = data.enabled || null;
        this.inMemory = data.inMemory || null;
        this.createdUtc = data.createdUtc || null;
        this.statistics = data.statistics ? new IndexStatistics(data.statistics) : null;
        this.customMetadata = data.customMetadata || null;
        this.labels = data.labels || null;
        this.tags = data.tags || null;
        this.cacheConfiguration = data.cacheConfiguration ? new CacheConfiguration(data.cacheConfiguration) : null;
    }

    /**
     * Alias for identifier for convenience.
     * @returns {string} The index identifier
     */
    get id() {
        return this.identifier;
    }
}

/**
 * Document information model.
 */
class DocumentInfo {
    /**
     * Create a DocumentInfo.
     * @param {object} data - Document data (camelCase from toCamelCaseKeys)
     */
    constructor(data) {
        this.documentId = data.documentId || '';
        this.id = this.documentId || data.id || '';
        this.documentPath = data.documentPath || null;
        this.originalFileName = data.originalFileName || null;
        this.documentLength = data.documentLength || 0;
        this.indexedDate = data.indexedDate || null;
        this.lastModified = data.lastModified || null;
        this.contentSha256 = data.contentSha256 || null;
        this.terms = data.terms || null;
        this.isDeleted = data.isDeleted || false;
        this.customMetadata = data.customMetadata || null;
        this.labels = data.labels || null;
        this.tags = data.tags || null;
        this.indexingRuntimeMs = data.indexingRuntimeMs || null;
    }
}

/**
 * Add document response data.
 */
class AddDocumentData {
    /**
     * Create an AddDocumentData.
     * @param {object} data - Add document response data
     */
    constructor(data) {
        this.documentId = data.documentId || '';
        this.message = data.message || null;
    }
}

/**
 * Batch document retrieval result.
 */
class BatchDocumentsResult {
    /**
     * Create a BatchDocumentsResult.
     * @param {object} data - Batch documents response data
     */
    constructor(data) {
        this.documents = (data?.documents || []).map(doc => new DocumentInfo(doc));
        this.notFound = data?.notFound || [];
        this.count = data?.count || 0;
        this.requestedCount = data?.requestedCount || 0;
    }
}

/**
 * Batch document deletion result.
 */
class BatchDeleteResult {
    /**
     * Create a BatchDeleteResult.
     * @param {object} data - Batch delete response data
     */
    constructor(data) {
        this.deleted = data?.deleted || [];
        this.notFound = data?.notFound || [];
        this.deletedCount = data?.deletedCount || 0;
        this.notFoundCount = data?.notFoundCount || 0;
        this.requestedCount = data?.requestedCount || 0;
    }
}

/**
 * Batch document add result item.
 */
class BatchAddResultItem {
    /**
     * Create a BatchAddResultItem.
     * @param {object} data - Add result item data
     */
    constructor(data) {
        this.documentId = data?.documentId || '';
        this.name = data?.name || '';
        this.success = data?.success || false;
        this.errorMessage = data?.errorMessage || null;
    }
}

/**
 * Batch document add result.
 */
class BatchAddResult {
    /**
     * Create a BatchAddResult.
     * @param {object} data - Batch add response data
     */
    constructor(data) {
        this.added = (data?.added || []).map(item => new BatchAddResultItem(item));
        this.failed = (data?.failed || []).map(item => new BatchAddResultItem(item));
        this.addedCount = data?.addedCount || 0;
        this.failedCount = data?.failedCount || 0;
        this.requestedCount = data?.requestedCount || 0;
    }
}

/**
 * Batch existence check result.
 */
class BatchExistenceResult {
    /**
     * Create a BatchExistenceResult.
     * @param {object} data - Batch existence check response data
     */
    constructor(data) {
        this.exists = data?.exists || [];
        this.notFound = data?.notFound || [];
        this.existsCount = data?.existsCount || 0;
        this.notFoundCount = data?.notFoundCount || 0;
        this.requestedCount = data?.requestedCount || 0;
    }
}

/**
 * Search result model.
 */
class SearchResult {
    /**
     * Create a SearchResult.
     * @param {object} data - Search result data (camelCase from toCamelCaseKeys)
     */
    constructor(data) {
        this.documentId = data.documentId || '';
        this.score = data.score || 0;
        this.content = data.content || null;
        this.totalTermMatches = data.totalTermMatches || 0;
        this.termScores = data.termScores || null;
        this.termFrequencies = data.termFrequencies || null;
    }
}

/**
 * Search response model.
 */
class SearchData {
    /**
     * Create a SearchData.
     * @param {object} data - Search response data (camelCase from toCamelCaseKeys)
     */
    constructor(data) {
        this.query = data.query || '';
        this.results = (data.results || []).map(r => new SearchResult(r));
        this.totalCount = data.totalCount || 0;
        this.maxResults = data.maxResults || 100;
        this.searchTime = data.searchTime || 0;
    }
}

/**
 * Cache configuration for an index.
 */
class CacheConfiguration {
    /**
     * Create a CacheConfiguration.
     * @param {object} data - Cache configuration data
     */
    constructor(data = {}) {
        this.enabled = data.enabled || false;
        this.enableTermCache = data.enableTermCache !== undefined ? data.enableTermCache : true;
        this.termCacheCapacity = data.termCacheCapacity || 10000;
        this.termCacheEvictCount = data.termCacheEvictCount || 100;
        this.termCacheTtlSeconds = data.termCacheTtlSeconds || 300;
        this.termCacheSlidingExpiration = data.termCacheSlidingExpiration !== undefined ? data.termCacheSlidingExpiration : true;
        this.enableDocumentCache = data.enableDocumentCache !== undefined ? data.enableDocumentCache : true;
        this.documentCacheCapacity = data.documentCacheCapacity || 5000;
        this.documentCacheEvictCount = data.documentCacheEvictCount || 50;
        this.documentCacheTtlSeconds = data.documentCacheTtlSeconds || 600;
        this.documentCacheSlidingExpiration = data.documentCacheSlidingExpiration !== undefined ? data.documentCacheSlidingExpiration : true;
        this.enableStatisticsCache = data.enableStatisticsCache !== undefined ? data.enableStatisticsCache : true;
        this.statisticsCacheTtlSeconds = data.statisticsCacheTtlSeconds || 60;
    }

    /**
     * Create a CacheConfiguration with caching enabled and default settings.
     * @returns {CacheConfiguration} Cache configuration with caching enabled
     */
    static createEnabled() {
        return new CacheConfiguration({ enabled: true });
    }
}

/**
 * Statistics for a single cache instance.
 */
class CacheStats {
    /**
     * Create a CacheStats.
     * @param {object} data - Cache stats data
     */
    constructor(data = {}) {
        this.enabled = data.enabled || false;
        this.hitCount = data.hitCount || 0;
        this.missCount = data.missCount || 0;
        this.hitRate = data.hitRate || 0;
        this.currentCount = data.currentCount || 0;
        this.capacity = data.capacity || 0;
        this.evictionCount = data.evictionCount || 0;
        this.expiredCount = data.expiredCount || 0;
    }
}

/**
 * Aggregate cache statistics for an index.
 */
class VerbexCacheStatistics {
    /**
     * Create a VerbexCacheStatistics.
     * @param {object} data - Cache statistics data
     */
    constructor(data = {}) {
        this.enabled = data.enabled || false;
        this.termCache = data.termCache ? new CacheStats(data.termCache) : null;
        this.documentCache = data.documentCache ? new CacheStats(data.documentCache) : null;
        this.statisticsCache = data.statisticsCache ? new CacheStats(data.statisticsCache) : null;
        this.cachedDocumentCount = data.cachedDocumentCount || null;
    }
}

/**
 * Index statistics model.
 */
class IndexStatistics {
    /**
     * Create an IndexStatistics.
     * @param {object} data - Index statistics data
     */
    constructor(data = {}) {
        this.documentCount = data.documentCount || 0;
        this.termCount = data.termCount || 0;
        this.postingCount = data.postingCount || 0;
        this.averageDocumentLength = data.averageDocumentLength || 0;
        this.totalDocumentSize = data.totalDocumentSize || 0;
        this.totalTermOccurrences = data.totalTermOccurrences || 0;
        this.averageTermsPerDocument = data.averageTermsPerDocument || 0;
        this.averageDocumentFrequency = data.averageDocumentFrequency || 0;
        this.maxDocumentFrequency = data.maxDocumentFrequency || 0;
        this.minDocumentLength = data.minDocumentLength || 0;
        this.maxDocumentLength = data.maxDocumentLength || 0;
        this.cacheStatistics = data.cacheStatistics ? new VerbexCacheStatistics(data.cacheStatistics) : null;
        this.generatedAt = data.generatedAt || null;
    }
}

/**
 * Health check response data.
 */
class HealthData {
    /**
     * Create a HealthData.
     * @param {object} data - Health data
     */
    constructor(data) {
        this.status = data.status || null;
        this.version = data.version || null;
        this.timestamp = data.timestamp || null;
    }
}

/**
 * Token validation response data.
 */
class ValidationData {
    /**
     * Create a ValidationData.
     * @param {object} data - Validation data
     */
    constructor(data) {
        this.valid = data.valid || false;
        this.tenantId = data.tenantId || null;
        this.userId = data.userId || null;
        this.email = data.email || null;
        this.isAdmin = data.isAdmin || false;
        this.isGlobalAdmin = data.isGlobalAdmin || false;
    }
}

/**
 * Tenant information model.
 */
class TenantInfo {
    /**
     * Create a TenantInfo.
     * @param {object} data - Tenant data (camelCase from toCamelCaseKeys)
     */
    constructor(data) {
        this.identifier = data.identifier || '';
        this.name = data.name || null;
        this.active = data.active || false;
        this.createdUtc = data.createdUtc || null;
        this.labels = data.labels || null;
        this.tags = data.tags || null;
    }
}

/**
 * User information model.
 */
class UserInfo {
    /**
     * Create a UserInfo.
     * @param {object} data - User data (camelCase from toCamelCaseKeys)
     */
    constructor(data) {
        this.identifier = data.identifier || '';
        this.tenantId = data.tenantId || '';
        this.email = data.email || '';
        this.firstName = data.firstName || null;
        this.lastName = data.lastName || null;
        this.isAdmin = data.isAdmin || false;
        this.active = data.active || false;
        this.createdUtc = data.createdUtc || null;
        this.labels = data.labels || null;
        this.tags = data.tags || null;
    }
}

/**
 * Credential information model.
 */
class CredentialInfo {
    /**
     * Create a CredentialInfo.
     * @param {object} data - Credential data (camelCase from toCamelCaseKeys)
     */
    constructor(data) {
        this.identifier = data.identifier || '';
        this.tenantId = data.tenantId || '';
        this.name = data.name || null;
        this.bearerToken = data.bearerToken || null;
        this.active = data.active || false;
        this.createdUtc = data.createdUtc || null;
        this.labels = data.labels || null;
        this.tags = data.tags || null;
    }
}

/**
 * Enumeration order options.
 * @readonly
 * @enum {string}
 */
const EnumerationOrder = Object.freeze({
    CreatedAscending: 'CreatedAscending',
    CreatedDescending: 'CreatedDescending'
});

/**
 * Options for paginated enumeration requests.
 */
class EnumerationOptions {
    /**
     * Create EnumerationOptions.
     * @param {object} options - Options
     * @param {number} [options.maxResults=100] - Maximum results per page (1-1000)
     * @param {number} [options.skip=0] - Number of records to skip
     * @param {string} [options.continuationToken] - Continuation token from previous result
     * @param {string} [options.ordering='CreatedDescending'] - Result ordering
     * @param {string[]} [options.labels] - Optional labels to filter by (AND logic, case-insensitive)
     * @param {Object<string, string>} [options.tags] - Optional tags to filter by (AND logic, exact match)
     */
    constructor(options = {}) {
        this.maxResults = options.maxResults || 100;
        this.skip = options.skip || 0;
        this.continuationToken = options.continuationToken || null;
        this.ordering = options.ordering || EnumerationOrder.CreatedDescending;
        /** @type {string[]|null} */
        this.labels = options.labels || null;
        /** @type {Object<string, string>|null} */
        this.tags = options.tags || null;
    }

    /**
     * Convert to URL query string.
     * @returns {string} Query string without leading '?'
     */
    toQueryString() {
        const params = [];
        if (this.maxResults !== 100) {
            params.push(`maxResults=${this.maxResults}`);
        }
        if (this.skip > 0) {
            params.push(`skip=${this.skip}`);
        }
        if (this.continuationToken) {
            params.push(`continuationToken=${encodeURIComponent(this.continuationToken)}`);
        }
        if (this.ordering !== EnumerationOrder.CreatedDescending) {
            params.push(`ordering=${this.ordering}`);
        }
        if (this.labels && this.labels.length > 0) {
            params.push(`labels=${encodeURIComponent(this.labels.join(','))}`);
        }
        if (this.tags && Object.keys(this.tags).length > 0) {
            for (const [key, value] of Object.entries(this.tags)) {
                params.push(`tag.${encodeURIComponent(key)}=${encodeURIComponent(value)}`);
            }
        }
        return params.join('&');
    }
}

/**
 * Result container for paginated enumeration of collections.
 */
class EnumerationResult {
    /**
     * Create an EnumerationResult.
     * @param {object} data - Result data
     * @param {Function} itemParser - Function to parse each item
     */
    constructor(data, itemParser = (x) => x) {
        this.success = data.success !== false;
        this.timestamp = data.timestamp || null;
        this.maxResults = data.maxResults || 100;
        this.skip = data.skip || 0;
        this.iterationsRequired = data.iterationsRequired || 1;
        this.continuationToken = data.continuationToken || null;
        this.endOfResults = data.endOfResults || false;
        this.totalRecords = data.totalRecords || 0;
        this.recordsRemaining = data.recordsRemaining || 0;
        this.objects = (data.objects || []).map(itemParser);
    }

    /**
     * Returns true if there are more records available to fetch.
     * @returns {boolean}
     */
    get hasMore() {
        return !this.endOfResults && this.continuationToken !== null;
    }

    /**
     * Creates EnumerationOptions to fetch the next page.
     * @returns {EnumerationOptions|null} Options for next page, or null if at end
     */
    getNextPageOptions() {
        if (this.endOfResults || !this.continuationToken) {
            return null;
        }
        return new EnumerationOptions({
            maxResults: this.maxResults,
            continuationToken: this.continuationToken
        });
    }
}

/**
 * Verbex SDK Client for JavaScript.
 * Provides methods to interact with all Verbex REST API endpoints.
 * All methods return domain objects directly rather than wrapped responses.
 */
class VerbexClient {
    /**
     * Initialize the Verbex client.
     * @param {string} endpoint - The base URL of the Verbex server
     * @param {string} accessKey - The bearer token for authentication
     */
    constructor(endpoint, accessKey) {
        this._endpoint = endpoint.replace(/\/+$/, '');
        this._accessKey = accessKey;
    }

    /**
     * Get headers with authentication.
     * @returns {object} Headers object
     */
    _getAuthHeaders() {
        return {
            'Authorization': `Bearer ${this._accessKey}`,
            'Content-Type': 'application/json',
            'Accept': 'application/json'
        };
    }

    /**
     * Get headers without authentication.
     * @returns {object} Headers object
     */
    _getHeaders() {
        return {
            'Content-Type': 'application/json',
            'Accept': 'application/json'
        };
    }

    /**
     * Make an HTTP request to the API and return the data directly.
     * @param {string} method - HTTP method
     * @param {string} path - API path
     * @param {object} data - Request body data
     * @param {boolean} requireAuth - Whether to include auth headers
     * @returns {Promise<any>} Response data (unwrapped from ApiResponse)
     */
    async _makeRequest(method, path, data = null, requireAuth = true) {
        const url = `${this._endpoint}${path}`;
        const headers = requireAuth ? this._getAuthHeaders() : this._getHeaders();

        const options = {
            method,
            headers
        };

        if (data !== null && (method === 'POST' || method === 'PUT' || method === 'PATCH')) {
            options.body = JSON.stringify(data);
        }

        try {
            const response = await fetch(url, options);
            let responseData;

            try {
                responseData = await response.json();
            } catch {
                responseData = {
                    success: response.ok,
                    statusCode: response.status,
                    data: null
                };
            }

            // Handle both PascalCase and camelCase from server
            const success = responseData.Success ?? responseData.success ?? false;
            const statusCode = responseData.StatusCode ?? responseData.statusCode ?? response.status;
            const errorMessage = responseData.ErrorMessage ?? responseData.errorMessage;
            const rawData = responseData.Data ?? responseData.data;

            if (!success && statusCode >= 400) {
                throw new VerbexError(
                    errorMessage || `Request failed with status ${statusCode}`,
                    statusCode,
                    responseData
                );
            }

            // Return the data directly, converting keys to camelCase
            return rawData ? toCamelCaseKeys(rawData) : null;
        } catch (error) {
            if (error instanceof VerbexError) {
                throw error;
            }
            throw new VerbexError(`Request failed: ${error.message}`);
        }
    }

    /**
     * Make an HTTP HEAD request to check existence.
     * @param {string} path - API path
     * @param {boolean} requireAuth - Whether to include auth headers
     * @returns {Promise<boolean>} True if resource exists
     */
    async _makeHeadRequest(path, requireAuth = true) {
        const url = `${this._endpoint}${path}`;
        const headers = requireAuth ? this._getAuthHeaders() : this._getHeaders();

        try {
            const response = await fetch(url, { method: 'HEAD', headers });
            return response.ok;
        } catch (error) {
            throw new VerbexError(`Request failed: ${error.message}`);
        }
    }

    // ==================== Health Endpoints ====================

    /**
     * Check server health.
     * @returns {Promise<HealthData>} Health status data
     */
    async healthCheck() {
        const data = await this._makeRequest('GET', '/v1.0/health', null, false);
        return new HealthData(data || {});
    }

    /**
     * Check server health via root endpoint.
     * @returns {Promise<HealthData>} Health status data
     */
    async rootHealthCheck() {
        const data = await this._makeRequest('GET', '/', null, false);
        return new HealthData(data || {});
    }

    // ==================== Authentication Endpoints ====================

    /**
     * Authenticate with tenant ID, email, and password.
     * @param {string} tenantId - The tenant identifier
     * @param {string} email - The user's email address
     * @param {string} password - The user's password
     * @returns {Promise<LoginResult>} Login result indicating success or failure
     */
    async loginWithCredentials(tenantId, email, password) {
        if (!tenantId) throw new Error('tenantId is required');
        if (!email) throw new Error('email is required');
        if (!password) throw new Error('password is required');

        try {
            const data = await this._makeRequest('POST', '/v1.0/auth/login', {
                TenantId: tenantId,
                Username: email,
                Password: password
            }, false);

            if (data && data.token) {
                return LoginResult.successful(data.token, {
                    tenantId,
                    email,
                    isAdmin: data.isAdmin || false,
                    isGlobalAdmin: data.isGlobalAdmin || false
                });
            }

            return LoginResult.failed(
                AuthenticationResult.InvalidCredentials,
                AuthorizationResult.Unauthorized,
                'Login failed'
            );
        } catch (error) {
            if (error instanceof VerbexError) {
                const authResult = error.statusCode === 401 ? AuthenticationResult.InvalidCredentials :
                    error.statusCode === 403 ? AuthenticationResult.TenantAccessDenied :
                    error.statusCode === 404 ? AuthenticationResult.NotFound :
                    AuthenticationResult.NotAuthenticated;

                const authzResult = error.statusCode === 401 ? AuthorizationResult.Unauthorized :
                    error.statusCode === 403 ? AuthorizationResult.AccessDenied :
                    error.statusCode === 404 ? AuthorizationResult.ResourceNotFound :
                    AuthorizationResult.Unauthorized;

                return LoginResult.failed(authResult, authzResult, error.message);
            }
            throw error;
        }
    }

    /**
     * Authenticate with an existing bearer token by validating it against the server.
     * @param {string} bearerToken - The bearer token to validate and use
     * @returns {Promise<LoginResult>} Login result indicating success or failure
     */
    async loginWithToken(bearerToken) {
        if (!bearerToken) throw new Error('bearerToken is required');

        const originalAccessKey = this._accessKey;

        try {
            // Temporarily use the provided bearer token
            this._accessKey = bearerToken;

            const data = await this._makeRequest('GET', '/v1.0/auth/validate', null, true);

            if (data && data.valid) {
                return LoginResult.successful(bearerToken, {
                    tenantId: data.tenantId || null,
                    userId: data.userId || null,
                    email: data.email || null,
                    isAdmin: data.isAdmin || false,
                    isGlobalAdmin: data.isGlobalAdmin || false
                });
            }

            // Restore original access key on failure
            this._accessKey = originalAccessKey;

            return LoginResult.failed(
                AuthenticationResult.InvalidCredentials,
                AuthorizationResult.Unauthorized,
                'Bearer token validation failed'
            );
        } catch (error) {
            // Restore original access key on exception
            this._accessKey = originalAccessKey;

            if (error instanceof VerbexError) {
                const authResult = error.statusCode === 401 ? AuthenticationResult.InvalidCredentials :
                    error.statusCode === 403 ? AuthenticationResult.TenantAccessDenied :
                    AuthenticationResult.NotAuthenticated;

                return LoginResult.failed(authResult, AuthorizationResult.Unauthorized, error.message);
            }
            throw error;
        }
    }

    /**
     * Validate the current bearer token.
     * @returns {Promise<ValidationData>} Validation data
     */
    async validateToken() {
        const data = await this._makeRequest('GET', '/v1.0/auth/validate', null, true);
        return new ValidationData(data || {});
    }

    // ==================== Index Management Endpoints ====================

    /**
     * List indices with pagination support.
     * @param {EnumerationOptions} [options] - Pagination options
     * @returns {Promise<EnumerationResult>} EnumerationResult containing IndexInfo objects
     */
    async listIndices(options = null) {
        let path = '/v1.0/indices';
        if (options) {
            const queryString = options.toQueryString();
            if (queryString) {
                path += '?' + queryString;
            }
        }
        const data = await this._makeRequest('GET', path);
        return new EnumerationResult(data, (item) => new IndexInfo(item));
    }

    /**
     * List all indices by iterating through all pages.
     * @returns {AsyncGenerator<IndexInfo>} Async generator yielding IndexInfo objects
     */
    async *listAllIndices() {
        let options = new EnumerationOptions({ maxResults: 1000 });

        while (true) {
            const result = await this.listIndices(options);

            for (const item of result.objects) {
                yield item;
            }

            if (result.endOfResults || !result.continuationToken) {
                break;
            }

            options = result.getNextPageOptions();
        }
    }

    /**
     * Create a new index.
     * @param {object} options - Index creation options
     * @param {string} options.name - Display name for the index (required)
     * @param {string} [options.description] - Description
     * @param {boolean} [options.inMemory=false] - Use in-memory storage only
     * @param {boolean} [options.enableLemmatizer=false] - Enable lemmatization
     * @param {boolean} [options.enableStopWordRemover=false] - Enable stop word filtering
     * @param {number} [options.minTokenLength=0] - Minimum token length
     * @param {number} [options.maxTokenLength=0] - Maximum token length
     * @param {string[]} [options.labels] - Labels to associate with the index
     * @param {object} [options.tags] - Key-value tags to associate with the index
     * @param {object} [options.customMetadata] - Custom metadata to associate with the index
     * @param {CacheConfiguration|object} [options.cacheConfiguration] - Cache configuration for the index
     * @returns {Promise<IndexInfo>} Created index information
     */
    async createIndex(options) {
        const requestData = {
            Name: options.name,
            InMemory: options.inMemory || false,
            EnableLemmatizer: options.enableLemmatizer || false,
            EnableStopWordRemover: options.enableStopWordRemover || false,
            MinTokenLength: options.minTokenLength || 0,
            MaxTokenLength: options.maxTokenLength || 0
        };
        if (options.tenantId) {
            requestData.TenantId = options.tenantId;
        }
        if (options.description) {
            requestData.Description = options.description;
        }
        if (options.labels) {
            requestData.Labels = options.labels;
        }
        if (options.tags) {
            requestData.Tags = options.tags;
        }
        if (options.customMetadata) {
            requestData.CustomMetadata = options.customMetadata;
        }
        if (options.cacheConfiguration) {
            requestData.CacheConfiguration = {
                Enabled: options.cacheConfiguration.enabled || false,
                EnableTermCache: options.cacheConfiguration.enableTermCache !== undefined ? options.cacheConfiguration.enableTermCache : true,
                TermCacheCapacity: options.cacheConfiguration.termCacheCapacity || 10000,
                TermCacheEvictCount: options.cacheConfiguration.termCacheEvictCount || 100,
                TermCacheTtlSeconds: options.cacheConfiguration.termCacheTtlSeconds || 300,
                TermCacheSlidingExpiration: options.cacheConfiguration.termCacheSlidingExpiration !== undefined ? options.cacheConfiguration.termCacheSlidingExpiration : true,
                EnableDocumentCache: options.cacheConfiguration.enableDocumentCache !== undefined ? options.cacheConfiguration.enableDocumentCache : true,
                DocumentCacheCapacity: options.cacheConfiguration.documentCacheCapacity || 5000,
                DocumentCacheEvictCount: options.cacheConfiguration.documentCacheEvictCount || 50,
                DocumentCacheTtlSeconds: options.cacheConfiguration.documentCacheTtlSeconds || 600,
                DocumentCacheSlidingExpiration: options.cacheConfiguration.documentCacheSlidingExpiration !== undefined ? options.cacheConfiguration.documentCacheSlidingExpiration : true,
                EnableStatisticsCache: options.cacheConfiguration.enableStatisticsCache !== undefined ? options.cacheConfiguration.enableStatisticsCache : true,
                StatisticsCacheTtlSeconds: options.cacheConfiguration.statisticsCacheTtlSeconds || 60
            };
        }
        const data = await this._makeRequest('POST', '/v1.0/indices', requestData);
        return new IndexInfo(data?.index || {});
    }

    /**
     * Get detailed information about a specific index.
     * @param {string} indexId - The index identifier
     * @returns {Promise<IndexInfo>} Index information
     */
    async getIndex(indexId) {
        const data = await this._makeRequest('GET', `/v1.0/indices/${indexId}`);
        return new IndexInfo(data || {});
    }

    /**
     * Check if an index exists.
     * @param {string} indexId - The index identifier
     * @returns {Promise<boolean>} True if index exists, false otherwise
     */
    async indexExists(indexId) {
        return await this._makeHeadRequest(`/v1.0/indices/${indexId}`);
    }

    /**
     * Delete an index.
     * @param {string} indexId - The index identifier
     * @returns {Promise<void>}
     */
    async deleteIndex(indexId) {
        await this._makeRequest('DELETE', `/v1.0/indices/${indexId}`);
    }

    /**
     * Update labels on an index (full replacement).
     * @param {string} indexId - The index identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<void>}
     */
    async updateIndexLabels(indexId, labels) {
        await this._makeRequest('PUT', `/v1.0/indices/${indexId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on an index (full replacement).
     * @param {string} indexId - The index identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<void>}
     */
    async updateIndexTags(indexId, tags) {
        await this._makeRequest('PUT', `/v1.0/indices/${indexId}/tags`, { Tags: tags || {} });
    }

    /**
     * Update custom metadata on an index (full replacement).
     * @param {string} indexId - The index identifier
     * @param {object} customMetadata - The new custom metadata to set
     * @returns {Promise<IndexInfo>} Updated index information
     */
    async updateIndexCustomMetadata(indexId, customMetadata) {
        const data = await this._makeRequest('PUT', `/v1.0/indices/${indexId}/customMetadata`, { CustomMetadata: customMetadata });
        return new IndexInfo(data || {});
    }

    // ==================== Document Management Endpoints ====================

    /**
     * List documents in an index with pagination support.
     * @param {string} indexId - The index identifier
     * @param {EnumerationOptions} [options] - Pagination options
     * @returns {Promise<EnumerationResult>} EnumerationResult containing DocumentInfo objects
     */
    async listDocuments(indexId, options = null) {
        let path = `/v1.0/indices/${indexId}/documents`;
        if (options) {
            const queryString = options.toQueryString();
            if (queryString) {
                path += '?' + queryString;
            }
        }
        const data = await this._makeRequest('GET', path);
        return new EnumerationResult(data, (item) => new DocumentInfo(item));
    }

    /**
     * List all documents in an index by iterating through all pages.
     * @param {string} indexId - The index identifier
     * @returns {AsyncGenerator<DocumentInfo>} Async generator yielding DocumentInfo objects
     */
    async *listAllDocuments(indexId) {
        let options = new EnumerationOptions({ maxResults: 1000 });

        while (true) {
            const result = await this.listDocuments(indexId, options);

            for (const item of result.objects) {
                yield item;
            }

            if (result.endOfResults || !result.continuationToken) {
                break;
            }

            options = result.getNextPageOptions();
        }
    }

    /**
     * Add a document to an index.
     * @param {string} indexId - The index identifier
     * @param {string} content - The document content
     * @param {string} [documentId] - Optional document ID (GUID)
     * @param {string[]} [labels] - Optional labels to associate with the document
     * @param {object} [tags] - Optional key-value tags to associate with the document
     * @param {object} [customMetadata] - Optional custom metadata to associate with the document
     * @returns {Promise<AddDocumentData>} Created document data including document ID
     */
    async addDocument(indexId, content, documentId = null, labels = null, tags = null, customMetadata = null) {
        const requestData = { Content: content };
        if (documentId) {
            requestData.Id = documentId;
        }
        if (labels) {
            requestData.Labels = labels;
        }
        if (tags) {
            requestData.Tags = tags;
        }
        if (customMetadata) {
            requestData.CustomMetadata = customMetadata;
        }
        const data = await this._makeRequest('POST', `/v1.0/indices/${indexId}/documents`, requestData);
        return new AddDocumentData(data || {});
    }

    /**
     * Get a specific document.
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @returns {Promise<DocumentInfo>} Document information
     */
    async getDocument(indexId, documentId) {
        const data = await this._makeRequest('GET', `/v1.0/indices/${indexId}/documents/${documentId}`);
        return new DocumentInfo(data || {});
    }

    /**
     * Get multiple documents by IDs from an index in a single request.
     * @param {string} indexId - The index identifier
     * @param {string[]} documentIds - Array of document IDs to retrieve
     * @returns {Promise<BatchDocumentsResult>} Batch result containing found documents and list of not found IDs
     */
    async getDocumentsBatch(indexId, documentIds) {
        if (!documentIds || documentIds.length === 0) {
            return new BatchDocumentsResult({});
        }
        // Join IDs with commas - encode individual IDs but not the commas
        const idsParam = documentIds.map(id => encodeURIComponent(id)).join(',');
        const data = await this._makeRequest('GET', `/v1.0/indices/${indexId}/documents?ids=${idsParam}`);
        return new BatchDocumentsResult(data || {});
    }

    /**
     * Check if a document exists in an index.
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @returns {Promise<boolean>} True if document exists, false otherwise
     */
    async documentExists(indexId, documentId) {
        return await this._makeHeadRequest(`/v1.0/indices/${indexId}/documents/${documentId}`);
    }

    /**
     * Delete a document from an index.
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @returns {Promise<void>}
     */
    async deleteDocument(indexId, documentId) {
        await this._makeRequest('DELETE', `/v1.0/indices/${indexId}/documents/${documentId}`);
    }

    /**
     * Delete multiple documents from an index by IDs in a single request.
     * @param {string} indexId - The index identifier
     * @param {string[]} documentIds - Array of document IDs to delete
     * @returns {Promise<BatchDeleteResult>} Batch result containing lists of deleted and not found IDs
     */
    async deleteDocumentsBatch(indexId, documentIds) {
        if (!documentIds || documentIds.length === 0) {
            return new BatchDeleteResult({});
        }
        // Join IDs with commas - encode individual IDs but not the commas
        const idsParam = documentIds.map(id => encodeURIComponent(id)).join(',');
        const data = await this._makeRequest('DELETE', `/v1.0/indices/${indexId}/documents?ids=${idsParam}`);
        return new BatchDeleteResult(data || {});
    }

    /**
     * Add multiple documents to an index in a single request.
     * @param {string} indexId - The index identifier
     * @param {Array<{name: string, content: string, id?: string, labels?: string[], tags?: object, customMetadata?: object}>} documents - Array of documents to add
     * @returns {Promise<BatchAddResult>} Batch result containing lists of added and failed documents
     */
    async addDocumentsBatch(indexId, documents) {
        if (!documents || documents.length === 0) {
            return new BatchAddResult({});
        }
        // Convert to the expected request format
        const requestDocs = documents.map(doc => ({
            Id: doc.id || null,
            Name: doc.name,
            Content: doc.content,
            Labels: doc.labels || [],
            Tags: doc.tags || {},
            CustomMetadata: doc.customMetadata || null
        }));
        const data = await this._makeRequest('POST', `/v1.0/indices/${indexId}/documents/batch`, { Documents: requestDocs });
        return new BatchAddResult(data || {});
    }

    /**
     * Check if multiple documents exist in an index.
     * @param {string} indexId - The index identifier
     * @param {string[]} documentIds - Array of document IDs to check
     * @returns {Promise<BatchExistenceResult>} Batch result containing lists of existing and not found IDs
     */
    async checkDocumentsExist(indexId, documentIds) {
        if (!documentIds || documentIds.length === 0) {
            return new BatchExistenceResult({});
        }
        const data = await this._makeRequest('POST', `/v1.0/indices/${indexId}/documents/exists`, { Ids: documentIds });
        return new BatchExistenceResult(data || {});
    }

    /**
     * Update labels on a document (full replacement).
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<void>}
     */
    async updateDocumentLabels(indexId, documentId, labels) {
        await this._makeRequest('PUT', `/v1.0/indices/${indexId}/documents/${documentId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on a document (full replacement).
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<void>}
     */
    async updateDocumentTags(indexId, documentId, tags) {
        await this._makeRequest('PUT', `/v1.0/indices/${indexId}/documents/${documentId}/tags`, { Tags: tags || {} });
    }

    /**
     * Update custom metadata on a document (full replacement).
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @param {object} customMetadata - The new custom metadata to set
     * @returns {Promise<DocumentInfo>} Updated document information
     */
    async updateDocumentCustomMetadata(indexId, documentId, customMetadata) {
        const data = await this._makeRequest('PUT', `/v1.0/indices/${indexId}/documents/${documentId}/customMetadata`, { CustomMetadata: customMetadata });
        return new DocumentInfo(data || {});
    }

    // ==================== Search Endpoint ====================

    /**
     * Search documents in an index with optional label and tag filters.
     * Use "*" as the query to return all documents (optionally filtered by labels/tags) without term matching.
     * Wildcard results have a score of 0.
     * @param {string} indexId - The index identifier
     * @param {string} query - The search query. Use "*" for wildcard (all documents).
     * @param {number} [maxResults=100] - Maximum results to return
     * @param {string[]} [labels=null] - Optional labels to filter by (AND logic, case-insensitive)
     * @param {Object} [tags=null] - Optional tags to filter by (AND logic, exact match)
     * @returns {Promise<SearchData>} Search results
     */
    async search(indexId, query, maxResults = 100, labels = null, tags = null) {
        const requestData = { Query: query, MaxResults: maxResults };
        if (labels && labels.length > 0) {
            requestData.Labels = labels;
        }
        if (tags && Object.keys(tags).length > 0) {
            requestData.Tags = tags;
        }
        const data = await this._makeRequest('POST', `/v1.0/indices/${indexId}/search`, requestData);
        return new SearchData(data || {});
    }

    // ==================== Terms Endpoints ====================

    /**
     * Get the top terms in an index sorted by document frequency.
     * @param {string} indexId - The index identifier
     * @param {number} [limit=10] - Maximum number of terms to return
     * @returns {Promise<Object>} Object mapping terms to their document frequencies
     */
    async getTopTerms(indexId, limit = 10) {
        let endpoint = `/v1.0/indices/${indexId}/terms/top`;
        if (limit !== 10) {
            endpoint += `?limit=${limit}`;
        }
        const data = await this._makeRequest('GET', endpoint);
        return data || {};
    }

    // ==================== Backup & Restore Endpoints ====================

    /**
     * Create a backup of an index.
     * @param {string} indexId - The index identifier
     * @returns {Promise<Blob>} Blob containing the backup ZIP archive
     */
    async backup(indexId) {
        if (!indexId) throw new Error('indexId is required');

        const url = `${this._endpoint}/v1.0/indices/${indexId}/backup`;
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${this._accessKey}`
            }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new VerbexError(`Backup failed: ${errorText}`, response.status);
        }

        return await response.blob();
    }

    /**
     * Create a backup of an index and trigger a file download.
     * @param {string} indexId - The index identifier
     * @param {string} [filename] - Optional filename (default: backup-{indexId}-{timestamp}.vbx)
     */
    async backupToFile(indexId, filename = null) {
        const blob = await this.backup(indexId);
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
        const downloadFilename = filename || `backup-${indexId}-${timestamp}.vbx`;

        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = downloadFilename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
    }

    /**
     * Restore a backup to create a new index.
     * @param {File|Blob} file - The backup file
     * @param {object} [options] - Restore options
     * @param {string} [options.name] - Optional new name for the restored index
     * @param {string} [options.indexId] - Optional specific ID for the new index
     * @returns {Promise<object>} Restore result including indexId
     */
    async restore(file, options = {}) {
        if (!file) throw new Error('file is required');

        const url = `${this._endpoint}/v1.0/indices/restore`;
        const formData = new FormData();
        formData.append('file', file);

        if (options.name) {
            formData.append('name', options.name);
        }
        if (options.indexId) {
            formData.append('indexId', options.indexId);
        }

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${this._accessKey}`
            },
            body: formData
        });

        const responseData = await response.json();

        if (!response.ok) {
            const errorMessage = responseData.ErrorMessage || responseData.errorMessage || 'Restore failed';
            throw new VerbexError(errorMessage, response.status, responseData);
        }

        const rawData = responseData.Data || responseData.data;
        return toCamelCaseKeys(rawData || {});
    }

    /**
     * Restore a backup by replacing an existing index.
     * @param {string} indexId - The index identifier to replace
     * @param {File|Blob} file - The backup file
     * @returns {Promise<object>} Restore result
     */
    async restoreReplace(indexId, file) {
        if (!indexId) throw new Error('indexId is required');
        if (!file) throw new Error('file is required');

        const url = `${this._endpoint}/v1.0/indices/${indexId}/restore`;
        const formData = new FormData();
        formData.append('file', file);

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${this._accessKey}`
            },
            body: formData
        });

        const responseData = await response.json();

        if (!response.ok) {
            const errorMessage = responseData.ErrorMessage || responseData.errorMessage || 'Restore failed';
            throw new VerbexError(errorMessage, response.status, responseData);
        }

        const rawData = responseData.Data || responseData.data;
        return toCamelCaseKeys(rawData || {});
    }

    // ==================== Admin - Tenant Management Endpoints ====================

    /**
     * List tenants with pagination support.
     * @param {EnumerationOptions} [options] - Pagination options
     * @returns {Promise<EnumerationResult>} EnumerationResult containing TenantInfo objects
     */
    async listTenants(options = null) {
        let path = '/v1.0/tenants';
        if (options) {
            const queryString = options.toQueryString();
            if (queryString) {
                path += '?' + queryString;
            }
        }
        const data = await this._makeRequest('GET', path);
        return new EnumerationResult(data, (item) => new TenantInfo(item));
    }

    /**
     * List all tenants by iterating through all pages.
     * @returns {AsyncGenerator<TenantInfo>} Async generator yielding TenantInfo objects
     */
    async *listAllTenants() {
        let options = new EnumerationOptions({ maxResults: 1000 });

        while (true) {
            const result = await this.listTenants(options);

            for (const item of result.objects) {
                yield item;
            }

            if (result.endOfResults || !result.continuationToken) {
                break;
            }

            options = result.getNextPageOptions();
        }
    }

    /**
     * Get a specific tenant.
     * @param {string} tenantId - The tenant identifier
     * @returns {Promise<TenantInfo>} Tenant information
     */
    async getTenant(tenantId) {
        const data = await this._makeRequest('GET', `/v1.0/admin/tenants/${tenantId}`);
        return new TenantInfo(data || {});
    }

    /**
     * Create a new tenant.
     * @param {object} options - Tenant creation options
     * @param {string} options.name - Tenant name
     * @param {string} [options.description] - Optional description
     * @returns {Promise<TenantInfo>} Created tenant information
     */
    async createTenant(options) {
        const requestData = { name: options.name };
        if (options.description) {
            requestData.description = options.description;
        }
        const data = await this._makeRequest('POST', '/v1.0/admin/tenants', requestData);
        return new TenantInfo(data?.tenant || {});
    }

    /**
     * Delete a tenant.
     * @param {string} tenantId - The tenant identifier
     * @returns {Promise<void>}
     */
    async deleteTenant(tenantId) {
        await this._makeRequest('DELETE', `/v1.0/admin/tenants/${tenantId}`);
    }

    /**
     * Update labels on a tenant (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<void>}
     */
    async updateTenantLabels(tenantId, labels) {
        await this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on a tenant (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<void>}
     */
    async updateTenantTags(tenantId, tags) {
        await this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/tags`, { Tags: tags || {} });
    }

    // ==================== Admin - User Management Endpoints ====================

    /**
     * List users in a tenant with pagination support.
     * @param {string} tenantId - The tenant identifier
     * @param {EnumerationOptions} [options] - Pagination options
     * @returns {Promise<EnumerationResult>} EnumerationResult containing UserInfo objects
     */
    async listUsers(tenantId, options = null) {
        let path = `/v1.0/tenants/${tenantId}/users`;
        if (options) {
            const queryString = options.toQueryString();
            if (queryString) {
                path += '?' + queryString;
            }
        }
        const data = await this._makeRequest('GET', path);
        return new EnumerationResult(data, (item) => new UserInfo(item));
    }

    /**
     * List all users in a tenant by iterating through all pages.
     * @param {string} tenantId - The tenant identifier
     * @returns {AsyncGenerator<UserInfo>} Async generator yielding UserInfo objects
     */
    async *listAllUsers(tenantId) {
        let options = new EnumerationOptions({ maxResults: 1000 });

        while (true) {
            const result = await this.listUsers(tenantId, options);

            for (const item of result.objects) {
                yield item;
            }

            if (result.endOfResults || !result.continuationToken) {
                break;
            }

            options = result.getNextPageOptions();
        }
    }

    /**
     * Get a specific user.
     * @param {string} tenantId - The tenant identifier
     * @param {string} userId - The user identifier
     * @returns {Promise<UserInfo>} User information
     */
    async getUser(tenantId, userId) {
        const data = await this._makeRequest('GET', `/v1.0/admin/tenants/${tenantId}/users/${userId}`);
        return new UserInfo(data || {});
    }

    /**
     * Create a new user in a tenant.
     * @param {string} tenantId - The tenant identifier
     * @param {object} options - User creation options
     * @param {string} options.email - User email
     * @param {string} options.password - User password
     * @param {string} [options.firstName] - Optional first name
     * @param {string} [options.lastName] - Optional last name
     * @param {boolean} [options.isAdmin=false] - Whether user is tenant admin
     * @returns {Promise<UserInfo>} Created user information
     */
    async createUser(tenantId, options) {
        const requestData = {
            email: options.email,
            password: options.password
        };
        if (options.firstName) {
            requestData.firstName = options.firstName;
        }
        if (options.lastName) {
            requestData.lastName = options.lastName;
        }
        if (options.isAdmin !== undefined) {
            requestData.isAdmin = options.isAdmin;
        }
        const data = await this._makeRequest('POST', `/v1.0/admin/tenants/${tenantId}/users`, requestData);
        return new UserInfo(data?.user || {});
    }

    /**
     * Delete a user.
     * @param {string} tenantId - The tenant identifier
     * @param {string} userId - The user identifier
     * @returns {Promise<void>}
     */
    async deleteUser(tenantId, userId) {
        await this._makeRequest('DELETE', `/v1.0/admin/tenants/${tenantId}/users/${userId}`);
    }

    /**
     * Update labels on a user (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string} userId - The user identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<void>}
     */
    async updateUserLabels(tenantId, userId, labels) {
        await this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/users/${userId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on a user (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string} userId - The user identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<void>}
     */
    async updateUserTags(tenantId, userId, tags) {
        await this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/users/${userId}/tags`, { Tags: tags || {} });
    }

    // ==================== Admin - Credential Management Endpoints ====================

    /**
     * List credentials in a tenant with pagination support.
     * @param {string} tenantId - The tenant identifier
     * @param {EnumerationOptions} [options] - Pagination options
     * @returns {Promise<EnumerationResult>} EnumerationResult containing CredentialInfo objects
     */
    async listCredentials(tenantId, options = null) {
        let path = `/v1.0/tenants/${tenantId}/credentials`;
        if (options) {
            const queryString = options.toQueryString();
            if (queryString) {
                path += '?' + queryString;
            }
        }
        const data = await this._makeRequest('GET', path);
        return new EnumerationResult(data, (item) => new CredentialInfo(item));
    }

    /**
     * List all credentials in a tenant by iterating through all pages.
     * @param {string} tenantId - The tenant identifier
     * @returns {AsyncGenerator<CredentialInfo>} Async generator yielding CredentialInfo objects
     */
    async *listAllCredentials(tenantId) {
        let options = new EnumerationOptions({ maxResults: 1000 });

        while (true) {
            const result = await this.listCredentials(tenantId, options);

            for (const item of result.objects) {
                yield item;
            }

            if (result.endOfResults || !result.continuationToken) {
                break;
            }

            options = result.getNextPageOptions();
        }
    }

    /**
     * Get a specific credential.
     * @param {string} tenantId - The tenant identifier
     * @param {string} credentialId - The credential identifier
     * @returns {Promise<CredentialInfo>} Credential information
     */
    async getCredential(tenantId, credentialId) {
        const data = await this._makeRequest('GET', `/v1.0/admin/tenants/${tenantId}/credentials/${credentialId}`);
        return new CredentialInfo(data || {});
    }

    /**
     * Create a new credential (API key) in a tenant.
     * @param {string} tenantId - The tenant identifier
     * @param {object} [options] - Credential creation options
     * @param {string} [options.description] - Optional description
     * @returns {Promise<CredentialInfo>} Created credential information (includes bearer token)
     */
    async createCredential(tenantId, options = {}) {
        const requestData = {};
        if (options.description) {
            requestData.description = options.description;
        }
        const data = await this._makeRequest('POST', `/v1.0/admin/tenants/${tenantId}/credentials`, requestData);
        return new CredentialInfo(data?.credential || {});
    }

    /**
     * Delete a credential.
     * @param {string} tenantId - The tenant identifier
     * @param {string} credentialId - The credential identifier
     * @returns {Promise<void>}
     */
    async deleteCredential(tenantId, credentialId) {
        await this._makeRequest('DELETE', `/v1.0/admin/tenants/${tenantId}/credentials/${credentialId}`);
    }

    /**
     * Update labels on a credential (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string} credentialId - The credential identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<void>}
     */
    async updateCredentialLabels(tenantId, credentialId, labels) {
        await this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/credentials/${credentialId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on a credential (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string} credentialId - The credential identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<void>}
     */
    async updateCredentialTags(tenantId, credentialId, tags) {
        await this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/credentials/${credentialId}/tags`, { Tags: tags || {} });
    }
}

// Export for Node.js
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        VerbexClient,
        VerbexError,
        AuthenticationResult,
        AuthorizationResult,
        LoginResult,
        IndexInfo,
        IndexStatistics,
        DocumentInfo,
        AddDocumentData,
        BatchDocumentsResult,
        BatchDeleteResult,
        BatchAddResultItem,
        BatchAddResult,
        BatchExistenceResult,
        SearchResult,
        SearchData,
        HealthData,
        ValidationData,
        TenantInfo,
        UserInfo,
        CredentialInfo,
        CacheConfiguration,
        CacheStats,
        VerbexCacheStatistics,
        EnumerationOrder,
        EnumerationOptions,
        EnumerationResult
    };
}

// Export for ES modules
if (typeof exports !== 'undefined') {
    exports.VerbexClient = VerbexClient;
    exports.VerbexError = VerbexError;
    exports.AuthenticationResult = AuthenticationResult;
    exports.AuthorizationResult = AuthorizationResult;
    exports.LoginResult = LoginResult;
    exports.IndexInfo = IndexInfo;
    exports.IndexStatistics = IndexStatistics;
    exports.DocumentInfo = DocumentInfo;
    exports.AddDocumentData = AddDocumentData;
    exports.BatchDocumentsResult = BatchDocumentsResult;
    exports.BatchDeleteResult = BatchDeleteResult;
    exports.BatchAddResultItem = BatchAddResultItem;
    exports.BatchAddResult = BatchAddResult;
    exports.BatchExistenceResult = BatchExistenceResult;
    exports.SearchResult = SearchResult;
    exports.SearchData = SearchData;
    exports.HealthData = HealthData;
    exports.ValidationData = ValidationData;
    exports.TenantInfo = TenantInfo;
    exports.UserInfo = UserInfo;
    exports.CredentialInfo = CredentialInfo;
    exports.CacheConfiguration = CacheConfiguration;
    exports.CacheStats = CacheStats;
    exports.VerbexCacheStatistics = VerbexCacheStatistics;
    exports.EnumerationOrder = EnumerationOrder;
    exports.EnumerationOptions = EnumerationOptions;
    exports.EnumerationResult = EnumerationResult;
}
