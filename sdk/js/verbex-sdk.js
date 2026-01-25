/**
 * Verbex JavaScript SDK
 * A comprehensive SDK for interacting with the Verbex Inverted Index REST API.
 */

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
 * API Response wrapper class.
 */
class ApiResponse {
    /**
     * Create an ApiResponse.
     * @param {object} data - Response data
     */
    constructor(data) {
        // Server returns PascalCase properties
        this.guid = data.Guid || data.guid || null;
        this.success = data.Success || data.success || false;
        this.timestampUtc = data.TimestampUtc || data.timestampUtc || null;
        this.statusCode = data.StatusCode || data.statusCode || 0;
        this.errorMessage = data.ErrorMessage || data.errorMessage || null;
        // Convert data object keys to camelCase for convenience
        const rawData = data.Data || data.data || null;
        this.data = rawData ? toCamelCaseKeys(rawData) : null;
        this.totalCount = data.TotalCount || data.totalCount || null;
        this.processingTimeMs = data.ProcessingTimeMs || data.processingTimeMs || null;
        this.rawResponse = data;
    }
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
        this.statistics = data.statistics || null;
        this.customMetadata = data.customMetadata || null;
        this.labels = data.labels || null;
        this.tags = data.tags || null;
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
class SearchResponse {
    /**
     * Create a SearchResponse.
     * @param {object} data - Search response data (camelCase from toCamelCaseKeys)
     */
    constructor(data) {
        this.query = data.query || '';
        this.results = (data.results || []).map(r => new SearchResult(r));
        this.totalCount = data.totalCount || 0;
        this.maxResults = data.maxResults || 100;
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
 * Verbex SDK Client for JavaScript.
 * Provides methods to interact with all Verbex REST API endpoints.
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
     * Make an HTTP request to the API.
     * @param {string} method - HTTP method
     * @param {string} path - API path
     * @param {object} data - Request body data
     * @param {boolean} requireAuth - Whether to include auth headers
     * @returns {Promise<ApiResponse>} API response
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

            const apiResponse = new ApiResponse(responseData);

            if (!apiResponse.success && apiResponse.statusCode >= 400) {
                throw new VerbexError(
                    apiResponse.errorMessage || `Request failed with status ${apiResponse.statusCode}`,
                    apiResponse.statusCode,
                    apiResponse
                );
            }

            return apiResponse;
        } catch (error) {
            if (error instanceof VerbexError) {
                throw error;
            }
            throw new VerbexError(`Request failed: ${error.message}`);
        }
    }

    // ==================== Health Endpoints ====================

    /**
     * Check server health.
     * @returns {Promise<ApiResponse>} Health status response
     */
    async healthCheck() {
        return this._makeRequest('GET', '/v1.0/health', null, false);
    }

    /**
     * Check server health via root endpoint.
     * @returns {Promise<ApiResponse>} Health status response
     */
    async rootHealthCheck() {
        return this._makeRequest('GET', '/', null, false);
    }

    // ==================== Authentication Endpoints ====================

    /**
     * Authenticate and receive a bearer token.
     * @param {string} username - The username
     * @param {string} password - The password
     * @returns {Promise<ApiResponse>} Login response with token
     */
    async login(username, password) {
        return this._makeRequest('POST', '/v1.0/auth/login', { Username: username, Password: password }, false);
    }

    /**
     * Validate the current bearer token.
     * @returns {Promise<ApiResponse>} Validation response
     */
    async validateToken() {
        return this._makeRequest('GET', '/v1.0/auth/validate', null, true);
    }

    // ==================== Index Management Endpoints ====================

    /**
     * List all available indices.
     * @returns {Promise<ApiResponse>} List of indices
     */
    async listIndices() {
        return this._makeRequest('GET', '/v1.0/indices');
    }

