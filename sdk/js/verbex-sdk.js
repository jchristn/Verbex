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
        this.id = data.id || '';
        this.name = data.name || null;
        this.description = data.description || null;
        this.enabled = data.enabled || null;
        this.inMemory = data.inMemory || null;
        this.createdUtc = data.createdUtc || null;
        this.statistics = data.statistics || null;
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
     * @param {string} options.id - Unique identifier for the index
     * @param {string} [options.name] - Display name
     * @param {string} [options.description] - Description
     * @param {string} [options.repositoryFilename] - Filename for persistence
     * @param {boolean} [options.inMemory=false] - Use in-memory storage
     * @param {string} [options.storageMode='MemoryOnly'] - Storage mode
     * @param {boolean} [options.enableLemmatizer=false] - Enable lemmatization
     * @param {boolean} [options.enableStopWordRemover=false] - Enable stop word filtering
     * @param {number} [options.minTokenLength=0] - Minimum token length
     * @param {number} [options.maxTokenLength=0] - Maximum token length
     * @param {string[]} [options.labels] - Labels to associate with the index
     * @param {object} [options.tags] - Key-value tags to associate with the index
     * @returns {Promise<ApiResponse>} Created index response
     */
    async createIndex(options) {
        const data = {
            Id: options.id,
            Name: options.name || options.id,
            Description: options.description || '',
            RepositoryFilename: options.repositoryFilename || `${options.id}.db`,
            InMemory: options.inMemory || false,
            StorageMode: options.storageMode || 'MemoryOnly',
            EnableLemmatizer: options.enableLemmatizer || false,
            EnableStopWordRemover: options.enableStopWordRemover || false,
            MinTokenLength: options.minTokenLength || 0,
            MaxTokenLength: options.maxTokenLength || 0
        };
        if (options.labels) {
            data.Labels = options.labels;
        }
        if (options.tags) {
            data.Tags = options.tags;
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
     * @returns {Promise<ApiResponse>} Document creation response
     */
    async addDocument(indexId, content, documentId = null, labels = null, tags = null) {
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
        SearchResponse
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
}
