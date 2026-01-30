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
   * @param {string} endpoint - API endpoint
   * @param {Object} options - Fetch options including optional signal for AbortController
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
      headers,
      signal: options.signal
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
  async login(username, password, tenantId = null) {
    const body = { Username: username, Password: password };
    if (tenantId) {
      body.TenantId = tenantId;
    }
    const response = await fetch(`${this.baseUrl}/v1.0/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
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
  async getIndices(options = {}) {
    return this.get('/v1.0/indices', options);
  }

  async getIndex(id, options = {}) {
    return this.get(`/v1.0/indices/${encodeURIComponent(id)}`, options);
  }

  async createIndex(indexConfig) {
    // Convert to PascalCase for the API
    const apiConfig = {
      Name: indexConfig.name
    };

    if (indexConfig.tenantId) {
      apiConfig.TenantId = indexConfig.tenantId;
    }
    if (indexConfig.description) {
      apiConfig.Description = indexConfig.description;
    }
    if (indexConfig.inMemory !== undefined) {
      apiConfig.InMemory = indexConfig.inMemory;
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
    if (indexConfig.customMetadata !== undefined && indexConfig.customMetadata !== null) {
      apiConfig.CustomMetadata = indexConfig.customMetadata;
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

  async updateIndexCustomMetadata(indexId, customMetadata) {
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}/customMetadata`, { CustomMetadata: customMetadata });
  }

  async updateIndex(indexId, updates) {
    const body = {};
    if (updates.name !== undefined) body.Name = updates.name;
    if (updates.description !== undefined) body.Description = updates.description;
    if (updates.enabled !== undefined) body.Enabled = updates.enabled;
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}`, body);
  }

  // Document endpoints
  async getDocuments(indexId, options = {}) {
    return this.get(`/v1.0/indices/${encodeURIComponent(indexId)}/documents`, options);
  }

  async getDocument(indexId, docId, options = {}) {
    return this.get(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}`, options);
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
    if (document.customMetadata !== undefined && document.customMetadata !== null) {
      apiDocument.CustomMetadata = document.customMetadata;
    }

    return this.post(`/v1.0/indices/${encodeURIComponent(indexId)}/documents`, apiDocument);
  }

  async deleteDocument(indexId, docId) {
    return this.delete(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}`);
  }

  async deleteDocumentsBatch(indexId, documentIds) {
    if (!documentIds || documentIds.length === 0) {
      return { data: { deleted: [], notFound: [], deletedCount: 0, notFoundCount: 0, requestedCount: 0 } };
    }
    const idsParam = documentIds.map(id => encodeURIComponent(id)).join(',');
    return this.delete(`/v1.0/indices/${encodeURIComponent(indexId)}/documents?ids=${idsParam}`);
  }

  async updateDocumentLabels(indexId, docId, labels) {
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}/labels`, { Labels: labels || [] });
  }

  async updateDocumentTags(indexId, docId, tags) {
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}/tags`, { Tags: tags || {} });
  }

  async updateDocumentCustomMetadata(indexId, docId, customMetadata) {
    return this.put(`/v1.0/indices/${encodeURIComponent(indexId)}/documents/${encodeURIComponent(docId)}/customMetadata`, { CustomMetadata: customMetadata });
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

  // Admin - Tenant endpoints
  async getTenants(options = {}) {
    return this.get('/v1.0/tenants', options);
  }

  async getTenant(tenantId, options = {}) {
    return this.get(`/v1.0/tenants/${encodeURIComponent(tenantId)}`, options);
  }

  async createTenant(tenant) {
    const apiTenant = {
      name: tenant.name
    };
    if (tenant.description) {
      apiTenant.description = tenant.description;
    }
    return this.post('/v1.0/tenants', apiTenant);
  }

  async deleteTenant(tenantId) {
    return this.delete(`/v1.0/tenants/${encodeURIComponent(tenantId)}`);
  }

  async updateTenant(tenantId, updates) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}`, updates);
  }

  async updateTenantLabels(tenantId, labels) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}/labels`, { Labels: labels || [] });
  }

  async updateTenantTags(tenantId, tags) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}/tags`, { Tags: tags || {} });
  }

  // Admin - User endpoints
  async getUsers(tenantId, options = {}) {
    return this.get(`/v1.0/tenants/${encodeURIComponent(tenantId)}/users`, options);
  }

  async getUser(tenantId, userId, options = {}) {
    return this.get(`/v1.0/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}`, options);
  }

  async createUser(tenantId, user) {
    const apiUser = {
      email: user.email,
      password: user.password
    };
    if (user.firstName) {
      apiUser.firstName = user.firstName;
    }
    if (user.lastName) {
      apiUser.lastName = user.lastName;
    }
    if (user.isAdmin !== undefined) {
      apiUser.isAdmin = user.isAdmin;
    }
    return this.post(`/v1.0/tenants/${encodeURIComponent(tenantId)}/users`, apiUser);
  }

  async deleteUser(tenantId, userId) {
    return this.delete(`/v1.0/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}`);
  }

  async updateUser(tenantId, userId, updates) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}`, updates);
  }

  async updateUserLabels(tenantId, userId, labels) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}/labels`, { Labels: labels || [] });
  }

  async updateUserTags(tenantId, userId, tags) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}/tags`, { Tags: tags || {} });
  }

  // Admin - Credential endpoints
  async getCredentials(tenantId, options = {}) {
    return this.get(`/v1.0/tenants/${encodeURIComponent(tenantId)}/credentials`, options);
  }

  async getCredential(tenantId, credentialId, options = {}) {
    return this.get(`/v1.0/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(credentialId)}`, options);
  }

  async createCredential(tenantId, credential) {
    const apiCredential = {};
    if (credential.description) {
      apiCredential.description = credential.description;
    }
    return this.post(`/v1.0/tenants/${encodeURIComponent(tenantId)}/credentials`, apiCredential);
  }

  async updateCredential(tenantId, credentialId, updates) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(credentialId)}`, updates);
  }

  async deleteCredential(tenantId, credentialId) {
    return this.delete(`/v1.0/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(credentialId)}`);
  }

  async updateCredentialLabels(tenantId, credentialId, labels) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(credentialId)}/labels`, { Labels: labels || [] });
  }

  async updateCredentialTags(tenantId, credentialId, tags) {
    return this.put(`/v1.0/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(credentialId)}/tags`, { Tags: tags || {} });
  }
}

export default ApiClient;