    /**
     * Get all indices as IndexInfo objects.
     * @returns {Promise<IndexInfo[]>} Array of IndexInfo objects
     */
    async getIndices() {
        const response = await this.listIndices();
        if (response.data?.indices) {
            return response.data.indices.map(idx => new IndexInfo(idx));
        }
        return [];
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
     * @returns {Promise<ApiResponse>} Created index response
     */
    async createIndex(options) {
        const data = {
            Name: options.name,
            InMemory: options.inMemory || false,
            EnableLemmatizer: options.enableLemmatizer || false,
            EnableStopWordRemover: options.enableStopWordRemover || false,
            MinTokenLength: options.minTokenLength || 0,
            MaxTokenLength: options.maxTokenLength || 0
        };
        if (options.description) {
            data.Description = options.description;
        }
        if (options.labels) {
            data.Labels = options.labels;
        }
        if (options.tags) {
            data.Tags = options.tags;
        }
        if (options.customMetadata) {
            data.CustomMetadata = options.customMetadata;
        }
        return this._makeRequest('POST', '/v1.0/indices', data);
    }

    /**
     * Get detailed information about a specific index.
     * @param {string} indexId - The index identifier
     * @returns {Promise<ApiResponse>} Index details
     */
    async getIndex(indexId) {
        return this._makeRequest('GET', `/v1.0/indices/${indexId}`);
    }

    /**
     * Get index as IndexInfo object.
     * @param {string} indexId - The index identifier
     * @returns {Promise<IndexInfo>} IndexInfo object
     */
    async getIndexInfo(indexId) {
        const response = await this.getIndex(indexId);
        return response.data ? new IndexInfo(response.data) : null;
    }

    /**
     * Check if an index exists.
     * @param {string} indexId - The index identifier
     * @returns {Promise<boolean>} True if index exists, false otherwise
     */
    async indexExists(indexId) {
        try {
            await this._makeRequest('HEAD', `/v1.0/indices/${indexId}`);
            return true;
        } catch (error) {
            if (error instanceof VerbexError && error.statusCode === 404) {
                return false;
            }
            throw error;
        }
    }

    /**
     * Delete an index.
     * @param {string} indexId - The index identifier
     * @returns {Promise<ApiResponse>} Deletion confirmation
     */
    async deleteIndex(indexId) {
        return this._makeRequest('DELETE', `/v1.0/indices/${indexId}`);
    }

    /**
     * Update labels on an index (full replacement).
     * @param {string} indexId - The index identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated index
     */
    async updateIndexLabels(indexId, labels) {
        return this._makeRequest('PUT', `/v1.0/indices/${indexId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on an index (full replacement).
     * @param {string} indexId - The index identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated index
     */
    async updateIndexTags(indexId, tags) {
        return this._makeRequest('PUT', `/v1.0/indices/${indexId}/tags`, { Tags: tags || {} });
    }

    /**
     * Update custom metadata on an index (full replacement).
     * @param {string} indexId - The index identifier
     * @param {object} customMetadata - The new custom metadata to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated index
     */
    async updateIndexCustomMetadata(indexId, customMetadata) {
        return this._makeRequest('PUT', `/v1.0/indices/${indexId}/customMetadata`, { CustomMetadata: customMetadata });
    }

    // ==================== Document Management Endpoints ====================

    /**
     * List all documents in an index.
     * @param {string} indexId - The index identifier
     * @returns {Promise<ApiResponse>} List of documents
     */
    async listDocuments(indexId) {
        return this._makeRequest('GET', `/v1.0/indices/${indexId}/documents`);
    }

    /**
     * Get all documents as DocumentInfo objects.
     * @param {string} indexId - The index identifier
     * @returns {Promise<DocumentInfo[]>} Array of DocumentInfo objects
     */
    async getDocuments(indexId) {
        const response = await this.listDocuments(indexId);
        if (response.data?.documents) {
            return response.data.documents.map(doc => new DocumentInfo(doc));
        }
        return [];
    }

    /**
     * Add a document to an index.
     * @param {string} indexId - The index identifier
     * @param {string} content - The document content
     * @param {string} [documentId] - Optional document ID (GUID)
     * @param {string[]} [labels] - Optional labels to associate with the document
     * @param {object} [tags] - Optional key-value tags to associate with the document
     * @param {object} [customMetadata] - Optional custom metadata to associate with the document
     * @returns {Promise<ApiResponse>} Document creation response
     */
    async addDocument(indexId, content, documentId = null, labels = null, tags = null, customMetadata = null) {
        const data = { Content: content };
        if (documentId) {
            data.Id = documentId;
        }
        if (labels) {
            data.Labels = labels;
        }
        if (tags) {
            data.Tags = tags;
        }
        if (customMetadata) {
            data.CustomMetadata = customMetadata;
        }
        return this._makeRequest('POST', `/v1.0/indices/${indexId}/documents`, data);
    }

    /**
     * Get a specific document.
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @returns {Promise<ApiResponse>} Document details
     */
    async getDocument(indexId, documentId) {
        return this._makeRequest('GET', `/v1.0/indices/${indexId}/documents/${documentId}`);
    }

    /**
     * Get document as DocumentInfo object.
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @returns {Promise<DocumentInfo>} DocumentInfo object
     */
    async getDocumentInfo(indexId, documentId) {
        const response = await this.getDocument(indexId, documentId);
        return response.data ? new DocumentInfo(response.data) : null;
    }

    /**
     * Check if a document exists in an index.
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @returns {Promise<boolean>} True if document exists, false otherwise
     */
    async documentExists(indexId, documentId) {
        try {
            await this._makeRequest('HEAD', `/v1.0/indices/${indexId}/documents/${documentId}`);
            return true;
        } catch (error) {
            if (error instanceof VerbexError && error.statusCode === 404) {
                return false;
            }
            throw error;
        }
    }

    /**
     * Delete a document from an index.
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @returns {Promise<ApiResponse>} Deletion confirmation
     */
    async deleteDocument(indexId, documentId) {
        return this._makeRequest('DELETE', `/v1.0/indices/${indexId}/documents/${documentId}`);
    }

    /**
     * Update labels on a document (full replacement).
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated document
     */
    async updateDocumentLabels(indexId, documentId, labels) {
        return this._makeRequest('PUT', `/v1.0/indices/${indexId}/documents/${documentId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on a document (full replacement).
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated document
     */
    async updateDocumentTags(indexId, documentId, tags) {
        return this._makeRequest('PUT', `/v1.0/indices/${indexId}/documents/${documentId}/tags`, { Tags: tags || {} });
    }

    /**
     * Update custom metadata on a document (full replacement).
     * @param {string} indexId - The index identifier
     * @param {string} documentId - The document identifier
     * @param {object} customMetadata - The new custom metadata to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated document
     */
    async updateDocumentCustomMetadata(indexId, documentId, customMetadata) {
        return this._makeRequest('PUT', `/v1.0/indices/${indexId}/documents/${documentId}/customMetadata`, { CustomMetadata: customMetadata });
    }

    // ==================== Search Endpoint ====================

    /**
     * Search documents in an index with optional label and tag filters.
     * @param {string} indexId - The index identifier
     * @param {string} query - The search query
     * @param {number} [maxResults=100] - Maximum results to return
     * @param {string[]} [labels=null] - Optional labels to filter by (AND logic, case-insensitive)
     * @param {Object} [tags=null] - Optional tags to filter by (AND logic, exact match)
     * @returns {Promise<ApiResponse>} Search results
     */
    async search(indexId, query, maxResults = 100, labels = null, tags = null) {
        const data = { Query: query, MaxResults: maxResults };
        if (labels && labels.length > 0) {
            data.Labels = labels;
        }
        if (tags && Object.keys(tags).length > 0) {
            data.Tags = tags;
        }
        return this._makeRequest('POST', `/v1.0/indices/${indexId}/search`, data);
    }

    /**
     * Search documents and return SearchResponse object with optional filters.
     * @param {string} indexId - The index identifier
     * @param {string} query - The search query
     * @param {number} [maxResults=100] - Maximum results to return
     * @param {string[]} [labels=null] - Optional labels to filter by (AND logic, case-insensitive)
     * @param {Object} [tags=null] - Optional tags to filter by (AND logic, exact match)
     * @returns {Promise<SearchResponse>} SearchResponse object
     */
    async searchDocuments(indexId, query, maxResults = 100, labels = null, tags = null) {
        const response = await this.search(indexId, query, maxResults, labels, tags);
        return response.data ? new SearchResponse(response.data) : null;
    }

    // ==================== Admin - Tenant Management Endpoints ====================

    /**
     * List all tenants.
     * @returns {Promise<ApiResponse>} List of tenants
     */
    async listTenants() {
        return this._makeRequest('GET', '/v1.0/admin/tenants');
    }

    /**
     * Get all tenants as TenantInfo objects.
     * @returns {Promise<TenantInfo[]>} Array of TenantInfo objects
     */
    async getTenants() {
        const response = await this.listTenants();
        if (response.data?.tenants) {
            return response.data.tenants.map(t => new TenantInfo(t));
        }
        return [];
    }

    /**
     * Get a specific tenant.
     * @param {string} tenantId - The tenant identifier
     * @returns {Promise<ApiResponse>} Tenant details
     */
    async getTenant(tenantId) {
        return this._makeRequest('GET', `/v1.0/admin/tenants/${tenantId}`);
    }

    /**
     * Create a new tenant.
     * @param {object} options - Tenant creation options
     * @param {string} options.name - Tenant name
     * @param {string} [options.description] - Optional description
     * @returns {Promise<ApiResponse>} Created tenant response
     */
    async createTenant(options) {
        const data = { name: options.name };
        if (options.description) {
            data.description = options.description;
        }
        return this._makeRequest('POST', '/v1.0/admin/tenants', data);
    }

    /**
     * Delete a tenant.
     * @param {string} tenantId - The tenant identifier
     * @returns {Promise<ApiResponse>} Deletion confirmation
     */
    async deleteTenant(tenantId) {
        return this._makeRequest('DELETE', `/v1.0/admin/tenants/${tenantId}`);
    }

    /**
     * Update labels on a tenant (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated tenant
     */
    async updateTenantLabels(tenantId, labels) {
        return this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on a tenant (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated tenant
     */
    async updateTenantTags(tenantId, tags) {
        return this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/tags`, { Tags: tags || {} });
    }

    // ==================== Admin - User Management Endpoints ====================

    /**
     * List all users in a tenant.
     * @param {string} tenantId - The tenant identifier
     * @returns {Promise<ApiResponse>} List of users
     */
    async listUsers(tenantId) {
        return this._makeRequest('GET', `/v1.0/admin/tenants/${tenantId}/users`);
    }

    /**
     * Get all users in a tenant as UserInfo objects.
     * @param {string} tenantId - The tenant identifier
     * @returns {Promise<UserInfo[]>} Array of UserInfo objects
     */
    async getUsers(tenantId) {
        const response = await this.listUsers(tenantId);
        if (response.data?.users) {
            return response.data.users.map(u => new UserInfo(u));
        }
        return [];
    }

    /**
     * Get a specific user.
     * @param {string} tenantId - The tenant identifier
     * @param {string} userId - The user identifier
     * @returns {Promise<ApiResponse>} User details
     */
    async getUser(tenantId, userId) {
        return this._makeRequest('GET', `/v1.0/admin/tenants/${tenantId}/users/${userId}`);
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
     * @returns {Promise<ApiResponse>} Created user response
     */
    async createUser(tenantId, options) {
        const data = {
            email: options.email,
            password: options.password
        };
        if (options.firstName) {
            data.firstName = options.firstName;
        }
        if (options.lastName) {
            data.lastName = options.lastName;
        }
        if (options.isAdmin !== undefined) {
            data.isAdmin = options.isAdmin;
        }
        return this._makeRequest('POST', `/v1.0/admin/tenants/${tenantId}/users`, data);
    }

    /**
     * Delete a user.
     * @param {string} tenantId - The tenant identifier
     * @param {string} userId - The user identifier
     * @returns {Promise<ApiResponse>} Deletion confirmation
     */
    async deleteUser(tenantId, userId) {
        return this._makeRequest('DELETE', `/v1.0/admin/tenants/${tenantId}/users/${userId}`);
    }

    /**
     * Update labels on a user (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string} userId - The user identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated user
     */
    async updateUserLabels(tenantId, userId, labels) {
        return this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/users/${userId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on a user (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string} userId - The user identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated user
     */
    async updateUserTags(tenantId, userId, tags) {
        return this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/users/${userId}/tags`, { Tags: tags || {} });
    }

    // ==================== Admin - Credential Management Endpoints ====================

    /**
     * List all credentials in a tenant.
     * @param {string} tenantId - The tenant identifier
     * @returns {Promise<ApiResponse>} List of credentials
     */
    async listCredentials(tenantId) {
        return this._makeRequest('GET', `/v1.0/admin/tenants/${tenantId}/credentials`);
    }

    /**
     * Get all credentials in a tenant as CredentialInfo objects.
     * @param {string} tenantId - The tenant identifier
     * @returns {Promise<CredentialInfo[]>} Array of CredentialInfo objects
     */
    async getCredentials(tenantId) {
        const response = await this.listCredentials(tenantId);
        if (response.data?.credentials) {
            return response.data.credentials.map(c => new CredentialInfo(c));
        }
        return [];
    }

    /**
     * Get a specific credential.
     * @param {string} tenantId - The tenant identifier
     * @param {string} credentialId - The credential identifier
     * @returns {Promise<ApiResponse>} Credential details
     */
    async getCredential(tenantId, credentialId) {
        return this._makeRequest('GET', `/v1.0/admin/tenants/${tenantId}/credentials/${credentialId}`);
    }

    /**
     * Create a new credential (API key) in a tenant.
     * @param {string} tenantId - The tenant identifier
     * @param {object} [options] - Credential creation options
     * @param {string} [options.description] - Optional description
     * @returns {Promise<ApiResponse>} Created credential response (includes bearer token)
     */
    async createCredential(tenantId, options = {}) {
        const data = {};
        if (options.description) {
            data.description = options.description;
        }
        return this._makeRequest('POST', `/v1.0/admin/tenants/${tenantId}/credentials`, data);
    }

    /**
     * Delete a credential.
     * @param {string} tenantId - The tenant identifier
     * @param {string} credentialId - The credential identifier
     * @returns {Promise<ApiResponse>} Deletion confirmation
     */
    async deleteCredential(tenantId, credentialId) {
        return this._makeRequest('DELETE', `/v1.0/admin/tenants/${tenantId}/credentials/${credentialId}`);
    }

    /**
     * Update labels on a credential (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string} credentialId - The credential identifier
     * @param {string[]} labels - The new labels to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated credential
     */
    async updateCredentialLabels(tenantId, credentialId, labels) {
        return this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/credentials/${credentialId}/labels`, { Labels: labels || [] });
    }

    /**
     * Update tags on a credential (full replacement).
     * @param {string} tenantId - The tenant identifier
     * @param {string} credentialId - The credential identifier
     * @param {object} tags - The new tags to set
     * @returns {Promise<ApiResponse>} Update confirmation with updated credential
     */
    async updateCredentialTags(tenantId, credentialId, tags) {
        return this._makeRequest('PUT', `/v1.0/tenants/${tenantId}/credentials/${credentialId}/tags`, { Tags: tags || {} });
    }
}

// Export for Node.js
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        VerbexClient,
        VerbexError,
        ApiResponse,
        IndexInfo,
        DocumentInfo,
        SearchResult,
        SearchResponse,
        TenantInfo,
        UserInfo,
        CredentialInfo
    };
}

// Export for ES modules
if (typeof exports !== 'undefined') {
    exports.VerbexClient = VerbexClient;
    exports.VerbexError = VerbexError;
    exports.ApiResponse = ApiResponse;
    exports.IndexInfo = IndexInfo;
    exports.DocumentInfo = DocumentInfo;
    exports.SearchResult = SearchResult;
    exports.SearchResponse = SearchResponse;
    exports.TenantInfo = TenantInfo;
    exports.UserInfo = UserInfo;
    exports.CredentialInfo = CredentialInfo;
}
