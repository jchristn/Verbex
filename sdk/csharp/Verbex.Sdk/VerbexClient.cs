namespace Verbex.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Sdk.DTO.Requests;
    using Verbex.Sdk.DTO.Responses;

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
            EnumerationResult<IndexInfo> result = await ListIndicesAsync(null, cancellationToken).ConfigureAwait(false);
            return result.Objects;
        }

        /// <summary>
        /// Lists available indices with pagination support.
        /// </summary>
        /// <param name="options">Pagination options (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumeration result containing indices and pagination information.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<EnumerationResult<IndexInfo>> ListIndicesAsync(EnumerationOptions? options, CancellationToken cancellationToken = default)
        {
            string path = "/v1.0/indices";
            string queryString = options?.ToQueryString() ?? string.Empty;
            if (!string.IsNullOrEmpty(queryString))
            {
                path += "?" + queryString;
            }
            return await MakeRequestAsync<EnumerationResult<IndexInfo>>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all indices by iterating through all pages.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of all indices.</returns>
        public async IAsyncEnumerable<IndexInfo> ListAllIndicesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnumerationOptions options = new EnumerationOptions { MaxResults = 1000 };

            while (true)
            {
                EnumerationResult<IndexInfo> result = await ListIndicesAsync(options, cancellationToken).ConfigureAwait(false);

                foreach (IndexInfo index in result.Objects)
                {
                    yield return index;
                }

                if (result.EndOfResults || string.IsNullOrEmpty(result.ContinuationToken))
                {
                    break;
                }

                options = result.GetNextPageOptions()!;
            }
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
            EnumerationResult<DocumentInfo> result = await ListDocumentsAsync(indexId, null, cancellationToken).ConfigureAwait(false);
            return result.Objects;
        }

        /// <summary>
        /// Lists documents in an index with pagination support.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="options">Pagination options (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumeration result containing documents and pagination information.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<EnumerationResult<DocumentInfo>> ListDocumentsAsync(string indexId, EnumerationOptions? options, CancellationToken cancellationToken = default)
        {
            string path = $"/v1.0/indices/{indexId}/documents";
            string queryString = options?.ToQueryString() ?? string.Empty;
            if (!string.IsNullOrEmpty(queryString))
            {
                path += "?" + queryString;
            }
            return await MakeRequestAsync<EnumerationResult<DocumentInfo>>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all documents in an index by iterating through all pages.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of all documents.</returns>
        public async IAsyncEnumerable<DocumentInfo> ListAllDocumentsAsync(string indexId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnumerationOptions options = new EnumerationOptions { MaxResults = 1000 };

            while (true)
            {
                EnumerationResult<DocumentInfo> result = await ListDocumentsAsync(indexId, options, cancellationToken).ConfigureAwait(false);

                foreach (DocumentInfo doc in result.Objects)
                {
                    yield return doc;
                }

                if (result.EndOfResults || string.IsNullOrEmpty(result.ContinuationToken))
                {
                    break;
                }

                options = result.GetNextPageOptions()!;
            }
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
        /// Gets multiple documents by IDs from an index in a single request.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentIds">Collection of document IDs to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Batch result containing found documents and list of not found IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when indexId or documentIds is null.</exception>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<BatchRetrieveResponse> GetDocumentsBatchAsync(
            string indexId,
            IEnumerable<string> documentIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(indexId);
            ArgumentNullException.ThrowIfNull(documentIds);

            List<string> idList = new List<string>(documentIds);
            if (idList.Count == 0)
            {
                return new BatchRetrieveResponse();
            }

            // Join IDs with commas - don't URL-encode the entire string as commas are valid in query strings
            // Only escape individual IDs that might contain special characters
            string idsParam = string.Join(",", idList.Select(id => Uri.EscapeDataString(id)));
            return await MakeRequestAsync<BatchRetrieveResponse>(
                HttpMethod.Get,
                $"/v1.0/indices/{indexId}/documents?ids={idsParam}",
                null,
                true,
                cancellationToken).ConfigureAwait(false);
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
        /// Deletes multiple documents from an index by IDs in a single request.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentIds">Collection of document IDs to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Batch result containing lists of deleted and not found IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when indexId or documentIds is null.</exception>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<BatchDeleteResponse> DeleteDocumentsBatchAsync(
            string indexId,
            IEnumerable<string> documentIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(indexId);
            ArgumentNullException.ThrowIfNull(documentIds);

            List<string> idList = new List<string>(documentIds);
            if (idList.Count == 0)
            {
                return new BatchDeleteResponse();
            }

            // Join IDs with commas - escape individual IDs that might contain special characters
            string idsParam = string.Join(",", idList.Select(id => Uri.EscapeDataString(id)));
            return await MakeRequestAsync<BatchDeleteResponse>(
                HttpMethod.Delete,
                $"/v1.0/indices/{indexId}/documents?ids={idsParam}",
                null,
                true,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds multiple documents to an index in a single request.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documents">Collection of documents to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Batch result containing lists of added and failed documents.</returns>
        /// <exception cref="ArgumentNullException">Thrown when indexId or documents is null.</exception>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<BatchAddDocumentsResponse> AddDocumentsBatchAsync(
            string indexId,
            IEnumerable<BatchAddDocumentItem> documents,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(indexId);
            ArgumentNullException.ThrowIfNull(documents);

            List<BatchAddDocumentItem> docList = new List<BatchAddDocumentItem>(documents);
            if (docList.Count == 0)
            {
                return new BatchAddDocumentsResponse();
            }

            BatchAddDocumentsRequest request = new BatchAddDocumentsRequest { Documents = docList };
            return await MakeRequestAsync<BatchAddDocumentsResponse>(
                HttpMethod.Post,
                $"/v1.0/indices/{indexId}/documents/batch",
                request,
                true,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if multiple documents exist in an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentIds">Collection of document IDs to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Batch result containing lists of existing and not found IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when indexId or documentIds is null.</exception>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<BatchCheckExistenceResponse> CheckDocumentsExistAsync(
            string indexId,
            IEnumerable<string> documentIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(indexId);
            ArgumentNullException.ThrowIfNull(documentIds);

            List<string> idList = new List<string>(documentIds);
            if (idList.Count == 0)
            {
                return new BatchCheckExistenceResponse();
            }

            BatchCheckExistenceRequest request = new BatchCheckExistenceRequest { Ids = idList };
            return await MakeRequestAsync<BatchCheckExistenceResponse>(
                HttpMethod.Post,
                $"/v1.0/indices/{indexId}/documents/exists",
                request,
                true,
                cancellationToken).ConfigureAwait(false);
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
            Dictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            SearchRequest request = new SearchRequest(query, maxResults, labels, tags);
            return await MakeRequestAsync<SearchData>(HttpMethod.Post, $"/v1.0/indices/{indexId}/search", request, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Terms Endpoints ====================

        /// <summary>
        /// Gets the top terms in an index sorted by document frequency.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="limit">Maximum number of terms to return (default: 10).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary mapping terms to their document frequencies.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<Dictionary<string, int>> GetTopTermsAsync(
            string indexId,
            int limit = 10,
            CancellationToken cancellationToken = default)
        {
            string endpoint = $"/v1.0/indices/{indexId}/terms/top";
            if (limit != 10)
            {
                endpoint += $"?limit={limit}";
            }
            return await MakeRequestAsync<Dictionary<string, int>>(HttpMethod.Get, endpoint, null, false, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Backup & Restore Endpoints ====================

        /// <summary>
        /// Creates a backup of an index and returns it as a stream.
        /// The stream contains a ZIP archive with the index database and metadata.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A stream containing the backup ZIP archive.</returns>
        /// <exception cref="VerbexException">Thrown when the backup fails.</exception>
        public async Task<Stream> BackupAsync(string indexId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(indexId);

            string url = $"{_Endpoint}/v1.0/indices/{indexId}/backup";

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _AccessKey);

            HttpResponseMessage response = await _HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new VerbexException($"Backup failed: {errorBody}", (int)response.StatusCode);
            }

            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a backup of an index and returns it as a byte array.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A byte array containing the backup ZIP archive.</returns>
        /// <exception cref="VerbexException">Thrown when the backup fails.</exception>
        public async Task<byte[]> BackupAsBytesAsync(string indexId, CancellationToken cancellationToken = default)
        {
            using Stream stream = await BackupAsync(indexId, cancellationToken).ConfigureAwait(false);
            using MemoryStream memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Creates a backup of an index and saves it to a file.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="filePath">The file path to save the backup to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="VerbexException">Thrown when the backup fails.</exception>
        public async Task BackupToFileAsync(string indexId, string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            using Stream backupStream = await BackupAsync(indexId, cancellationToken).ConfigureAwait(false);
            using FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await backupStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Restores a backup to create a new index.
        /// </summary>
        /// <param name="backupStream">The backup stream (ZIP archive).</param>
        /// <param name="name">Optional new name for the restored index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the restored index.</returns>
        /// <exception cref="VerbexException">Thrown when the restore fails.</exception>
        public async Task<RestoreResult> RestoreAsync(Stream backupStream, string? name = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(backupStream);

            string url = $"{_Endpoint}/v1.0/indices/restore";

            using MultipartFormDataContent content = new MultipartFormDataContent();
            using MemoryStream ms = new MemoryStream();
            await backupStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            ms.Position = 0;

            StreamContent fileContent = new StreamContent(ms);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            content.Add(fileContent, "file", "backup.vbx");

            if (!string.IsNullOrEmpty(name))
            {
                content.Add(new StringContent(name), "name");
            }

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _AccessKey);
            request.Content = content;

            HttpResponseMessage response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new VerbexException($"Restore failed: {responseBody}", (int)response.StatusCode);
            }

            ApiResponse<RestoreResult>? apiResponse = JsonSerializer.Deserialize<ApiResponse<RestoreResult>>(responseBody, _JsonOptions);
            if (apiResponse?.Data == null)
            {
                throw new VerbexException("Failed to parse restore response");
            }

            return apiResponse.Data;
        }

        /// <summary>
        /// Restores a backup from a file to create a new index.
        /// </summary>
        /// <param name="filePath">The path to the backup file.</param>
        /// <param name="name">Optional new name for the restored index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the restored index.</returns>
        /// <exception cref="VerbexException">Thrown when the restore fails.</exception>
        public async Task<RestoreResult> RestoreFromFileAsync(string filePath, string? name = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return await RestoreAsync(fileStream, name, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Restores a backup by replacing an existing index.
        /// </summary>
        /// <param name="indexId">The index identifier to replace.</param>
        /// <param name="backupStream">The backup stream (ZIP archive).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the restored index.</returns>
        /// <exception cref="VerbexException">Thrown when the restore fails.</exception>
        public async Task<RestoreResult> RestoreReplaceAsync(string indexId, Stream backupStream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(indexId);
            ArgumentNullException.ThrowIfNull(backupStream);

            string url = $"{_Endpoint}/v1.0/indices/{indexId}/restore";

            using MultipartFormDataContent content = new MultipartFormDataContent();
            using MemoryStream ms = new MemoryStream();
            await backupStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            ms.Position = 0;

            StreamContent fileContent = new StreamContent(ms);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            content.Add(fileContent, "file", "backup.vbx");

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _AccessKey);
            request.Content = content;

            HttpResponseMessage response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new VerbexException($"Restore failed: {responseBody}", (int)response.StatusCode);
            }

            ApiResponse<RestoreResult>? apiResponse = JsonSerializer.Deserialize<ApiResponse<RestoreResult>>(responseBody, _JsonOptions);
            if (apiResponse?.Data == null)
            {
                throw new VerbexException("Failed to parse restore response");
            }

            return apiResponse.Data;
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
            EnumerationResult<TenantInfo> result = await ListTenantsAsync(null, cancellationToken).ConfigureAwait(false);
            return result.Objects;
        }

        /// <summary>
        /// Lists tenants with pagination support.
        /// </summary>
        /// <param name="options">Pagination options (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumeration result containing tenants and pagination information.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<EnumerationResult<TenantInfo>> ListTenantsAsync(EnumerationOptions? options, CancellationToken cancellationToken = default)
        {
            string path = "/v1.0/tenants";
            string queryString = options?.ToQueryString() ?? string.Empty;
            if (!string.IsNullOrEmpty(queryString))
            {
                path += "?" + queryString;
            }
            return await MakeRequestAsync<EnumerationResult<TenantInfo>>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all tenants by iterating through all pages.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of all tenants.</returns>
        public async IAsyncEnumerable<TenantInfo> ListAllTenantsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnumerationOptions options = new EnumerationOptions { MaxResults = 1000 };

            while (true)
            {
                EnumerationResult<TenantInfo> result = await ListTenantsAsync(options, cancellationToken).ConfigureAwait(false);

                foreach (TenantInfo tenant in result.Objects)
                {
                    yield return tenant;
                }

                if (result.EndOfResults || string.IsNullOrEmpty(result.ContinuationToken))
                {
                    break;
                }

                options = result.GetNextPageOptions()!;
            }
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
            EnumerationResult<UserInfo> result = await ListUsersAsync(tenantId, null, cancellationToken).ConfigureAwait(false);
            return result.Objects;
        }

        /// <summary>
        /// Lists users in a tenant with pagination support.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="options">Pagination options (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumeration result containing users and pagination information.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<EnumerationResult<UserInfo>> ListUsersAsync(string tenantId, EnumerationOptions? options, CancellationToken cancellationToken = default)
        {
            string path = $"/v1.0/tenants/{tenantId}/users";
            string queryString = options?.ToQueryString() ?? string.Empty;
            if (!string.IsNullOrEmpty(queryString))
            {
                path += "?" + queryString;
            }
            return await MakeRequestAsync<EnumerationResult<UserInfo>>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all users in a tenant by iterating through all pages.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of all users.</returns>
        public async IAsyncEnumerable<UserInfo> ListAllUsersAsync(string tenantId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnumerationOptions options = new EnumerationOptions { MaxResults = 1000 };

            while (true)
            {
                EnumerationResult<UserInfo> result = await ListUsersAsync(tenantId, options, cancellationToken).ConfigureAwait(false);

                foreach (UserInfo user in result.Objects)
                {
                    yield return user;
                }

                if (result.EndOfResults || string.IsNullOrEmpty(result.ContinuationToken))
                {
                    break;
                }

                options = result.GetNextPageOptions()!;
            }
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
            EnumerationResult<CredentialInfo> result = await ListCredentialsAsync(tenantId, null, cancellationToken).ConfigureAwait(false);
            return result.Objects;
        }

        /// <summary>
        /// Lists credentials in a tenant with pagination support.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="options">Pagination options (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumeration result containing credentials and pagination information.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<EnumerationResult<CredentialInfo>> ListCredentialsAsync(string tenantId, EnumerationOptions? options, CancellationToken cancellationToken = default)
        {
            string path = $"/v1.0/tenants/{tenantId}/credentials";
            string queryString = options?.ToQueryString() ?? string.Empty;
            if (!string.IsNullOrEmpty(queryString))
            {
                path += "?" + queryString;
            }
            return await MakeRequestAsync<EnumerationResult<CredentialInfo>>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all credentials in a tenant by iterating through all pages.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of all credentials.</returns>
        public async IAsyncEnumerable<CredentialInfo> ListAllCredentialsAsync(string tenantId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnumerationOptions options = new EnumerationOptions { MaxResults = 1000 };

            while (true)
            {
                EnumerationResult<CredentialInfo> result = await ListCredentialsAsync(tenantId, options, cancellationToken).ConfigureAwait(false);

                foreach (CredentialInfo credential in result.Objects)
                {
                    yield return credential;
                }

                if (result.EndOfResults || string.IsNullOrEmpty(result.ContinuationToken))
                {
                    break;
                }

                options = result.GetNextPageOptions()!;
            }
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
