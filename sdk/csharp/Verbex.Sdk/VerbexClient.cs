namespace Verbex.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Verbex SDK Client for .NET.
    /// Provides methods to interact with all Verbex REST API endpoints.
    /// </summary>
    /// <remarks>
    /// This client is thread-safe and can be reused for multiple requests.
    /// Implements IDisposable to properly clean up HTTP resources.
    /// All methods return domain objects directly rather than wrapped responses.
    /// </remarks>
    public class VerbexClient : IDisposable
    {
        private readonly string _Endpoint;
        private readonly string _AccessKey;
        private readonly HttpClient _HttpClient;
        private readonly JsonSerializerOptions _JsonOptions;
        private bool _Disposed;

        /// <summary>
        /// Creates a new VerbexClient instance.
        /// </summary>
        /// <param name="endpoint">The base URL of the Verbex server (e.g., "http://localhost:8080").</param>
        /// <param name="accessKey">The bearer token for authentication.</param>
        /// <exception cref="ArgumentNullException">Thrown when endpoint or accessKey is null.</exception>
        public VerbexClient(string endpoint, string accessKey)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(accessKey);

            _Endpoint = endpoint.TrimEnd('/');
            _AccessKey = accessKey;
            _HttpClient = new HttpClient();
            _HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Disposes the HTTP client resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                _HttpClient.Dispose();
            }

            _Disposed = true;
        }

        private async Task<T> MakeRequestAsync<T>(
            HttpMethod method,
            string path,
            object? data = null,
            bool requireAuth = true,
            CancellationToken cancellationToken = default)
        {
            string url = $"{_Endpoint}{path}";

            using HttpRequestMessage request = new HttpRequestMessage(method, url);

            if (requireAuth)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _AccessKey);
            }

            if (data != null && (method == HttpMethod.Post || method == HttpMethod.Put))
            {
                string json = JsonSerializer.Serialize(data, _JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            try
            {
                HttpResponseMessage response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                ApiResponse<T>? apiResponse;
                try
                {
                    apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(responseBody, _JsonOptions);
                }
                catch (JsonException)
                {
                    apiResponse = new ApiResponse<T>
                    {
                        Success = response.IsSuccessStatusCode,
                        StatusCode = (int)response.StatusCode,
                        ErrorMessage = responseBody
                    };
                }

                if (apiResponse == null)
                {
                    throw new VerbexException("Failed to parse API response");
                }

                if (!apiResponse.Success && apiResponse.StatusCode >= 400)
                {
                    string errorMessage = apiResponse.ErrorMessage ?? $"Request failed with status {apiResponse.StatusCode}";
                    ApiResponse errorResponse = new ApiResponse
                    {
                        Guid = apiResponse.Guid,
                        Success = apiResponse.Success,
                        TimestampUtc = apiResponse.TimestampUtc,
                        StatusCode = apiResponse.StatusCode,
                        ErrorMessage = apiResponse.ErrorMessage,
                        TotalCount = apiResponse.TotalCount,
                        ProcessingTimeMs = apiResponse.ProcessingTimeMs
                    };
                    throw new VerbexException(errorMessage, apiResponse.StatusCode, errorResponse);
                }

                if (apiResponse.Data == null)
                {
                    throw new VerbexException("API response data was null");
                }

                return apiResponse.Data;
            }
            catch (HttpRequestException ex)
            {
                throw new VerbexException($"Request failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                throw new VerbexException("Request timed out", ex);
            }
        }

        private async Task MakeRequestAsync(
            HttpMethod method,
            string path,
            object? data = null,
            bool requireAuth = true,
            CancellationToken cancellationToken = default)
        {
            string url = $"{_Endpoint}{path}";

            using HttpRequestMessage request = new HttpRequestMessage(method, url);

            if (requireAuth)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _AccessKey);
            }

            if (data != null && (method == HttpMethod.Post || method == HttpMethod.Put))
            {
                string json = JsonSerializer.Serialize(data, _JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            try
            {
                HttpResponseMessage response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                ApiResponse? apiResponse;
                try
                {
                    apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseBody, _JsonOptions);
                }
                catch (JsonException)
                {
                    apiResponse = new ApiResponse
                    {
                        Success = response.IsSuccessStatusCode,
                        StatusCode = (int)response.StatusCode,
                        ErrorMessage = responseBody
                    };
                }

                if (apiResponse == null)
                {
                    throw new VerbexException("Failed to parse API response");
                }

                if (!apiResponse.Success && apiResponse.StatusCode >= 400)
                {
                    string errorMessage = apiResponse.ErrorMessage ?? $"Request failed with status {apiResponse.StatusCode}";
                    throw new VerbexException(errorMessage, apiResponse.StatusCode, apiResponse);
                }
            }
            catch (HttpRequestException ex)
            {
                throw new VerbexException($"Request failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                throw new VerbexException("Request timed out", ex);
            }
        }

        private async Task<bool> MakeHeadRequestAsync(
            string path,
            bool requireAuth = true,
            CancellationToken cancellationToken = default)
        {
            string url = $"{_Endpoint}{path}";

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);

            if (requireAuth)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _AccessKey);
            }

            try
            {
                HttpResponseMessage response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                throw new VerbexException($"Request failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                throw new VerbexException("Request timed out", ex);
            }
        }

        // ==================== Health Endpoints ====================

        /// <summary>
        /// Checks server health via the root endpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health check data including status and version.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<HealthData> RootHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<HealthData>(HttpMethod.Get, "/", null, false, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks server health via the /v1.0/health endpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health check data including status and version.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<HealthData> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<HealthData>(HttpMethod.Get, "/v1.0/health", null, false, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Authentication Endpoints ====================

        /// <summary>
        /// Authenticates with tenant ID, email, and password.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="email">The user's email address.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Login result indicating success or failure with context.</returns>
        public async Task<LoginResult> LoginAsync(string tenantId, string email, string password, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tenantId);
            ArgumentNullException.ThrowIfNull(email);
            ArgumentNullException.ThrowIfNull(password);

            LoginRequest request = new LoginRequest(tenantId, email, password);

            try
            {
                LoginData loginData = await MakeRequestAsync<LoginData>(HttpMethod.Post, "/v1.0/auth/login", request, false, cancellationToken).ConfigureAwait(false);

                return LoginResult.Successful(
                    token: loginData.Token ?? string.Empty,
                    tenantId: tenantId,
                    email: email,
                    isAdmin: loginData.IsAdmin,
                    isGlobalAdmin: loginData.IsGlobalAdmin);
            }
            catch (VerbexException ex)
            {
                AuthenticationResultEnum authResult = ex.StatusCode switch
                {
                    401 => AuthenticationResultEnum.InvalidCredentials,
                    403 => AuthenticationResultEnum.TenantAccessDenied,
                    404 => AuthenticationResultEnum.NotFound,
                    _ => AuthenticationResultEnum.NotAuthenticated
                };

                AuthorizationResultEnum authzResult = ex.StatusCode switch
                {
                    401 => AuthorizationResultEnum.Unauthorized,
                    403 => AuthorizationResultEnum.AccessDenied,
                    404 => AuthorizationResultEnum.ResourceNotFound,
                    _ => AuthorizationResultEnum.Unauthorized
                };

                return LoginResult.Failed(authResult, authzResult, ex.Message);
            }
        }

        /// <summary>
        /// Authenticates with an existing bearer token by validating it against the server.
        /// </summary>
        /// <param name="bearerToken">The bearer token to validate and use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Login result indicating success or failure with context.</returns>
        public async Task<LoginResult> LoginAsync(string bearerToken, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(bearerToken);

            string originalAccessKey = _AccessKey;

            try
            {
                // Temporarily use the provided bearer token
                System.Reflection.FieldInfo? field = typeof(VerbexClient).GetField("_AccessKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(this, bearerToken);

                ValidationData validationData = await MakeRequestAsync<ValidationData>(HttpMethod.Get, "/v1.0/auth/validate", null, true, cancellationToken).ConfigureAwait(false);

                if (validationData.Valid)
                {
                    return LoginResult.Successful(
                        token: bearerToken,
                        tenantId: validationData.TenantId,
                        userId: validationData.UserId,
                        email: validationData.Email,
                        isAdmin: validationData.IsAdmin,
                        isGlobalAdmin: validationData.IsGlobalAdmin);
                }

                // Restore original access key on failure
                field?.SetValue(this, originalAccessKey);

                return LoginResult.Failed(
                    AuthenticationResultEnum.InvalidCredentials,
                    AuthorizationResultEnum.Unauthorized,
                    "Bearer token validation failed");
            }
            catch (VerbexException ex)
            {
                // Restore original access key on exception
                System.Reflection.FieldInfo? field = typeof(VerbexClient).GetField("_AccessKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(this, originalAccessKey);

                AuthenticationResultEnum authResult = ex.StatusCode switch
                {
                    401 => AuthenticationResultEnum.InvalidCredentials,
                    403 => AuthenticationResultEnum.TenantAccessDenied,
                    _ => AuthenticationResultEnum.NotAuthenticated
                };

                return LoginResult.Failed(authResult, AuthorizationResultEnum.Unauthorized, ex.Message);
            }
        }

        /// <summary>
        /// Validates the current bearer token.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation data including whether the token is valid and user details.</returns>
        /// <exception cref="VerbexException">Thrown when validation fails.</exception>
        public async Task<ValidationData> ValidateTokenAsync(CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<ValidationData>(HttpMethod.Get, "/v1.0/auth/validate", null, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Index Management Endpoints ====================

        /// <summary>
        /// Lists all available indices.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of index information objects.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<List<IndexInfo>> ListIndicesAsync(CancellationToken cancellationToken = default)
        {
            IndicesListData data = await MakeRequestAsync<IndicesListData>(HttpMethod.Get, "/v1.0/indices", null, true, cancellationToken).ConfigureAwait(false);
            return data.Indices ?? new List<IndexInfo>();
        }

        /// <summary>
        /// Creates a new index.
        /// </summary>
        /// <param name="name">Display name for the index.</param>
        /// <param name="description">Description of the index.</param>
        /// <param name="inMemory">Whether to use in-memory storage only.</param>
        /// <param name="enableLemmatizer">Enable word lemmatization.</param>
        /// <param name="enableStopWordRemover">Enable stop word filtering.</param>
        /// <param name="minTokenLength">Minimum token length (0 to disable).</param>
        /// <param name="maxTokenLength">Maximum token length (0 to disable).</param>
        /// <param name="labels">Optional list of labels to associate with the index.</param>
        /// <param name="tags">Optional key-value tags to associate with the index.</param>
        /// <param name="tenantId">Tenant ID (required for global admin users, optional for tenant users).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created index information.</returns>
        /// <exception cref="VerbexException">Thrown when creation fails.</exception>
        public async Task<IndexInfo> CreateIndexAsync(
            string name,
            string? description = null,
            bool inMemory = false,
            bool enableLemmatizer = false,
            bool enableStopWordRemover = false,
            int minTokenLength = 0,
            int maxTokenLength = 0,
            List<string>? labels = null,
            Dictionary<string, string>? tags = null,
            string? tenantId = null,
            CancellationToken cancellationToken = default)
        {
            CreateIndexRequest request = new CreateIndexRequest(name)
            {
                TenantId = tenantId,
                Description = description ?? string.Empty,
                InMemory = inMemory,
                EnableLemmatizer = enableLemmatizer,
                EnableStopWordRemover = enableStopWordRemover,
                MinTokenLength = minTokenLength,
                MaxTokenLength = maxTokenLength,
                Labels = labels,
                Tags = tags
            };
            CreateIndexData data = await MakeRequestAsync<CreateIndexData>(HttpMethod.Post, "/v1.0/indices", request, true, cancellationToken).ConfigureAwait(false);
            return data.Index ?? new IndexInfo();
        }

        /// <summary>
        /// Gets detailed information about a specific index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Index information including statistics.</returns>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task<IndexInfo> GetIndexAsync(string indexId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<IndexInfo>(HttpMethod.Get, $"/v1.0/indices/{indexId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if an index exists.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the index exists, false otherwise.</returns>
        public async Task<bool> IndexExistsAsync(string indexId, CancellationToken cancellationToken = default)
        {
            return await MakeHeadRequestAsync($"/v1.0/indices/{indexId}", true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task DeleteIndexAsync(string indexId, CancellationToken cancellationToken = default)
        {
            await MakeRequestAsync<DeleteIndexData>(HttpMethod.Delete, $"/v1.0/indices/{indexId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates labels on an index (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="labels">The new labels to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task UpdateIndexLabelsAsync(
            string indexId,
            List<string> labels,
            CancellationToken cancellationToken = default)
        {
            object request = new { Labels = labels ?? new List<string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/indices/{indexId}/labels", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates tags on an index (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="tags">The new tags to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task UpdateIndexTagsAsync(
            string indexId,
            Dictionary<string, string> tags,
            CancellationToken cancellationToken = default)
        {
            object request = new { Tags = tags ?? new Dictionary<string, string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/indices/{indexId}/tags", request, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Document Management Endpoints ====================

        /// <summary>
        /// Lists all documents in an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of document information objects.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<List<DocumentInfo>> ListDocumentsAsync(string indexId, CancellationToken cancellationToken = default)
        {
            DocumentsListData data = await MakeRequestAsync<DocumentsListData>(HttpMethod.Get, $"/v1.0/indices/{indexId}/documents", null, true, cancellationToken).ConfigureAwait(false);
            return data.Documents ?? new List<DocumentInfo>();
        }

        /// <summary>
        /// Adds a document to an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="content">The document content to index.</param>
        /// <param name="documentId">Optional document ID (GUID format, auto-generated if not provided).</param>
        /// <param name="labels">Optional list of labels to associate with the document.</param>
        /// <param name="tags">Optional key-value tags to associate with the document.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created document data including the document ID.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<AddDocumentData> AddDocumentAsync(
            string indexId,
            string content,
            string? documentId = null,
            List<string>? labels = null,
            Dictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            AddDocumentRequest request = new AddDocumentRequest(content, documentId, labels, tags);
            return await MakeRequestAsync<AddDocumentData>(HttpMethod.Post, $"/v1.0/indices/{indexId}/documents", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a specific document.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Document information.</returns>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task<DocumentInfo> GetDocumentAsync(string indexId, string documentId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<DocumentInfo>(HttpMethod.Get, $"/v1.0/indices/{indexId}/documents/{documentId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if a document exists in an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the document exists, false otherwise.</returns>
        public async Task<bool> DocumentExistsAsync(string indexId, string documentId, CancellationToken cancellationToken = default)
        {
            return await MakeHeadRequestAsync($"/v1.0/indices/{indexId}/documents/{documentId}", true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a document from an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task DeleteDocumentAsync(string indexId, string documentId, CancellationToken cancellationToken = default)
        {
            await MakeRequestAsync<DeleteDocumentData>(HttpMethod.Delete, $"/v1.0/indices/{indexId}/documents/{documentId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates labels on a document (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="labels">The new labels to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task UpdateDocumentLabelsAsync(
            string indexId,
            string documentId,
            List<string> labels,
            CancellationToken cancellationToken = default)
        {
            object request = new { Labels = labels ?? new List<string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/indices/{indexId}/documents/{documentId}/labels", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates tags on a document (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="tags">The new tags to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task UpdateDocumentTagsAsync(
            string indexId,
            string documentId,
            Dictionary<string, string> tags,
            CancellationToken cancellationToken = default)
        {
            object request = new { Tags = tags ?? new Dictionary<string, string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/indices/{indexId}/documents/{documentId}/tags", request, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Search Endpoint ====================

        /// <summary>
        /// Searches documents in an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="query">The search query.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="labels">Optional labels to filter by (AND logic, case-insensitive).</param>
        /// <param name="tags">Optional tags to filter by (AND logic, exact match).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Search data including results and metadata.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<SearchData> SearchAsync(
            string indexId,
            string query,
            int maxResults = 100,
            List<string>? labels = null,
            Dictionary<string, object>? tags = null,
            CancellationToken cancellationToken = default)
        {
            SearchRequest request = new SearchRequest(query, maxResults, labels, tags);
            return await MakeRequestAsync<SearchData>(HttpMethod.Post, $"/v1.0/indices/{indexId}/search", request, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Admin - Tenant Management Endpoints ====================

        /// <summary>
        /// Lists all tenants.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of tenant information objects.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<List<TenantInfo>> ListTenantsAsync(CancellationToken cancellationToken = default)
        {
            TenantsListData data = await MakeRequestAsync<TenantsListData>(HttpMethod.Get, "/v1.0/admin/tenants", null, true, cancellationToken).ConfigureAwait(false);
            return data.Tenants ?? new List<TenantInfo>();
        }

        /// <summary>
        /// Gets a specific tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tenant information.</returns>
        /// <exception cref="VerbexException">Thrown when the tenant is not found.</exception>
        public async Task<TenantInfo> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<TenantInfo>(HttpMethod.Get, $"/v1.0/admin/tenants/{tenantId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new tenant.
        /// </summary>
        /// <param name="name">Tenant name.</param>
        /// <param name="description">Optional description.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created tenant information.</returns>
        /// <exception cref="VerbexException">Thrown when creation fails.</exception>
        public async Task<TenantInfo> CreateTenantAsync(
            string name,
            string? description = null,
            CancellationToken cancellationToken = default)
        {
            CreateTenantRequest request = new CreateTenantRequest(name, description);
            CreateTenantData data = await MakeRequestAsync<CreateTenantData>(HttpMethod.Post, "/v1.0/admin/tenants", request, true, cancellationToken).ConfigureAwait(false);
            return data.Tenant ?? new TenantInfo();
        }

        /// <summary>
        /// Deletes a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the tenant is not found.</exception>
        public async Task DeleteTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            await MakeRequestAsync<DeleteData>(HttpMethod.Delete, $"/v1.0/admin/tenants/{tenantId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Admin - User Management Endpoints ====================

        /// <summary>
        /// Lists all users in a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of user information objects.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<List<UserInfo>> ListUsersAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            UsersListData data = await MakeRequestAsync<UsersListData>(HttpMethod.Get, $"/v1.0/admin/tenants/{tenantId}/users", null, true, cancellationToken).ConfigureAwait(false);
            return data.Users ?? new List<UserInfo>();
        }

        /// <summary>
        /// Gets a specific user.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>User information.</returns>
        /// <exception cref="VerbexException">Thrown when the user is not found.</exception>
        public async Task<UserInfo> GetUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<UserInfo>(HttpMethod.Get, $"/v1.0/admin/tenants/{tenantId}/users/{userId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new user in a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="email">User email.</param>
        /// <param name="password">User password.</param>
        /// <param name="firstName">Optional first name.</param>
        /// <param name="lastName">Optional last name.</param>
        /// <param name="isAdmin">Whether user is tenant admin.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created user information.</returns>
        /// <exception cref="VerbexException">Thrown when creation fails.</exception>
        public async Task<UserInfo> CreateUserAsync(
            string tenantId,
            string email,
            string password,
            string? firstName = null,
            string? lastName = null,
            bool isAdmin = false,
            CancellationToken cancellationToken = default)
        {
            CreateUserRequest request = new CreateUserRequest(email, password, firstName, lastName, isAdmin);
            CreateUserData data = await MakeRequestAsync<CreateUserData>(HttpMethod.Post, $"/v1.0/admin/tenants/{tenantId}/users", request, true, cancellationToken).ConfigureAwait(false);
            return data.User ?? new UserInfo();
        }

        /// <summary>
        /// Deletes a user.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the user is not found.</exception>
        public async Task DeleteUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
        {
            await MakeRequestAsync<DeleteData>(HttpMethod.Delete, $"/v1.0/admin/tenants/{tenantId}/users/{userId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Admin - Credential Management Endpoints ====================

        /// <summary>
        /// Lists all credentials in a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of credential information objects.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<List<CredentialInfo>> ListCredentialsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            CredentialsListData data = await MakeRequestAsync<CredentialsListData>(HttpMethod.Get, $"/v1.0/admin/tenants/{tenantId}/credentials", null, true, cancellationToken).ConfigureAwait(false);
            return data.Credentials ?? new List<CredentialInfo>();
        }

        /// <summary>
        /// Gets a specific credential.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="credentialId">The credential identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Credential information.</returns>
        /// <exception cref="VerbexException">Thrown when the credential is not found.</exception>
        public async Task<CredentialInfo> GetCredentialAsync(string tenantId, string credentialId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<CredentialInfo>(HttpMethod.Get, $"/v1.0/admin/tenants/{tenantId}/credentials/{credentialId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new credential (API key) in a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="description">Optional description.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created credential information (includes bearer token).</returns>
        /// <exception cref="VerbexException">Thrown when creation fails.</exception>
        public async Task<CredentialInfo> CreateCredentialAsync(
            string tenantId,
            string? description = null,
            CancellationToken cancellationToken = default)
        {
            CreateCredentialRequest request = new CreateCredentialRequest(description);
            CreateCredentialData data = await MakeRequestAsync<CreateCredentialData>(HttpMethod.Post, $"/v1.0/admin/tenants/{tenantId}/credentials", request, true, cancellationToken).ConfigureAwait(false);
            return data.Credential ?? new CredentialInfo();
        }

        /// <summary>
        /// Deletes a credential.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="credentialId">The credential identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the credential is not found.</exception>
        public async Task DeleteCredentialAsync(string tenantId, string credentialId, CancellationToken cancellationToken = default)
        {
            await MakeRequestAsync<DeleteData>(HttpMethod.Delete, $"/v1.0/admin/tenants/{tenantId}/credentials/{credentialId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Tenant Labels and Tags Endpoints ====================

        /// <summary>
        /// Updates labels on a tenant (full replacement).
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="labels">The new labels to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the tenant is not found.</exception>
        public async Task UpdateTenantLabelsAsync(
            string tenantId,
            List<string> labels,
            CancellationToken cancellationToken = default)
        {
            object request = new { Labels = labels ?? new List<string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/tenants/{tenantId}/labels", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates tags on a tenant (full replacement).
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="tags">The new tags to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the tenant is not found.</exception>
        public async Task UpdateTenantTagsAsync(
            string tenantId,
            Dictionary<string, string> tags,
            CancellationToken cancellationToken = default)
        {
            object request = new { Tags = tags ?? new Dictionary<string, string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/tenants/{tenantId}/tags", request, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== User Labels and Tags Endpoints ====================

        /// <summary>
        /// Updates labels on a user (full replacement).
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="labels">The new labels to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the user is not found.</exception>
        public async Task UpdateUserLabelsAsync(
            string tenantId,
            string userId,
            List<string> labels,
            CancellationToken cancellationToken = default)
        {
            object request = new { Labels = labels ?? new List<string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/tenants/{tenantId}/users/{userId}/labels", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates tags on a user (full replacement).
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="tags">The new tags to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the user is not found.</exception>
        public async Task UpdateUserTagsAsync(
            string tenantId,
            string userId,
            Dictionary<string, string> tags,
            CancellationToken cancellationToken = default)
        {
            object request = new { Tags = tags ?? new Dictionary<string, string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/tenants/{tenantId}/users/{userId}/tags", request, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Credential Labels and Tags Endpoints ====================

        /// <summary>
        /// Updates labels on a credential (full replacement).
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="credentialId">The credential identifier.</param>
        /// <param name="labels">The new labels to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the credential is not found.</exception>
        public async Task UpdateCredentialLabelsAsync(
            string tenantId,
            string credentialId,
            List<string> labels,
            CancellationToken cancellationToken = default)
        {
            object request = new { Labels = labels ?? new List<string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/tenants/{tenantId}/credentials/{credentialId}/labels", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates tags on a credential (full replacement).
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="credentialId">The credential identifier.</param>
        /// <param name="tags">The new tags to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the credential is not found.</exception>
        public async Task UpdateCredentialTagsAsync(
            string tenantId,
            string credentialId,
            Dictionary<string, string> tags,
            CancellationToken cancellationToken = default)
        {
            object request = new { Tags = tags ?? new Dictionary<string, string>() };
            await MakeRequestAsync(HttpMethod.Put, $"/v1.0/tenants/{tenantId}/credentials/{credentialId}/tags", request, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Custom Metadata Endpoints ====================

        /// <summary>
        /// Updates custom metadata on an index (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="customMetadata">The custom metadata object to set. Can be any JSON-serializable object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated index information.</returns>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task<IndexInfo> UpdateIndexCustomMetadataAsync(
            string indexId,
            object? customMetadata,
            CancellationToken cancellationToken = default)
        {
            object request = new { customMetadata };
            return await MakeRequestAsync<IndexInfo>(HttpMethod.Put, $"/v1.0/indices/{indexId}/customMetadata", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates custom metadata on a document (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="customMetadata">The custom metadata object to set. Can be any JSON-serializable object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated document information.</returns>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task<DocumentInfo> UpdateDocumentCustomMetadataAsync(
            string indexId,
            string documentId,
            object? customMetadata,
            CancellationToken cancellationToken = default)
        {
            object request = new { customMetadata };
            return await MakeRequestAsync<DocumentInfo>(HttpMethod.Put, $"/v1.0/indices/{indexId}/documents/{documentId}/customMetadata", request, true, cancellationToken).ConfigureAwait(false);
        }
    }
}
