/**
 * Verbex API Client
 * Handles all communication with the Verbex Server REST API
 */

/**
 * Convert PascalCase keys to camelCase recursively
 */
function toCamelCase(obj) {
  if (obj === null || obj === undefined) {
    return obj;
  }

  if (Array.isArray(obj)) {
    return obj.map(toCamelCase);
  }

  if (typeof obj === 'object') {
    const result = {};
    for (const key in obj) {
      if (Object.prototype.hasOwnProperty.call(obj, key)) {
        const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
        result[camelKey] = toCamelCase(obj[key]);
      }
    }
    return result;
  }

  return obj;
}

class ApiClient {
  constructor(baseUrl, token) {
    this.baseUrl = baseUrl.replace(/\/$/, '');
    this.token = token;
  }

  /**
   * Make an HTTP request to the API
   */
  async request(endpoint, options = {}) {
    const url = `${this.baseUrl}${endpoint}`;
    const headers = {
      'Content-Type': 'application/json',
      ...options.headers
    };

    if (this.token) {
      headers['Authorization'] = `Bearer ${this.token}`;
    }

    const response = await fetch(url, {
      ...options,
      headers
    });

    const rawData = await response.json();

    // Check for success - server uses PascalCase (Success) in response
    const isSuccess = rawData.Success !== undefined ? rawData.Success : rawData.success;
    const errorMsg = rawData.ErrorMessage || rawData.errorMessage;

    if (!response.ok || isSuccess === false) {
      throw new Error(errorMsg || `HTTP error ${response.status}`);
    }

    // Normalize response to camelCase
    return toCamelCase(rawData);
  }

  async get(endpoint, options = {}) {
    return this.request(endpoint, { ...options, method: 'GET' });
  }

  async post(endpoint, body, options = {}) {
    return this.request(endpoint, {
      ...options,
      method: 'POST',
      body: JSON.stringify(body)
    });
  }

  async put(endpoint, body, options = {}) {
    return this.request(endpoint, {
      ...options,
      method: 'PUT',
      body: JSON.stringify(body)
    });
  }

  async delete(endpoint, options = {}) {
    return this.request(endpoint, { ...options, method: 'DELETE' });
  }

  // Health endpoints
  async testConnection() {
    return this.get('/v1.0/health');
  }

  // Authentication endpoints
  async login(username, password) {
    const response = await fetch(`${this.baseUrl}/v1.0/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Username: username, Password: password })
    });
    const rawData = await response.json();
    const isSuccess = rawData.Success !== undefined ? rawData.Success : rawData.success;
    const errorMsg = rawData.ErrorMessage || rawData.errorMessage;
    if (!response.ok || isSuccess === false) {
      throw new Error(errorMsg || 'Login failed');
    }
    return toCamelCase(rawData);
  }

  async validateToken() {
    return this.get('/v1.0/auth/validate');
  }

  // Index endpoints
  async getIndices() {
    return this.get('/v1.0/indices');
  }

  async getIndex(id) {
    return this.get(`/v1.0/indices/${encodeURIComponent(id)}`);
  }

  async createIndex(indexConfig) {
    // Convert to PascalCase for the API
    const apiConfig = {
      Id: indexConfig.id,
      Name: indexConfig.name
    };

    if (indexConfig.description) {
      apiConfig.Description = indexConfig.description;
    }
    if (indexConfig.repositoryFilename) {
      apiConfig.RepositoryFilename = indexConfig.repositoryFilename;
    }
    if (indexConfig.inMemory !== undefined) {
      apiConfig.InMemory = indexConfig.inMemory;
    }
    if (indexConfig.storageMode) {
      apiConfig.StorageMode = indexConfig.storageMode;
    }
    if (indexConfig.enableLemmatizer !== undefined) {
      apiConfig.EnableLemmatizer = indexConfig.enableLemmatizer;
    }
    if (indexConfig.enableStopWordRemover !== undefined) {
      apiConfig.EnableStopWordRemover = indexConfig.enableStopWordRemover;
    }
    if (indexConfig.minTokenLength !== undefined) {
      apiConfig.MinTokenLength = indexConfig.minTokenLength;
    }
    if (indexConfig.maxTokenLength !== undefined) {
      apiConfig.MaxTokenLength = indexConfig.maxTokenLength;
    }
    if (indexConfig.labels && indexConfig.labels.length > 0) {
      apiConfig.Labels = indexConfig.labels;
    }
    if (indexConfig.tags && Object.keys(indexConfig.tags).length > 0) {
      apiConfig.Tags = indexConfig.tags;
    }

    return this.post('/v1.0/indices', apiConfig);
  }

  async deleteIndex(id) {
    return this.delete(`/v1.0/indices/${encodeURIComponent(id)}`);
  }

  async updateIndexLabels(indexId, labels) {
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}/labels`, { Labels: labels || [] });
  }

  async updateIndexTags(indexId, tags) {
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}/tags`, { Tags: tags || {} });
  }

  // Document endpoints
  async getDocuments(indexId) {
    return this.get(`/v1.0/indices/${encodeURIComponent(indexId)}/documents`);
  }

  async getDocument(indexId, docId) {
    return this.get(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}`);
  }

  async addDocument(indexId, document) {
    // Convert to PascalCase for the API
    const apiDocument = {
      Content: document.content
    };

    if (document.id) {
      apiDocument.Id = document.id;
    }
    if (document.labels && document.labels.length > 0) {
      apiDocument.Labels = document.labels;
    }
    if (document.tags && Object.keys(document.tags).length > 0) {
      apiDocument.Tags = document.tags;
    }

    return this.post(`/v1.0/indices/${encodeURIComponent(indexId)}/documents`, apiDocument);
  }

  async deleteDocument(indexId, docId) {
    return this.delete(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}`);
  }

  async updateDocumentLabels(indexId, docId, labels) {
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}/labels`, { Labels: labels || [] });
  }

  async updateDocumentTags(indexId, docId, tags) {
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}/tags`, { Tags: tags || {} });
  }

  // Search endpoints
  async search(indexId, query, maxResults = 100, labels = null, tags = null, useAndLogic = false) {
    const body = {
      Query: query,
      MaxResults: maxResults,
      UseAndLogic: useAndLogic
    };
    if (labels && labels.length > 0) {
      body.Labels = labels;
    }
    if (tags && Object.keys(tags).length > 0) {
      body.Tags = tags;
    }
    return this.post(`/v1.0/indices/${encodeURIComponent(indexId)}/search`, body);
  }
}

export default ApiClient;
