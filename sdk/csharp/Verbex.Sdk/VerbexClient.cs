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

            _JsonOptions = new JsonSerializerOptions();
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

        private async Task<ApiResponse<T>> MakeRequestAsync<T>(
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

                return apiResponse;
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

        private async Task<ApiResponse> MakeRequestAsync(
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

                return apiResponse;
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
        /// <returns>Health check response.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<ApiResponse<HealthData>> RootHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<HealthData>(HttpMethod.Get, "/", null, false, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks server health via the /v1.0/health endpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health check response.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<ApiResponse<HealthData>> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<HealthData>(HttpMethod.Get, "/v1.0/health", null, false, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Authentication Endpoints ====================

        /// <summary>
        /// Authenticates and receives a bearer token.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Login response with token.</returns>
        /// <exception cref="VerbexException">Thrown when authentication fails.</exception>
        public async Task<ApiResponse<LoginData>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            LoginRequest request = new LoginRequest(username, password);
            return await MakeRequestAsync<LoginData>(HttpMethod.Post, "/v1.0/auth/login", request, false, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates the current bearer token.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation response.</returns>
        /// <exception cref="VerbexException">Thrown when validation fails.</exception>
        public async Task<ApiResponse<ValidationData>> ValidateTokenAsync(CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<ValidationData>(HttpMethod.Get, "/v1.0/auth/validate", null, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Index Management Endpoints ====================

        /// <summary>
        /// Lists all available indices.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of indices.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<ApiResponse<IndicesListData>> ListIndicesAsync(CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<IndicesListData>(HttpMethod.Get, "/v1.0/indices", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all indices as a list.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of IndexInfo objects.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<List<IndexInfo>> GetIndicesAsync(CancellationToken cancellationToken = default)
        {
            ApiResponse<IndicesListData> response = await ListIndicesAsync(cancellationToken).ConfigureAwait(false);
            return response.Data?.Indices ?? new List<IndexInfo>();
        }

        /// <summary>
        /// Creates a new index.
        /// </summary>
        /// <param name="id">Unique identifier for the index.</param>
        /// <param name="name">Display name for the index.</param>
        /// <param name="description">Description of the index.</param>
        /// <param name="repositoryFilename">Filename for persistence.</param>
        /// <param name="inMemory">Whether to use in-memory storage only.</param>
        /// <param name="storageMode">Storage mode (MemoryOnly, PersistenceOnly, Hybrid).</param>
        /// <param name="enableLemmatizer">Enable word lemmatization.</param>
        /// <param name="enableStopWordRemover">Enable stop word filtering.</param>
        /// <param name="minTokenLength">Minimum token length (0 to disable).</param>
        /// <param name="maxTokenLength">Maximum token length (0 to disable).</param>
        /// <param name="labels">Optional list of labels to associate with the index.</param>
        /// <param name="tags">Optional key-value tags to associate with the index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Created index response.</returns>
        /// <exception cref="VerbexException">Thrown when creation fails.</exception>
        public async Task<ApiResponse<CreateIndexData>> CreateIndexAsync(
            string id,
            string? name = null,
            string? description = null,
            string? repositoryFilename = null,
            bool inMemory = false,
            string storageMode = "MemoryOnly",
            bool enableLemmatizer = false,
            bool enableStopWordRemover = false,
            int minTokenLength = 0,
            int maxTokenLength = 0,
            List<string>? labels = null,
            Dictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            CreateIndexRequest request = new CreateIndexRequest(id, name)
            {
                Description = description ?? string.Empty,
                RepositoryFilename = repositoryFilename ?? $"{id}.db",
                InMemory = inMemory,
                StorageMode = storageMode,
                EnableLemmatizer = enableLemmatizer,
                EnableStopWordRemover = enableStopWordRemover,
                MinTokenLength = minTokenLength,
                MaxTokenLength = maxTokenLength,
                Labels = labels,
                Tags = tags
            };
            return await MakeRequestAsync<CreateIndexData>(HttpMethod.Post, "/v1.0/indices", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets detailed information about a specific index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Index details response.</returns>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task<ApiResponse<IndexInfo>> GetIndexAsync(string indexId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<IndexInfo>(HttpMethod.Get, $"/v1.0/indices/{indexId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets index information.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>IndexInfo object.</returns>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task<IndexInfo?> GetIndexInfoAsync(string indexId, CancellationToken cancellationToken = default)
        {
            ApiResponse<IndexInfo> response = await GetIndexAsync(indexId, cancellationToken).ConfigureAwait(false);
            return response.Data;
        }

        /// <summary>
        /// Deletes an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Deletion confirmation.</returns>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task<ApiResponse<DeleteIndexData>> DeleteIndexAsync(string indexId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<DeleteIndexData>(HttpMethod.Delete, $"/v1.0/indices/{indexId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates labels on an index (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="labels">The new labels to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Update confirmation with updated index info.</returns>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task<ApiResponse> UpdateIndexLabelsAsync(
            string indexId,
            List<string> labels,
            CancellationToken cancellationToken = default)
        {
            object request = new { Labels = labels ?? new List<string>() };
            return await MakeRequestAsync(HttpMethod.Put, $"/v1.0/indices/{indexId}/labels", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates tags on an index (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="tags">The new tags to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Update confirmation with updated index info.</returns>
        /// <exception cref="VerbexException">Thrown when the index is not found.</exception>
        public async Task<ApiResponse> UpdateIndexTagsAsync(
            string indexId,
            Dictionary<string, string> tags,
            CancellationToken cancellationToken = default)
        {
            object request = new { Tags = tags ?? new Dictionary<string, string>() };
            return await MakeRequestAsync(HttpMethod.Put, $"/v1.0/indices/{indexId}/tags", request, true, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Document Management Endpoints ====================

        /// <summary>
        /// Lists all documents in an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<ApiResponse<DocumentsListData>> ListDocumentsAsync(string indexId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<DocumentsListData>(HttpMethod.Get, $"/v1.0/indices/{indexId}/documents", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all documents in an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of DocumentInfo objects.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<List<DocumentInfo>> GetDocumentsAsync(string indexId, CancellationToken cancellationToken = default)
        {
            ApiResponse<DocumentsListData> response = await ListDocumentsAsync(indexId, cancellationToken).ConfigureAwait(false);
            return response.Data?.Documents ?? new List<DocumentInfo>();
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
        /// <returns>Document creation response.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<ApiResponse<AddDocumentData>> AddDocumentAsync(
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
        /// <returns>Document details.</returns>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task<ApiResponse<DocumentInfo>> GetDocumentAsync(string indexId, string documentId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<DocumentInfo>(HttpMethod.Get, $"/v1.0/indices/{indexId}/documents/{documentId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets document information.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>DocumentInfo object.</returns>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task<DocumentInfo?> GetDocumentInfoAsync(string indexId, string documentId, CancellationToken cancellationToken = default)
        {
            ApiResponse<DocumentInfo> response = await GetDocumentAsync(indexId, documentId, cancellationToken).ConfigureAwait(false);
            return response.Data;
        }

        /// <summary>
        /// Deletes a document from an index.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Deletion confirmation.</returns>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task<ApiResponse<DeleteDocumentData>> DeleteDocumentAsync(string indexId, string documentId, CancellationToken cancellationToken = default)
        {
            return await MakeRequestAsync<DeleteDocumentData>(HttpMethod.Delete, $"/v1.0/indices/{indexId}/documents/{documentId}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates labels on a document (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="labels">The new labels to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Update confirmation with updated document.</returns>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task<ApiResponse> UpdateDocumentLabelsAsync(
            string indexId,
            string documentId,
            List<string> labels,
            CancellationToken cancellationToken = default)
        {
            object request = new { Labels = labels ?? new List<string>() };
            return await MakeRequestAsync(HttpMethod.Put, $"/v1.0/indices/{indexId}/documents/{documentId}/labels", request, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates tags on a document (full replacement).
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="tags">The new tags to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Update confirmation with updated document.</returns>
        /// <exception cref="VerbexException">Thrown when the document is not found.</exception>
        public async Task<ApiResponse> UpdateDocumentTagsAsync(
            string indexId,
            string documentId,
            Dictionary<string, string> tags,
            CancellationToken cancellationToken = default)
        {
            object request = new { Tags = tags ?? new Dictionary<string, string>() };
            return await MakeRequestAsync(HttpMethod.Put, $"/v1.0/indices/{indexId}/documents/{documentId}/tags", request, true, cancellationToken).ConfigureAwait(false);
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
        /// <returns>Search results.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<ApiResponse<SearchData>> SearchAsync(
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

        /// <summary>
        /// Searches documents and returns search data.
        /// </summary>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="query">The search query.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="labels">Optional labels to filter by (AND logic, case-insensitive).</param>
        /// <param name="tags">Optional tags to filter by (AND logic, exact match).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>SearchData object.</returns>
        /// <exception cref="VerbexException">Thrown when the request fails.</exception>
        public async Task<SearchData?> SearchDocumentsAsync(
            string indexId,
            string query,
            int maxResults = 100,
            List<string>? labels = null,
            Dictionary<string, object>? tags = null,
            CancellationToken cancellationToken = default)
        {
            ApiResponse<SearchData> response = await SearchAsync(indexId, query, maxResults, labels, tags, cancellationToken).ConfigureAwait(false);
            return response.Data;
        }
    }
}
