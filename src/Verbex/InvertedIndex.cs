namespace Verbex
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database;
    using Verbex.Database.Interfaces;
    using Verbex.Database.Sqlite;
    using Verbex.DTO;
    using Verbex.DTO.Requests;
    using Verbex.DTO.Responses;
    using Verbex.Models;
    using Verbex.Utilities;

    /// <summary>
    /// Main inverted index implementation using DatabaseDriverBase for storage.
    /// Supports SQLite (in-memory and on-disk), PostgreSQL, MySQL, and SQL Server.
    /// Thread-safe for concurrent operations.
    /// </summary>
    public class InvertedIndex : IDisposable, IAsyncDisposable
    {
        private const string LocalTenantName = "local";
        private const string LocalTenantDescription = "Local tenant for standalone index usage";

        private readonly VerbexConfiguration _Configuration;
        private readonly DatabaseDriverBase _Driver;
        private readonly ITokenizer _Tokenizer;
        private readonly string _IndexName;
        private readonly bool _OwnsDriver;
        private readonly SemaphoreSlim _WriteLock = new SemaphoreSlim(1, 1);
        private string _TenantId = string.Empty;
        private string _IndexId = string.Empty;
        private string _TablePrefix = string.Empty;
        private bool _IsDisposed;
        private volatile int _DocumentsSinceLastFlush = 0;
        private IndexCacheManager? _CacheManager;
        private CacheInvalidator? _CacheInvalidator;
        private TermIdCache? _TermIdCache;

        /// <summary>
        /// Gets the configuration used by this index.
        /// </summary>
        public VerbexConfiguration Configuration => _Configuration;

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public string IndexName => _IndexName;

        /// <summary>
        /// Gets whether the index is open and ready for operations.
        /// </summary>
        public bool IsOpen => _Driver.IsOpen && !string.IsNullOrEmpty(_IndexId);

        /// <summary>
        /// Gets the underlying database driver.
        /// </summary>
        public DatabaseDriverBase Driver => _Driver;

        /// <summary>
        /// Initializes a new instance of the InvertedIndex class with configuration object.
        /// Creates an SQLite database driver based on the storage mode configuration.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="configuration">Configuration settings for the index.</param>
        /// <exception cref="ArgumentNullException">Thrown when indexName is null.</exception>
        /// <exception cref="ArgumentException">Thrown when indexName is empty or whitespace.</exception>
        public InvertedIndex(string indexName, VerbexConfiguration? configuration = null)
        {
            ArgumentNullException.ThrowIfNull(indexName);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty or whitespace.", nameof(indexName));
            }

            _IndexName = indexName;
            _Configuration = configuration?.Clone() ?? new VerbexConfiguration();
            _Tokenizer = _Configuration.Tokenizer ?? new DefaultTokenizer();
            _OwnsDriver = true;

            DatabaseSettings settings;
            if (_Configuration.StorageMode == StorageMode.InMemory)
            {
                settings = DatabaseSettings.CreateInMemory();
            }
            else
            {
                string directory = _Configuration.StorageDirectory ?? VerbexConfiguration.GetDefaultStorageDirectory(indexName);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string databasePath = Path.Combine(directory, _Configuration.DatabaseFilename);
                settings = DatabaseSettings.CreateSqliteFile(databasePath);
            }

            _Driver = new SqliteDatabaseDriver(settings);
        }

        /// <summary>
        /// Initializes a new instance of the InvertedIndex class with a custom database driver.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="driver">Database driver to use for storage.</param>
        /// <param name="configuration">Configuration settings for the index.</param>
        /// <exception cref="ArgumentNullException">Thrown when indexName or driver is null.</exception>
        /// <exception cref="ArgumentException">Thrown when indexName is empty or whitespace.</exception>
        public InvertedIndex(string indexName, DatabaseDriverBase driver, VerbexConfiguration? configuration = null)
        {
            ArgumentNullException.ThrowIfNull(indexName);
            ArgumentNullException.ThrowIfNull(driver);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty or whitespace.", nameof(indexName));
            }

            _IndexName = indexName;
            _Driver = driver;
            _Configuration = configuration?.Clone() ?? new VerbexConfiguration();
            _Tokenizer = _Configuration.Tokenizer ?? new DefaultTokenizer();
            _OwnsDriver = false;
        }

        /// <summary>
        /// Opens the index. Must be called before any operations.
        /// Initializes the database driver, creates/gets the local tenant, and creates/gets the index.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task OpenAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (!_Driver.IsOpen)
            {
                await _Driver.InitializeAsync(token).ConfigureAwait(false);
            }

            // Get or create the local tenant
            TenantMetadata? tenant = await _Driver.Tenants.ReadByNameAsync(LocalTenantName, token).ConfigureAwait(false);
            if (tenant == null)
            {
                tenant = new TenantMetadata(LocalTenantName)
                {
                    Description = LocalTenantDescription
                };
                tenant = await _Driver.Tenants.CreateAsync(tenant, token).ConfigureAwait(false);
            }
            _TenantId = tenant.Identifier;

            // Get or create the index
            bool isNewIndex = false;
            IndexMetadata? index = await _Driver.Indexes.ReadByNameAsync(_TenantId, _IndexName, token).ConfigureAwait(false);
            if (index == null)
            {
                index = new IndexMetadata
                {
                    TenantId = _TenantId,
                    Name = _IndexName,
                    Description = $"Index: {_IndexName}"
                };
                index = await _Driver.Indexes.CreateAsync(index, token).ConfigureAwait(false);
                isNewIndex = true;
            }
            _IndexId = index.Identifier;
            _TablePrefix = TablePrefixValidator.FromIndexId(_IndexId);

            // Create the prefixed tables for the index if this is a new index
            if (isNewIndex)
            {
                await _Driver.CreateIndexTablesAsync(_TablePrefix, token).ConfigureAwait(false);
            }

            await InitializeCachesAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Opens the index with pre-existing tenant and index identifiers.
        /// Use this when the index metadata already exists in the database (e.g., server-managed indices).
        /// The prefixed tables must already exist; this method will not create them.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="indexId">The index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or indexId is null or empty.</exception>
        public async Task OpenAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId));
            }

            if (string.IsNullOrEmpty(indexId))
            {
                throw new ArgumentNullException(nameof(indexId));
            }

            if (!_Driver.IsOpen)
            {
                await _Driver.InitializeAsync(token).ConfigureAwait(false);
            }

            _TenantId = tenantId;
            _IndexId = indexId;
            _TablePrefix = TablePrefixValidator.FromIndexId(_IndexId);

            await InitializeCachesAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Initializes caches based on configuration.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        private async Task InitializeCachesAsync(CancellationToken token)
        {
            // Initialize cache manager if caching is enabled
            if (_Configuration.CacheConfiguration.Enabled)
            {
                _CacheManager = new IndexCacheManager(_IndexId, _Configuration.CacheConfiguration);
                _CacheInvalidator = new CacheInvalidator(_CacheManager);
            }

            // Initialize term ID cache for optimized ingestion
            if (_Configuration.CacheConfiguration.EnableTermIdCache)
            {
                Dictionary<string, string> termIds = await _Driver.Terms.GetAllTermIdsAsync(_TablePrefix, token).ConfigureAwait(false);
                int initialCapacity = Math.Max(termIds.Count, _Configuration.CacheConfiguration.TermIdCacheInitialCapacity);
                _TermIdCache = new TermIdCache(_TablePrefix, initialCapacity);
                _TermIdCache.Load(termIds);
            }
        }

        /// <summary>
        /// Closes the index.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task CloseAsync(CancellationToken token = default)
        {
            // Dispose cache manager
            _CacheManager?.Dispose();
            _CacheManager = null;
            _CacheInvalidator = null;

            if (_Driver.IsOpen && _OwnsDriver)
            {
                await _Driver.CloseAsync(token).ConfigureAwait(false);
            }
            _TenantId = string.Empty;
            _IndexId = string.Empty;
            _TablePrefix = string.Empty;
        }

        #region Document Operations

        /// <summary>
        /// Gets the total number of documents in the index.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document count.</returns>
        public async Task<long> GetDocumentCountAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            // Try cache first
            if (_CacheManager != null && _CacheManager.TryGetDocumentCount(out long cachedCount))
            {
                return cachedCount;
            }

            long count = await _Driver.Documents.GetCountAsync(_TablePrefix, token).ConfigureAwait(false);

            // Cache the result
            _CacheManager?.SetDocumentCount(count);

            return count;
        }

        /// <summary>
        /// Adds a document to the index.
        /// </summary>
        /// <param name="documentName">Name/path of the document.</param>
        /// <param name="content">Document content to index.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The document ID.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentName or content is null.</exception>
        public async Task<string> AddDocumentAsync(string documentName, string content, CancellationToken token = default)
        {
            AddDocumentResult result = await AddDocumentWithMetricsAsync(documentName, content, token).ConfigureAwait(false);
            return result.DocumentId;
        }

        /// <summary>
        /// Adds a document to the index and returns detailed ingestion metrics.
        /// </summary>
        /// <param name="documentName">Name/path of the document.</param>
        /// <param name="content">Document content to index.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result containing the document ID and ingestion metrics.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentName or content is null.</exception>
        public async Task<AddDocumentResult> AddDocumentWithMetricsAsync(string documentName, string content, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentName);
            ArgumentNullException.ThrowIfNull(content);

            Stopwatch stopwatch = Stopwatch.StartNew();

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            double lockWaitMs = stopwatch.Elapsed.TotalMilliseconds;

            AddDocumentResult addResult;
            bool shouldFlush = false;

            try
            {
                string documentId = IdGenerator.GenerateDocumentId();
                string contentSha256 = ComputeContentHash(content);

                await _Driver.Documents.AddAsync(_TablePrefix, documentId, documentName, contentSha256, content.Length, null, null, token).ConfigureAwait(false);

                IndexContentResult result = await IndexDocumentContentAsync(documentId, documentName, content, stopwatch, token).ConfigureAwait(false);
                result.Metrics.Steps.LockWaitMs = lockWaitMs;

                // Invalidate cache (using terms already computed during indexing)
                _CacheInvalidator?.OnDocumentAdded(documentId, result.AffectedTerms);

                // Check if flush is needed (increment counter inside lock, flush outside)
                shouldFlush = ShouldAutoFlush();

                addResult = new AddDocumentResult(documentId, result.Metrics);
            }
            finally
            {
                _WriteLock.Release();
            }

            // Perform flush outside the write lock to avoid blocking other ingestions
            if (shouldFlush)
            {
                await PerformAutoFlushAsync(token).ConfigureAwait(false);
            }

            return addResult;
        }

        /// <summary>
        /// Adds a document to the index with a specific ID.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="documentName">Name/path of the document.</param>
        /// <param name="content">Document content to index.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentName or content is null.</exception>
        /// <exception cref="ArgumentException">Thrown when documentId is empty.</exception>
        public async Task AddDocumentAsync(string documentId, string documentName, string content, CancellationToken token = default)
        {
            await AddDocumentWithMetricsAsync(documentId, documentName, content, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a document to the index with a specific ID and returns detailed ingestion metrics.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="documentName">Name/path of the document.</param>
        /// <param name="content">Document content to index.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result containing the document ID and ingestion metrics.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentId, documentName, or content is null.</exception>
        /// <exception cref="ArgumentException">Thrown when documentId is empty.</exception>
        public async Task<AddDocumentResult> AddDocumentWithMetricsAsync(string documentId, string documentName, string content, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));
            }

            ArgumentNullException.ThrowIfNull(documentName);
            ArgumentNullException.ThrowIfNull(content);

            Stopwatch stopwatch = Stopwatch.StartNew();

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            double lockWaitMs = stopwatch.Elapsed.TotalMilliseconds;

            AddDocumentResult addResult;
            bool shouldFlush = false;

            try
            {
                string contentSha256 = ComputeContentHash(content);

                await _Driver.Documents.AddAsync(_TablePrefix, documentId, documentName, contentSha256, content.Length, null, null, token).ConfigureAwait(false);

                IndexContentResult result = await IndexDocumentContentAsync(documentId, documentName, content, stopwatch, token).ConfigureAwait(false);
                result.Metrics.Steps.LockWaitMs = lockWaitMs;

                // Invalidate cache (using terms already computed during indexing)
                _CacheInvalidator?.OnDocumentAdded(documentId, result.AffectedTerms);

                // Check if flush is needed (increment counter inside lock, flush outside)
                shouldFlush = ShouldAutoFlush();

                addResult = new AddDocumentResult(documentId, result.Metrics);
            }
            finally
            {
                _WriteLock.Release();
            }

            // Perform flush outside the write lock to avoid blocking other ingestions
            if (shouldFlush)
            {
                await PerformAutoFlushAsync(token).ConfigureAwait(false);
            }

            return addResult;
        }

        /// <summary>
        /// Gets a document by ID.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata or null if not found.</returns>
        public async Task<DocumentMetadata?> GetDocumentAsync(string documentId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            return await _Driver.Documents.GetAsync(_TablePrefix, documentId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a document by ID with all metadata (labels, tags, terms) populated in a single query.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata with labels, tags, and terms populated, or null if not found.</returns>
        public async Task<DocumentMetadata?> GetDocumentWithMetadataAsync(string documentId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            return await _Driver.Documents.GetWithMetadataAsync(_TablePrefix, documentId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a document by name.
        /// </summary>
        /// <param name="documentName">Document name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata or null if not found.</returns>
        public async Task<DocumentMetadata?> GetDocumentByNameAsync(string documentName, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentName);

            return await _Driver.Documents.GetByNameAsync(_TablePrefix, documentName, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all documents with pagination.
        /// </summary>
        /// <param name="limit">Maximum number of documents.</param>
        /// <param name="offset">Number of documents to skip.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        public async Task<List<DocumentMetadata>> GetDocumentsAsync(int limit = 100, int offset = 0, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            return await _Driver.Documents.GetAllAsync(_TablePrefix, limit, offset, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets multiple documents by their IDs with full metadata (labels, tags, custom metadata) populated.
        /// Documents that are not found are silently omitted from the result.
        /// </summary>
        /// <param name="documentIds">Collection of document IDs to retrieve.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents that were found with all metadata populated.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentIds is null.</exception>
        public async Task<List<DocumentMetadata>> GetDocumentsWithMetadataAsync(IEnumerable<string> documentIds, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentIds);

            List<DocumentMetadata> results = new List<DocumentMetadata>();
            foreach (string docId in documentIds.Distinct())
            {
                if (string.IsNullOrWhiteSpace(docId))
                {
                    continue;
                }

                DocumentMetadata? doc = await GetDocumentWithMetadataAsync(docId, token).ConfigureAwait(false);
                if (doc != null)
                {
                    results.Add(doc);
                }
            }
            return results;
        }

        /// <summary>
        /// Checks if a document exists by ID.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document exists.</returns>
        public async Task<bool> DocumentExistsAsync(string documentId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            return await _Driver.Documents.ExistsAsync(_TablePrefix, documentId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if a document exists by name.
        /// </summary>
        /// <param name="documentName">Document name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document exists.</returns>
        public async Task<bool> DocumentExistsByNameAsync(string documentName, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentName);

            return await _Driver.Documents.ExistsByNameAsync(_TablePrefix, documentName, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if multiple documents exist by their IDs.
        /// Returns lists of which IDs exist and which do not.
        /// </summary>
        /// <param name="documentIds">Collection of document IDs to check.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A response containing the list of existing IDs and the list of not-found IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentIds is null.</exception>
        public async Task<BatchCheckExistenceResponse> DocumentsExistBatchAsync(IEnumerable<string> documentIds, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentIds);

            List<string> requestedIds = documentIds.Distinct().Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (requestedIds.Count == 0)
            {
                return new BatchCheckExistenceResponse();
            }

            List<string> existingIds = new List<string>();
            List<string> notFoundIds = new List<string>();

            foreach (string docId in requestedIds)
            {
                bool exists = await _Driver.Documents.ExistsAsync(_TablePrefix, docId, token).ConfigureAwait(false);
                if (exists)
                {
                    existingIds.Add(docId);
                }
                else
                {
                    notFoundIds.Add(docId);
                }
            }

            return new BatchCheckExistenceResponse
            {
                Exists = existingIds,
                NotFound = notFoundIds,
                ExistsCount = existingIds.Count,
                NotFoundCount = notFoundIds.Count,
                RequestedCount = requestedIds.Count
            };
        }

        /// <summary>
        /// Adds multiple documents to the index in a batch operation.
        /// This method processes each document individually but within a single context.
        /// </summary>
        /// <param name="documents">Collection of documents to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A response containing the list of successfully added document results and the list of failed document results.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documents is null.</exception>
        public async Task<BatchAddDocumentsResponse> AddDocumentsBatchAsync(
            IEnumerable<BatchAddDocumentItem> documents,
            CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documents);

            List<BatchAddDocumentItem> docList = documents.ToList();
            if (docList.Count == 0)
            {
                return new BatchAddDocumentsResponse();
            }

            List<BatchAddDocumentResult> addedDocs = new List<BatchAddDocumentResult>();
            List<BatchAddDocumentResult> failedDocs = new List<BatchAddDocumentResult>();

            foreach (BatchAddDocumentItem doc in docList)
            {
                try
                {
                    string documentId;
                    if (!string.IsNullOrEmpty(doc.Id))
                    {
                        await AddDocumentAsync(doc.Id, doc.Name, doc.Content, token).ConfigureAwait(false);
                        documentId = doc.Id;
                    }
                    else
                    {
                        documentId = await AddDocumentAsync(doc.Name, doc.Content, token).ConfigureAwait(false);
                    }

                    // Add labels if provided
                    if (doc.Labels != null && doc.Labels.Count > 0)
                    {
                        await AddLabelsBatchAsync(documentId, doc.Labels, token).ConfigureAwait(false);
                    }

                    // Add tags if provided
                    if (doc.Tags != null && doc.Tags.Count > 0)
                    {
                        await AddTagsBatchAsync(documentId, doc.Tags, token).ConfigureAwait(false);
                    }

                    // Set custom metadata if provided
                    if (doc.CustomMetadata != null)
                    {
                        await SetCustomMetadataAsync(documentId, doc.CustomMetadata, token).ConfigureAwait(false);
                    }

                    addedDocs.Add(BatchAddDocumentResult.Successful(documentId, doc.Name));
                }
                catch (Exception ex)
                {
                    failedDocs.Add(BatchAddDocumentResult.Failed(doc.Name, ex.Message));
                }
            }

            return new BatchAddDocumentsResponse
            {
                Added = addedDocs,
                Failed = failedDocs,
                AddedCount = addedDocs.Count,
                FailedCount = failedDocs.Count,
                RequestedCount = docList.Count
            };
        }

        /// <summary>
        /// Removes a document from the index.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document was removed.</returns>
        public async Task<bool> RemoveDocumentAsync(string documentId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<DocumentTermRecord> docTerms = await _Driver.DocumentTerms.GetByDocumentAsync(_TablePrefix, documentId, token).ConfigureAwait(false);

                // Wrap all deletion operations in a transaction for atomicity
                await _Driver.BeginTransactionAsync(token).ConfigureAwait(false);
                try
                {
                    // Batch decrement term frequencies (single UPDATE instead of N)
                    if (docTerms.Count > 0)
                    {
                        Dictionary<string, FrequencyDelta> decrements =
                            new Dictionary<string, FrequencyDelta>();
                        foreach (DocumentTermRecord docTerm in docTerms)
                        {
                            decrements[docTerm.TermId] = new FrequencyDelta(1, docTerm.TermFrequency);
                        }
                        await _Driver.Terms.DecrementFrequenciesBatchAsync(_TablePrefix, decrements, token).ConfigureAwait(false);
                    }

                    await _Driver.DocumentTerms.DeleteByDocumentAsync(_TablePrefix, documentId, token).ConfigureAwait(false);

                    bool deleted = await _Driver.Documents.DeleteAsync(_TablePrefix, documentId, token).ConfigureAwait(false);

                    // Delete orphaned terms and get the list of deleted term texts for cache invalidation
                    List<string> deletedTermTexts = await _Driver.Terms.DeleteOrphanedAsync(_TablePrefix, token).ConfigureAwait(false);

                    await _Driver.CommitTransactionAsync(token).ConfigureAwait(false);

                    // Invalidate caches after successful commit
                    if (deleted)
                    {
                        List<string> affectedTermIds = docTerms.Select(dt => dt.TermId).ToList();
                        _CacheInvalidator?.OnDocumentRemoved(documentId, affectedTermIds);
                    }

                    // Invalidate TermIdCache for deleted orphan terms
                    if (deletedTermTexts.Count > 0)
                    {
                        _TermIdCache?.RemoveRange(deletedTermTexts);
                    }

                    return deleted;
                }
                catch
                {
                    await _Driver.RollbackTransactionAsync(token).ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        /// <summary>
        /// Removes multiple documents from the index in a single batch operation.
        /// This is more efficient than calling RemoveDocumentAsync multiple times.
        /// </summary>
        /// <param name="documentIds">Collection of document IDs to remove.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A response containing the list of deleted IDs and the list of not-found IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentIds is null.</exception>
        public async Task<BatchDeleteResponse> RemoveDocumentsBatchAsync(IEnumerable<string> documentIds, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentIds);

            List<string> requestedIds = documentIds.Distinct().Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (requestedIds.Count == 0)
            {
                return new BatchDeleteResponse();
            }

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                // Step 1: Get all document-terms for all requested documents in a single query
                List<DocumentTermRecord> allDocTerms = await _Driver.DocumentTerms.GetByDocumentsAsync(_TablePrefix, requestedIds, token).ConfigureAwait(false);

                // Wrap all deletion operations in a transaction for atomicity
                await _Driver.BeginTransactionAsync(token).ConfigureAwait(false);
                try
                {
                    // Step 2: Aggregate term frequency decrements across all documents
                    if (allDocTerms.Count > 0)
                    {
                        Dictionary<string, FrequencyDelta> decrements = new Dictionary<string, FrequencyDelta>();
                        foreach (DocumentTermRecord docTerm in allDocTerms)
                        {
                            if (decrements.TryGetValue(docTerm.TermId, out FrequencyDelta? existing))
                            {
                                decrements[docTerm.TermId] = new FrequencyDelta(existing.DocFreqDelta + 1, existing.TotalFreqDelta + docTerm.TermFrequency);
                            }
                            else
                            {
                                decrements[docTerm.TermId] = new FrequencyDelta(1, docTerm.TermFrequency);
                            }
                        }

                        // Step 3: Single batch decrement for all term frequencies
                        await _Driver.Terms.DecrementFrequenciesBatchAsync(_TablePrefix, decrements, token).ConfigureAwait(false);
                    }

                    // Step 4: Delete all document-terms in one statement
                    await _Driver.DocumentTerms.DeleteByDocumentsAsync(_TablePrefix, requestedIds, token).ConfigureAwait(false);

                    // Step 5: Delete all documents in one statement and get back which ones existed
                    List<string> deletedIds = await _Driver.Documents.DeleteBatchAsync(_TablePrefix, requestedIds, token).ConfigureAwait(false);

                    // Step 6: Run orphan cleanup once at the end and get deleted term texts for cache invalidation
                    List<string> deletedTermTexts = await _Driver.Terms.DeleteOrphanedAsync(_TablePrefix, token).ConfigureAwait(false);

                    await _Driver.CommitTransactionAsync(token).ConfigureAwait(false);

                    // Step 7: Calculate not-found IDs
                    HashSet<string> deletedSet = new HashSet<string>(deletedIds);
                    List<string> notFoundIds = requestedIds.Where(id => !deletedSet.Contains(id)).ToList();

                    // Invalidate caches after successful commit
                    if (deletedIds.Count > 0)
                    {
                        List<string> affectedTermIds = allDocTerms.Select(dt => dt.TermId).Distinct().ToList();
                        _CacheInvalidator?.OnDocumentsRemoved(deletedIds, affectedTermIds);
                    }

                    // Invalidate TermIdCache for deleted orphan terms
                    if (deletedTermTexts.Count > 0)
                    {
                        _TermIdCache?.RemoveRange(deletedTermTexts);
                    }

                    return new BatchDeleteResponse
                    {
                        Deleted = deletedIds,
                        NotFound = notFoundIds,
                        DeletedCount = deletedIds.Count,
                        NotFoundCount = notFoundIds.Count,
                        RequestedCount = requestedIds.Count
                    };
                }
                catch
                {
                    await _Driver.RollbackTransactionAsync(token).ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        /// <summary>
        /// Removes all documents from the index.
        /// Also clears all terms, labels, and tags via cascade delete.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of documents removed.</returns>
        public async Task<long> ClearAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                long documentCount = await _Driver.Documents.DeleteAllAsync(_TablePrefix, token).ConfigureAwait(false);

                await _Driver.Terms.DeleteAllAsync(_TablePrefix, token).ConfigureAwait(false);

                // Invalidate all caches
                _CacheInvalidator?.OnIndexCleared();
                _TermIdCache?.Clear();

                return documentCount;
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        #endregion

        #region Search Operations

        /// <summary>
        /// Searches the index for documents matching the query.
        /// </summary>
        /// <param name="query">Search query string.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <param name="useAndLogic">If true, documents must contain all terms. If false, any term.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search results.</returns>
        public Task<SearchResults> SearchAsync(string query, int? maxResults = null, bool useAndLogic = false, CancellationToken token = default)
        {
            return SearchAsync(query, maxResults, useAndLogic, null, null, token);
        }

        /// <summary>
        /// Searches the index for documents matching the query with optional label and tag filtering.
        /// </summary>
        /// <param name="query">Search query string.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <param name="useAndLogic">If true, documents must contain all terms. If false, any term.</param>
        /// <param name="labels">Optional list of labels to filter by (documents must have ALL labels).</param>
        /// <param name="tags">Optional dictionary of tags to filter by (documents must have ALL tags).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search results.</returns>
        public async Task<SearchResults> SearchAsync(string query, int? maxResults, bool useAndLogic, List<string>? labels, Dictionary<string, string>? tags, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (string.IsNullOrWhiteSpace(query))
            {
                return new SearchResults(new List<SearchResult>(), 0, TimeSpan.Zero);
            }

            DateTime startTime = DateTime.UtcNow;

            List<string> queryTerms = TokenizeAndProcess(query);
            if (queryTerms.Count == 0)
            {
                return new SearchResults(new List<SearchResult>(), 0, TimeSpan.Zero);
            }

            // Try to get terms from cache first
            Dictionary<string, TermRecord> termRecords = new Dictionary<string, TermRecord>();
            List<string> termsToFetch = new List<string>();

            if (_CacheManager != null)
            {
                foreach (string term in queryTerms)
                {
                    if (_CacheManager.TryGetTerm(term, out TermRecord? cachedTerm) && cachedTerm != null)
                    {
                        termRecords[term] = cachedTerm;
                    }
                    else
                    {
                        termsToFetch.Add(term);
                    }
                }
            }
            else
            {
                termsToFetch = queryTerms;
            }

            // Fetch remaining terms from database
            if (termsToFetch.Count > 0)
            {
                Dictionary<string, TermRecord> fetchedTerms = await _Driver.Terms.GetMultipleAsync(_TablePrefix, termsToFetch, token).ConfigureAwait(false);

                foreach (KeyValuePair<string, TermRecord> kvp in fetchedTerms)
                {
                    termRecords[kvp.Key] = kvp.Value;
                    _CacheManager?.SetTerm(kvp.Key, kvp.Value);
                }
            }

            if (termRecords.Count == 0)
            {
                return new SearchResults(new List<SearchResult>(), 0, DateTime.UtcNow - startTime);
            }

            // When using AND logic, ALL query terms must exist in the index
            // If any term doesn't exist, no document can match all terms
            if (useAndLogic && termRecords.Count < queryTerms.Count)
            {
                return new SearchResults(new List<SearchResult>(), 0, DateTime.UtcNow - startTime);
            }

            List<string> termIds = termRecords.Values.Select(t => t.Id).ToList();

            int limit = maxResults ?? _Configuration.DefaultMaxSearchResults;
            List<SearchMatch> matches = await _Driver.DocumentTerms.SearchAsync(_TablePrefix, termIds, useAndLogic, limit, labels, tags, token).ConfigureAwait(false);

            if (matches.Count == 0)
            {
                return new SearchResults(new List<SearchResult>(), 0, DateTime.UtcNow - startTime);
            }

            List<string> docIds = matches.Select(m => m.DocumentId).ToList();

            // Fetch per-term frequencies for all matched documents
            List<DocumentTermRecord> documentTermRecords = await _Driver.DocumentTerms.GetByDocumentsAndTermsAsync(_TablePrefix, docIds, termIds, token).ConfigureAwait(false);

            // Build lookup: documentId -> (termId -> frequency)
            Dictionary<string, Dictionary<string, int>> perDocTermFrequencies = new Dictionary<string, Dictionary<string, int>>();
            foreach (DocumentTermRecord dtr in documentTermRecords)
            {
                if (!perDocTermFrequencies.TryGetValue(dtr.DocumentId, out Dictionary<string, int>? termFreqs))
                {
                    termFreqs = new Dictionary<string, int>();
                    perDocTermFrequencies[dtr.DocumentId] = termFreqs;
                }
                termFreqs[dtr.TermId] = dtr.TermFrequency;
            }

            // Populate TermFrequencies in each SearchMatch
            foreach (SearchMatch match in matches)
            {
                if (perDocTermFrequencies.TryGetValue(match.DocumentId, out Dictionary<string, int>? termFreqs))
                {
                    match.TermFrequencies = termFreqs;
                }
            }

            List<DocumentMetadata> documents = await _Driver.Documents.GetByIdsAsync(_TablePrefix, docIds, token).ConfigureAwait(false);

            Dictionary<string, DocumentMetadata> docLookup = documents.ToDictionary(d => d.DocumentId);

            long totalDocs = await _Driver.Documents.GetCountAsync(_TablePrefix, token).ConfigureAwait(false);

            List<SearchResult> results = new List<SearchResult>();
            foreach (SearchMatch match in matches)
            {
                if (!docLookup.TryGetValue(match.DocumentId, out DocumentMetadata? doc))
                {
                    continue;
                }

                double score = CalculateScore(match, termRecords, totalDocs);

                results.Add(new SearchResult(doc, score, match.MatchedTermCount));
            }

            results = results.OrderByDescending(r => r.Score).Take(limit).ToList();

            TimeSpan searchTime = DateTime.UtcNow - startTime;
            return new SearchResults(results, results.Count, searchTime);
        }

        /// <summary>
        /// Checks if a term exists in the index.
        /// </summary>
        /// <param name="term">Term to check.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if term exists.</returns>
        public async Task<bool> TermExistsAsync(string term, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(term);

            string normalizedTerm = NormalizeTerm(term);
            if (string.IsNullOrEmpty(normalizedTerm))
            {
                return false;
            }

            return await _Driver.Terms.ExistsAsync(_TablePrefix, normalizedTerm, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the posting list for a term (documents containing the term).
        /// </summary>
        /// <param name="term">Term to look up.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records.</returns>
        public async Task<List<DocumentTermRecord>> GetPostingsAsync(string term, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(term);

            string normalizedTerm = NormalizeTerm(term);
            if (string.IsNullOrEmpty(normalizedTerm))
            {
                return new List<DocumentTermRecord>();
            }

            TermRecord? termRecord = await _Driver.Terms.GetAsync(_TablePrefix, normalizedTerm, token).ConfigureAwait(false);
            if (termRecord == null)
            {
                return new List<DocumentTermRecord>();
            }

            return await _Driver.DocumentTerms.GetPostingsByTermAsync(_TablePrefix, termRecord.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets terms matching a prefix.
        /// </summary>
        /// <param name="prefix">Prefix to match.</param>
        /// <param name="limit">Maximum number of terms.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching terms.</returns>
        public async Task<List<TermRecord>> GetTermsByPrefixAsync(string prefix, int limit = 100, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(prefix);

            return await _Driver.Terms.GetByPrefixAsync(_TablePrefix, prefix.ToLowerInvariant(), limit, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all terms indexed for a specific document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records containing term information.</returns>
        public async Task<List<DocumentTermRecord>> GetDocumentTermsAsync(string documentId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            return await _Driver.DocumentTerms.GetByDocumentAsync(_TablePrefix, documentId, token).ConfigureAwait(false);
        }

        #endregion

        #region Label Operations

        /// <summary>
        /// Adds a label to a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="label">Label text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task AddLabelAsync(string documentId, string label, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(label);

            string labelId = IdGenerator.GenerateLabelId();
            await _Driver.Labels.AddAsync(_TablePrefix, labelId, documentId, label.ToLowerInvariant(), token).ConfigureAwait(false);

            // Invalidate document cache
            _CacheInvalidator?.OnDocumentUpdated(documentId);
        }

        /// <summary>
        /// Batch adds labels to a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="labels">List of labels to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task AddLabelsBatchAsync(string documentId, List<string> labels, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(labels);

            if (labels.Count == 0)
            {
                return;
            }

            List<LabelRecord> records = labels.Select(l => new LabelRecord
            {
                Id = IdGenerator.GenerateLabelId(),
                DocumentId = documentId,
                Label = l.ToLowerInvariant()
            }).ToList();

            await _Driver.Labels.AddBatchAsync(_TablePrefix, records, token).ConfigureAwait(false);

            // Invalidate document cache
            _CacheInvalidator?.OnDocumentUpdated(documentId);
        }

        /// <summary>
        /// Replaces all labels on a document with the provided list (deletes all existing, then adds new).
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="labels">List of labels to set.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ReplaceLabelsAsync(string documentId, List<string> labels, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(labels);

            List<string> normalizedLabels = labels.Select(l => l.ToLowerInvariant()).ToList();
            await _Driver.Labels.ReplaceAsync(_TablePrefix, documentId, normalizedLabels, token).ConfigureAwait(false);

            // Invalidate document cache
            _CacheInvalidator?.OnDocumentUpdated(documentId);
        }

        /// <summary>
        /// Adds an index-level label.
        /// </summary>
        /// <param name="label">Label text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task AddIndexLabelAsync(string label, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(label);

            string labelId = IdGenerator.GenerateLabelId();
            await _Driver.Labels.AddAsync(_TablePrefix, labelId, null, label.ToLowerInvariant(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets labels for a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        public async Task<List<string>> GetLabelsAsync(string documentId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            return await _Driver.Labels.GetByDocumentAsync(_TablePrefix, documentId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets index-level labels.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        public async Task<List<string>> GetIndexLabelsAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            return await _Driver.Labels.GetIndexLabelsAsync(_TablePrefix, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets documents with a specific label.
        /// </summary>
        /// <param name="label">Label to filter by.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs.</returns>
        public async Task<List<string>> GetDocumentIdsByLabelAsync(string label, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(label);

            return await _Driver.Labels.GetDocumentsByLabelAsync(_TablePrefix, label.ToLowerInvariant(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes a label from a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="label">Label to remove.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if label was removed.</returns>
        public async Task<bool> RemoveLabelAsync(string documentId, string label, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(label);

            bool removed = await _Driver.Labels.RemoveAsync(_TablePrefix, documentId, label.ToLowerInvariant(), token).ConfigureAwait(false);

            // Invalidate document cache
            if (removed)
            {
                _CacheInvalidator?.OnDocumentUpdated(documentId);
            }

            return removed;
        }

        /// <summary>
        /// Removes an index-level label.
        /// </summary>
        /// <param name="label">Label to remove.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if label was removed.</returns>
        public async Task<bool> RemoveIndexLabelAsync(string label, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(label);

            return await _Driver.Labels.RemoveIndexLabelAsync(_TablePrefix, label.ToLowerInvariant(), token).ConfigureAwait(false);
        }

        #endregion

        #region Tag Operations

        /// <summary>
        /// Sets a tag on a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task SetTagAsync(string documentId, string key, string? value, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(key);

            string tagId = IdGenerator.GenerateTagId();
            await _Driver.Tags.SetAsync(_TablePrefix, tagId, documentId, key, value, token).ConfigureAwait(false);

            // Invalidate document cache
            _CacheInvalidator?.OnDocumentUpdated(documentId);
        }

        /// <summary>
        /// Adds multiple tags to a document in a single batch operation.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="tags">Dictionary of tag keys and values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task AddTagsBatchAsync(string documentId, Dictionary<string, string> tags, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(tags);

            if (tags.Count == 0)
            {
                return;
            }

            List<TagRecord> records = tags.Select(kvp => new TagRecord
            {
                Id = IdGenerator.GenerateTagId(),
                DocumentId = documentId,
                Key = kvp.Key,
                Value = kvp.Value
            }).ToList();

            await _Driver.Tags.AddBatchAsync(_TablePrefix, records, token).ConfigureAwait(false);

            // Invalidate document cache
            _CacheInvalidator?.OnDocumentUpdated(documentId);
        }

        /// <summary>
        /// Replaces all tags on a document with the provided dictionary (deletes all existing, then adds new).
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="tags">Dictionary of tags to set.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ReplaceTagsAsync(string documentId, Dictionary<string, string> tags, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(tags);

            await _Driver.Tags.ReplaceAsync(_TablePrefix, documentId, tags, token).ConfigureAwait(false);

            // Invalidate document cache
            _CacheInvalidator?.OnDocumentUpdated(documentId);
        }

        /// <summary>
        /// Sets an index-level tag.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task SetIndexTagAsync(string key, string? value, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key);

            string tagId = IdGenerator.GenerateTagId();
            await _Driver.Tags.SetAsync(_TablePrefix, tagId, null, key, value, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a tag value from a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">Tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tag value or null.</returns>
        public async Task<string?> GetTagAsync(string documentId, string key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(key);

            return await _Driver.Tags.GetAsync(_TablePrefix, documentId, key, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all tags for a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of tags.</returns>
        public async Task<Dictionary<string, string>> GetTagsAsync(string documentId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            return await _Driver.Tags.GetByDocumentAsync(_TablePrefix, documentId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets index-level tags.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of tags.</returns>
        public async Task<Dictionary<string, string>> GetIndexTagsAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            return await _Driver.Tags.GetIndexTagsAsync(_TablePrefix, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets document IDs with a specific tag.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value to match.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs.</returns>
        public async Task<List<string>> GetDocumentIdsByTagAsync(string key, string value, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);

            return await _Driver.Tags.GetDocumentsByTagAsync(_TablePrefix, key, value, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes a tag from a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">Tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if tag was removed.</returns>
        public async Task<bool> RemoveTagAsync(string documentId, string key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);
            ArgumentNullException.ThrowIfNull(key);

            bool removed = await _Driver.Tags.RemoveAsync(_TablePrefix, documentId, key, token).ConfigureAwait(false);

            // Invalidate document cache
            if (removed)
            {
                _CacheInvalidator?.OnDocumentUpdated(documentId);
            }

            return removed;
        }

        /// <summary>
        /// Removes an index-level tag.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if tag was removed.</returns>
        public async Task<bool> RemoveIndexTagAsync(string key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key);

            return await _Driver.Tags.RemoveIndexTagAsync(_TablePrefix, key, token).ConfigureAwait(false);
        }

        #endregion

        #region Custom-Metadata

        /// <summary>
        /// Sets or updates the custom metadata for a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="customMetadata">Custom metadata (any JSON-serializable value, or null to clear).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentId is null.</exception>
        public async Task SetCustomMetadataAsync(string documentId, object? customMetadata, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            await _Driver.Documents.UpdateCustomMetadataAsync(_TablePrefix, documentId, customMetadata, token).ConfigureAwait(false);

            // Invalidate document cache
            _CacheInvalidator?.OnDocumentUpdated(documentId);
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Gets comprehensive index statistics.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Index statistics.</returns>
        public async Task<IndexStatistics> GetStatisticsAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            // Try cache first
            if (_CacheManager != null && _CacheManager.TryGetStatistics(out IndexStatistics? cachedStats) && cachedStats != null)
            {
                // Add cache statistics to the result
                cachedStats.CacheStatistics = _CacheManager.GetCacheStatistics();
                cachedStats.TermIdCacheSize = _TermIdCache?.Count ?? 0;
                cachedStats.TermIdCacheLoaded = _TermIdCache?.IsLoaded ?? false;
                return cachedStats;
            }

            IndexStatistics stats = await _Driver.Statistics.GetIndexStatisticsAsync(_TablePrefix, token).ConfigureAwait(false);

            // Cache the result
            _CacheManager?.SetStatistics(stats);

            // Add cache statistics to the result
            if (_CacheManager != null)
            {
                stats.CacheStatistics = _CacheManager.GetCacheStatistics();
            }

            // Add term ID cache statistics
            stats.TermIdCacheSize = _TermIdCache?.Count ?? 0;
            stats.TermIdCacheLoaded = _TermIdCache?.IsLoaded ?? false;

            return stats;
        }

        /// <summary>
        /// Gets statistics for a specific term.
        /// </summary>
        /// <param name="term">Term to get statistics for.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term statistics or null if term not found.</returns>
        public async Task<TermStatisticsResult?> GetTermStatisticsAsync(string term, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(term);

            string normalizedTerm = NormalizeTerm(term);
            if (string.IsNullOrEmpty(normalizedTerm))
            {
                return null;
            }

            return await _Driver.Statistics.GetTermStatisticsAsync(_TablePrefix, normalizedTerm, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the unique term count in the index.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term count.</returns>
        public async Task<long> GetTermCountAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            return await _Driver.Terms.GetCountAsync(_TablePrefix, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the top terms by document frequency.
        /// </summary>
        /// <param name="limit">Maximum number of terms.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of top terms.</returns>
        public async Task<List<TermRecord>> GetTopTermsAsync(int limit = 100, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            return await _Driver.Terms.GetTopAsync(_TablePrefix, limit, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the current cache statistics for this index.
        /// Returns null if caching is not enabled.
        /// </summary>
        /// <returns>Cache statistics or null if caching is disabled.</returns>
        public VerbexCacheStatistics? GetCacheStatistics()
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            return _CacheManager?.GetCacheStatistics();
        }

        #endregion

        #region Flush Operations

        /// <summary>
        /// Flushes any pending changes to persistent storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task FlushAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await _Driver.FlushAsync(token).ConfigureAwait(false);
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Rebuilds the term ID cache by reloading all terms from the database.
        /// Use this if the database has been modified externally.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of terms loaded into cache.</returns>
        public async Task<int> RebuildTermCacheAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!_Configuration.CacheConfiguration.EnableTermIdCache)
                {
                    return 0;
                }

                Dictionary<string, string> termIds = await _Driver.Terms.GetAllTermIdsAsync(_TablePrefix, token).ConfigureAwait(false);

                if (_TermIdCache == null)
                {
                    int initialCapacity = Math.Max(termIds.Count, _Configuration.CacheConfiguration.TermIdCacheInitialCapacity);
                    _TermIdCache = new TermIdCache(_TablePrefix, initialCapacity);
                }

                _TermIdCache.Load(termIds);
                return termIds.Count;
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        /// <summary>
        /// Gets the current term ID cache size.
        /// </summary>
        /// <returns>Number of cached term IDs, or 0 if caching is disabled.</returns>
        public int GetTermCacheSize()
        {
            return _TermIdCache?.Count ?? 0;
        }

        /// <summary>
        /// Gets whether the term ID cache is loaded and ready.
        /// </summary>
        /// <returns>True if the cache is loaded.</returns>
        public bool IsTermCacheLoaded()
        {
            return _TermIdCache?.IsLoaded ?? false;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the index.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the index asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                _CacheManager?.Dispose();
                _CacheManager = null;
                _CacheInvalidator = null;

                _TermIdCache?.Dispose();
                _TermIdCache = null;

                _WriteLock.Dispose();
                if (_OwnsDriver)
                {
                    _Driver.Dispose();
                }
            }

            _IsDisposed = true;
        }

        /// <summary>
        /// Disposes managed resources asynchronously.
        /// </summary>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            // Dispose caches first
            _CacheManager?.Dispose();
            _CacheManager = null;
            _CacheInvalidator = null;

            _TermIdCache?.Dispose();
            _TermIdCache = null;

            _WriteLock.Dispose();

            if (_OwnsDriver)
            {
                // Flush WAL to main database before closing to ensure all data is written
                // This is critical for proper cleanup when the directory will be deleted
                try
                {
                    await _Driver.FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore flush errors during disposal
                }

                await _Driver.DisposeAsync().ConfigureAwait(false);
            }

            _IsDisposed = true;
        }

        #endregion

        #region Private Methods

        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(InvertedIndex));
            }
        }

        private void ThrowIfNotOpen()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Index is not open. Call OpenAsync first.");
            }
        }

        private bool ShouldAutoFlush()
        {
            // Only applicable for file-based SQLite databases
            if (_Driver.Settings.Type != DatabaseTypeEnum.Sqlite || _Driver.Settings.InMemory)
            {
                return false;
            }

            int interval = _Driver.Settings.AutoFlushInterval;
            int currentCount = Interlocked.Increment(ref _DocumentsSinceLastFlush);

            // Only the request that hits exactly the interval triggers the flush
            // This prevents multiple concurrent requests from all triggering flushes
            if (currentCount == interval)
            {
                // Atomically reset the counter - only one request will succeed
                int previousValue = Interlocked.CompareExchange(ref _DocumentsSinceLastFlush, 0, interval);
                return previousValue == interval;
            }

            return false;
        }

        private async Task PerformAutoFlushAsync(CancellationToken token)
        {
            // Perform flush - only one request at a time should reach here due to ShouldAutoFlush logic
            await _Driver.FlushAsync(token).ConfigureAwait(false);
        }

        private async Task<IndexContentResult> IndexDocumentContentAsync(string documentId, string documentPath, string content, Stopwatch totalStopwatch, CancellationToken token)
        {
            IngestionMetrics metrics = new IngestionMetrics();
            Stopwatch stepStopwatch = new Stopwatch();

            // Step 1: Tokenization
            stepStopwatch.Restart();
            List<string> tokens = TokenizeAndProcess(content);
            stepStopwatch.Stop();
            metrics.Steps.TokenizationMs = stepStopwatch.Elapsed.TotalMilliseconds;
            metrics.Counts.TotalTokens = tokens.Count;

            if (tokens.Count == 0)
            {
                totalStopwatch.Stop();
                metrics.TotalMs = totalStopwatch.Elapsed.TotalMilliseconds;
                await _Driver.Documents.UpdateAsync(
                    _TablePrefix,
                    documentId,
                    documentPath,
                    ComputeContentHash(content),
                    content.Length,
                    0,
                    null,
                    (decimal)totalStopwatch.Elapsed.TotalMilliseconds,
                    token).ConfigureAwait(false);
                return new IndexContentResult(metrics, new List<string>());
            }

            // Step 2: Position calculation
            stepStopwatch.Restart();
            Dictionary<string, TermPositionData> termPositions = new Dictionary<string, TermPositionData>();
            int absoluteOffset = 0;
            int relativePosition = 0;

            string[] words = content.Split(_Configuration.TokenizationDelimiters, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                string normalizedTerm = NormalizeTerm(word);
                if (string.IsNullOrEmpty(normalizedTerm))
                {
                    absoluteOffset += word.Length + 1;
                    continue;
                }

                if (!termPositions.ContainsKey(normalizedTerm))
                {
                    termPositions[normalizedTerm] = new TermPositionData();
                }

                int charOffset = content.IndexOf(word, absoluteOffset, StringComparison.Ordinal);
                if (charOffset >= 0)
                {
                    absoluteOffset = charOffset;
                }

                termPositions[normalizedTerm].CharacterPositions.Add(absoluteOffset);
                termPositions[normalizedTerm].TermPositions.Add(relativePosition);

                absoluteOffset += word.Length + 1;
                relativePosition++;
            }
            stepStopwatch.Stop();
            metrics.Steps.PositionCalculationMs = stepStopwatch.Elapsed.TotalMilliseconds;

            int distinctTermCount = termPositions.Count;
            metrics.Counts.UniqueTerms = distinctTermCount;

            // Wrap all database operations in a single transaction for performance
            await _Driver.BeginTransactionAsync(token).ConfigureAwait(false);
            try
            {
                // Step 3: Batch add/get all terms (with cache optimization)
                stepStopwatch.Restart();
                List<string> termList = new List<string>(termPositions.Keys);
                Dictionary<string, string> termIds = new Dictionary<string, string>();
                List<string> uncachedTerms;
                int cacheHits = 0;
                int cacheMisses = 0;

                // Check cache first if available
                if (_TermIdCache != null && _TermIdCache.IsLoaded)
                {
                    (Dictionary<string, string> cachedTermIds, List<string> notCached) = _TermIdCache.TryGetIds(termList);
                    foreach (KeyValuePair<string, string> kvp in cachedTermIds)
                    {
                        termIds[kvp.Key] = kvp.Value;
                    }
                    uncachedTerms = notCached;
                    cacheHits = cachedTermIds.Count;
                    cacheMisses = notCached.Count;
                }
                else
                {
                    uncachedTerms = termList;
                    cacheMisses = termList.Count;
                }

                // Query database only for uncached terms
                int newTermsCount = 0;
                if (uncachedTerms.Count > 0)
                {
                    Dictionary<string, string> termIdsToGenerate = new Dictionary<string, string>();
                    foreach (string term in uncachedTerms)
                    {
                        termIdsToGenerate[IdGenerator.GenerateTermId()] = term;
                    }
                    Dictionary<string, string> dbTermIds = await _Driver.Terms.AddOrGetBatchAsync(_TablePrefix, termIdsToGenerate, token).ConfigureAwait(false);

                    // Calculate new terms added (terms where we generated the ID that got used)
                    foreach (KeyValuePair<string, string> kvp in termIdsToGenerate)
                    {
                        string generatedId = kvp.Key;
                        string term = kvp.Value;
                        if (dbTermIds.TryGetValue(term, out string? actualId) && actualId == generatedId)
                        {
                            newTermsCount++;
                        }
                    }

                    // Write-through: update cache with results from DB
                    _TermIdCache?.SetRange(dbTermIds);

                    // Merge DB results into termIds
                    foreach (KeyValuePair<string, string> kvp in dbTermIds)
                    {
                        termIds[kvp.Key] = kvp.Value;
                    }
                }
                stepStopwatch.Stop();
                metrics.Steps.TermLookupMs = stepStopwatch.Elapsed.TotalMilliseconds;
                metrics.Counts.NewTerms = newTermsCount;
                metrics.Counts.TermCacheHits = cacheHits;
                metrics.Counts.TermCacheMisses = cacheMisses;

                // Step 4: Prepare document-term mappings for batch insert
                List<DocumentTermRecord> docTermRecords = new List<DocumentTermRecord>();
                Dictionary<string, FrequencyDelta> frequencyUpdates =
                    new Dictionary<string, FrequencyDelta>();

                foreach (KeyValuePair<string, TermPositionData> kvp in termPositions)
                {
                    string term = kvp.Key;
                    List<int> characterPositions = kvp.Value.CharacterPositions;
                    List<int> termPositionsList = kvp.Value.TermPositions;
                    int termFrequency = characterPositions.Count;

                    if (termIds.TryGetValue(term, out string? termId))
                    {
                        docTermRecords.Add(new DocumentTermRecord
                        {
                            Id = IdGenerator.GenerateDocumentTermId(),
                            DocumentId = documentId,
                            TermId = termId,
                            TermFrequency = termFrequency,
                            CharacterPositions = characterPositions,
                            TermPositions = termPositionsList
                        });
                        frequencyUpdates[termId] = new FrequencyDelta(1, termFrequency);
                    }
                }

                // Step 5: Batch insert document-term mappings (single INSERT)
                stepStopwatch.Restart();
                await _Driver.DocumentTerms.AddBatchAsync(_TablePrefix, docTermRecords, token).ConfigureAwait(false);
                stepStopwatch.Stop();
                metrics.Steps.DocumentTermInsertMs = stepStopwatch.Elapsed.TotalMilliseconds;

                // Step 6: Batch update term frequencies (single UPDATE)
                stepStopwatch.Restart();
                await _Driver.Terms.IncrementFrequenciesBatchAsync(_TablePrefix, frequencyUpdates, token).ConfigureAwait(false);
                stepStopwatch.Stop();
                metrics.Steps.FrequencyUpdateMs = stepStopwatch.Elapsed.TotalMilliseconds;

                // Step 7: Update document metadata
                stepStopwatch.Restart();
                totalStopwatch.Stop();
                await _Driver.Documents.UpdateAsync(
                    _TablePrefix,
                    documentId,
                    documentPath,
                    ComputeContentHash(content),
                    content.Length,
                    distinctTermCount,
                    null,
                    (decimal)totalStopwatch.Elapsed.TotalMilliseconds,
                    token).ConfigureAwait(false);
                stepStopwatch.Stop();
                metrics.Steps.DocumentUpdateMs = stepStopwatch.Elapsed.TotalMilliseconds;

                // Step 8: Commit transaction
                stepStopwatch.Restart();
                await _Driver.CommitTransactionAsync(token).ConfigureAwait(false);
                stepStopwatch.Stop();
                metrics.Steps.TransactionCommitMs = stepStopwatch.Elapsed.TotalMilliseconds;

                metrics.TotalMs = totalStopwatch.Elapsed.TotalMilliseconds;
                return new IndexContentResult(metrics, termList);
            }
            catch
            {
                await _Driver.RollbackTransactionAsync(token).ConfigureAwait(false);
                throw;
            }
        }

        private List<string> TokenizeAndProcess(string content)
        {
            IEnumerable<string> tokens = _Tokenizer.Tokenize(content);

            List<string> result = new List<string>();
            foreach (string token in tokens)
            {
                string? processed = ProcessToken(token);
                if (!string.IsNullOrEmpty(processed))
                {
                    result.Add(processed);
                }
            }

            return result;
        }

        private string? ProcessToken(string token)
        {
            if (_Configuration.StopWordRemover != null && _Configuration.StopWordRemover.IsStopWord(token))
            {
                return null;
            }

            string processed = token.ToLowerInvariant();

            if (_Configuration.Lemmatizer != null)
            {
                processed = _Configuration.Lemmatizer.Lemmatize(processed);
            }

            if (_Configuration.MinTokenLength > 0 && processed.Length < _Configuration.MinTokenLength)
            {
                return null;
            }

            if (_Configuration.MaxTokenLength > 0 && processed.Length > _Configuration.MaxTokenLength)
            {
                return null;
            }

            return processed;
        }

        private string NormalizeTerm(string term)
        {
            string? processed = ProcessToken(term.Trim());
            return processed ?? string.Empty;
        }

        private static string ComputeContentHash(string content)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash);
        }

        private double CalculateScore(SearchMatch match, Dictionary<string, TermRecord> termRecords, long totalDocs)
        {
            double score = 0.0;

            // Calculate TF-IDF for each term that this document actually contains
            // Formula: score = Σ log(1 + TF) × log((N + 1) / (df + 1))
            foreach (TermRecord term in termRecords.Values)
            {
                // Only include terms that this document actually matches
                if (!match.TermFrequencies.TryGetValue(term.Id, out int termFrequency))
                {
                    continue;
                }

                if (termFrequency > 0 && totalDocs > 0)
                {
                    // TF component: logarithmic term frequency
                    double tf = Math.Log(1.0 + termFrequency);

                    // IDF component: inverse document frequency with smoothing
                    // Smoothing prevents division by zero and ensures non-zero IDF
                    double idf = Math.Log((totalDocs + 1.0) / (term.DocumentFrequency + 1.0));

                    // Accumulate TF-IDF for this term
                    score += tf * idf;
                }
            }

            // Apply sigmoid normalization to map score to 0-1 range
            score = 1.0 / (1.0 + Math.Exp(-score / _Configuration.SigmoidNormalizationDivisor));

            return score;
        }

        #endregion
    }
}
