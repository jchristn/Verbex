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
        private bool _IsDisposed;

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
            }
            _IndexId = index.Identifier;
        }

        /// <summary>
        /// Closes the index.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task CloseAsync(CancellationToken token = default)
        {
            if (_Driver.IsOpen && _OwnsDriver)
            {
                await _Driver.CloseAsync(token).ConfigureAwait(false);
            }
            _TenantId = string.Empty;
            _IndexId = string.Empty;
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

            return await _Driver.Documents.GetCountAsync(_TenantId, _IndexId, token).ConfigureAwait(false);
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
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentName);
            ArgumentNullException.ThrowIfNull(content);

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                string documentId = IdGenerator.GenerateDocumentId();
                string contentSha256 = ComputeContentHash(content);

                await _Driver.Documents.AddAsync(_TenantId, _IndexId, documentId, documentName, contentSha256, content.Length, null, null, token).ConfigureAwait(false);

                await IndexDocumentContentAsync(documentId, documentName, content, stopwatch, token).ConfigureAwait(false);

                return documentId;
            }
            finally
            {
                _WriteLock.Release();
            }
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
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));
            }

            ArgumentNullException.ThrowIfNull(documentName);
            ArgumentNullException.ThrowIfNull(content);

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                string contentSha256 = ComputeContentHash(content);

                await _Driver.Documents.AddAsync(_TenantId, _IndexId, documentId, documentName, contentSha256, content.Length, null, null, token).ConfigureAwait(false);

                await IndexDocumentContentAsync(documentId, documentName, content, stopwatch, token).ConfigureAwait(false);
            }
            finally
            {
                _WriteLock.Release();
            }
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

            return await _Driver.Documents.GetAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);
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

            return await _Driver.Documents.GetWithMetadataAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);
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

            return await _Driver.Documents.GetByNameAsync(_TenantId, _IndexId, documentName, token).ConfigureAwait(false);
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

            return await _Driver.Documents.GetAllAsync(_TenantId, _IndexId, limit, offset, token).ConfigureAwait(false);
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

            return await _Driver.Documents.ExistsAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);
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

            return await _Driver.Documents.ExistsByNameAsync(_TenantId, _IndexId, documentName, token).ConfigureAwait(false);
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
                List<DocumentTermRecord> docTerms = await _Driver.DocumentTerms.GetByDocumentAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);

                // Batch decrement term frequencies (single UPDATE instead of N)
                if (docTerms.Count > 0)
                {
                    Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> decrements =
                        new Dictionary<string, (int, int)>();
                    foreach (DocumentTermRecord docTerm in docTerms)
                    {
                        decrements[docTerm.TermId] = (1, docTerm.TermFrequency);
                    }
                    await _Driver.Terms.DecrementFrequenciesBatchAsync(_TenantId, _IndexId, decrements, token).ConfigureAwait(false);
                }

                await _Driver.DocumentTerms.DeleteByDocumentAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);

                bool deleted = await _Driver.Documents.DeleteAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);

                await _Driver.Terms.DeleteOrphanedAsync(_TenantId, _IndexId, token).ConfigureAwait(false);

                return deleted;
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
        /// <returns>A tuple containing the list of deleted IDs and the list of not-found IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when documentIds is null.</exception>
        public async Task<(List<string> Deleted, List<string> NotFound)> RemoveDocumentsBatchAsync(IEnumerable<string> documentIds, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentIds);

            List<string> requestedIds = documentIds.Distinct().Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (requestedIds.Count == 0)
            {
                return (new List<string>(), new List<string>());
            }

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                // Step 1: Get all document-terms for all requested documents in a single query
                List<DocumentTermRecord> allDocTerms = await _Driver.DocumentTerms.GetByDocumentsAsync(_TenantId, _IndexId, requestedIds, token).ConfigureAwait(false);

                // Step 2: Aggregate term frequency decrements across all documents
                if (allDocTerms.Count > 0)
                {
                    Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> decrements = new Dictionary<string, (int, int)>();
                    foreach (DocumentTermRecord docTerm in allDocTerms)
                    {
                        if (decrements.TryGetValue(docTerm.TermId, out (int DocFreqDelta, int TotalFreqDelta) existing))
                        {
                            decrements[docTerm.TermId] = (existing.DocFreqDelta + 1, existing.TotalFreqDelta + docTerm.TermFrequency);
                        }
                        else
                        {
                            decrements[docTerm.TermId] = (1, docTerm.TermFrequency);
                        }
                    }

                    // Step 3: Single batch decrement for all term frequencies
                    await _Driver.Terms.DecrementFrequenciesBatchAsync(_TenantId, _IndexId, decrements, token).ConfigureAwait(false);
                }

                // Step 4: Delete all document-terms in one statement
                await _Driver.DocumentTerms.DeleteByDocumentsAsync(_TenantId, _IndexId, requestedIds, token).ConfigureAwait(false);

                // Step 5: Delete all documents in one statement and get back which ones existed
                List<string> deletedIds = await _Driver.Documents.DeleteBatchAsync(_TenantId, _IndexId, requestedIds, token).ConfigureAwait(false);

                // Step 6: Run orphan cleanup once at the end
                await _Driver.Terms.DeleteOrphanedAsync(_TenantId, _IndexId, token).ConfigureAwait(false);

                // Step 7: Calculate not-found IDs
                HashSet<string> deletedSet = new HashSet<string>(deletedIds);
                List<string> notFoundIds = requestedIds.Where(id => !deletedSet.Contains(id)).ToList();

                return (deletedIds, notFoundIds);
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
                long documentCount = await _Driver.Documents.DeleteAllAsync(_TenantId, _IndexId, token).ConfigureAwait(false);

                await _Driver.Terms.DeleteAllAsync(_TenantId, _IndexId, token).ConfigureAwait(false);

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

            Dictionary<string, TermRecord> termRecords = await _Driver.Terms.GetMultipleAsync(_TenantId, _IndexId, queryTerms, token).ConfigureAwait(false);

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
            List<SearchMatch> matches = await _Driver.DocumentTerms.SearchAsync(_TenantId, _IndexId, termIds, useAndLogic, limit, labels, tags, token).ConfigureAwait(false);

            if (matches.Count == 0)
            {
                return new SearchResults(new List<SearchResult>(), 0, DateTime.UtcNow - startTime);
            }

            List<string> docIds = matches.Select(m => m.DocumentId).ToList();

            // Fetch per-term frequencies for all matched documents
            List<DocumentTermRecord> documentTermRecords = await _Driver.DocumentTerms.GetByDocumentsAndTermsAsync(_TenantId, _IndexId, docIds, termIds, token).ConfigureAwait(false);

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

            List<DocumentMetadata> documents = await _Driver.Documents.GetByIdsAsync(_TenantId, _IndexId, docIds, token).ConfigureAwait(false);

            Dictionary<string, DocumentMetadata> docLookup = documents.ToDictionary(d => d.DocumentId);

            long totalDocs = await _Driver.Documents.GetCountAsync(_TenantId, _IndexId, token).ConfigureAwait(false);

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

            return await _Driver.Terms.ExistsAsync(_TenantId, _IndexId, normalizedTerm, token).ConfigureAwait(false);
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

            TermRecord? termRecord = await _Driver.Terms.GetAsync(_TenantId, _IndexId, normalizedTerm, token).ConfigureAwait(false);
            if (termRecord == null)
            {
                return new List<DocumentTermRecord>();
            }

            return await _Driver.DocumentTerms.GetPostingsByTermAsync(_TenantId, _IndexId, termRecord.Id, token).ConfigureAwait(false);
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

            return await _Driver.Terms.GetByPrefixAsync(_TenantId, _IndexId, prefix.ToLowerInvariant(), limit, token).ConfigureAwait(false);
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

            return await _Driver.DocumentTerms.GetByDocumentAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);
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
            await _Driver.Labels.AddAsync(_TenantId, _IndexId, labelId, documentId, label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            await _Driver.Labels.AddBatchAsync(_TenantId, _IndexId, records, token).ConfigureAwait(false);
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
            await _Driver.Labels.ReplaceAsync(_TenantId, _IndexId, documentId, normalizedLabels, token).ConfigureAwait(false);
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
            await _Driver.Labels.AddAsync(_TenantId, _IndexId, labelId, null, label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            return await _Driver.Labels.GetByDocumentAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);
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

            return await _Driver.Labels.GetIndexLabelsAsync(_TenantId, _IndexId, token).ConfigureAwait(false);
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

            return await _Driver.Labels.GetDocumentsByLabelAsync(_TenantId, _IndexId, label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            return await _Driver.Labels.RemoveAsync(_TenantId, _IndexId, documentId, label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            return await _Driver.Labels.RemoveIndexLabelAsync(_TenantId, _IndexId, label.ToLowerInvariant(), token).ConfigureAwait(false);
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
            await _Driver.Tags.SetAsync(_TenantId, _IndexId, tagId, documentId, key, value, token).ConfigureAwait(false);
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

            await _Driver.Tags.AddBatchAsync(_TenantId, _IndexId, records, token).ConfigureAwait(false);
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

            await _Driver.Tags.ReplaceAsync(_TenantId, _IndexId, documentId, tags, token).ConfigureAwait(false);
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
            await _Driver.Tags.SetAsync(_TenantId, _IndexId, tagId, null, key, value, token).ConfigureAwait(false);
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

            return await _Driver.Tags.GetAsync(_TenantId, _IndexId, documentId, key, token).ConfigureAwait(false);
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

            return await _Driver.Tags.GetByDocumentAsync(_TenantId, _IndexId, documentId, token).ConfigureAwait(false);
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

            return await _Driver.Tags.GetIndexTagsAsync(_TenantId, _IndexId, token).ConfigureAwait(false);
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

            return await _Driver.Tags.GetDocumentsByTagAsync(_TenantId, _IndexId, key, value, token).ConfigureAwait(false);
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

            return await _Driver.Tags.RemoveAsync(_TenantId, _IndexId, documentId, key, token).ConfigureAwait(false);
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

            return await _Driver.Tags.RemoveIndexTagAsync(_TenantId, _IndexId, key, token).ConfigureAwait(false);
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

            await _Driver.Documents.UpdateCustomMetadataAsync(_TenantId, _IndexId, documentId, customMetadata, token).ConfigureAwait(false);
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

            return await _Driver.Statistics.GetIndexStatisticsAsync(_TenantId, _IndexId, token).ConfigureAwait(false);
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

            return await _Driver.Statistics.GetTermStatisticsAsync(_TenantId, _IndexId, normalizedTerm, token).ConfigureAwait(false);
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

            return await _Driver.Terms.GetCountAsync(_TenantId, _IndexId, token).ConfigureAwait(false);
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

            return await _Driver.Terms.GetTopAsync(_TenantId, _IndexId, limit, token).ConfigureAwait(false);
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
            if (_OwnsDriver)
            {
                await _Driver.DisposeAsync().ConfigureAwait(false);
            }
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

        private async Task IndexDocumentContentAsync(string documentId, string documentPath, string content, Stopwatch stopwatch, CancellationToken token)
        {
            List<string> tokens = TokenizeAndProcess(content);
            if (tokens.Count == 0)
            {
                stopwatch.Stop();
                await _Driver.Documents.UpdateAsync(
                    _TenantId,
                    _IndexId,
                    documentId,
                    documentPath,
                    ComputeContentHash(content),
                    content.Length,
                    0,
                    null,
                    (decimal)stopwatch.Elapsed.TotalMilliseconds,
                    token).ConfigureAwait(false);
                return;
            }

            Dictionary<string, (List<int> CharacterPositions, List<int> TermPositions)> termPositions = new Dictionary<string, (List<int>, List<int>)>();
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
                    termPositions[normalizedTerm] = (new List<int>(), new List<int>());
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

            int distinctTermCount = termPositions.Count;

            // Wrap all database operations in a single transaction for performance
            await _Driver.BeginTransactionAsync(token).ConfigureAwait(false);
            try
            {
                // Step 1: Batch add/get all terms (single INSERT + single SELECT)
                List<string> termList = new List<string>(termPositions.Keys);
                Dictionary<string, string> termIdsToGenerate = new Dictionary<string, string>();
                foreach (string term in termList)
                {
                    termIdsToGenerate[IdGenerator.GenerateTermId()] = term;
                }
                Dictionary<string, string> termIds = await _Driver.Terms.AddOrGetBatchAsync(_TenantId, _IndexId, termIdsToGenerate, token).ConfigureAwait(false);

                // Step 2: Prepare document-term mappings for batch insert
                List<DocumentTermRecord> docTermRecords = new List<DocumentTermRecord>();
                Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> frequencyUpdates =
                    new Dictionary<string, (int, int)>();

                foreach (KeyValuePair<string, (List<int> CharacterPositions, List<int> TermPositions)> kvp in termPositions)
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
                        frequencyUpdates[termId] = (1, termFrequency);
                    }
                }

                // Step 3: Batch insert document-term mappings (single INSERT)
                await _Driver.DocumentTerms.AddBatchAsync(_TenantId, _IndexId, docTermRecords, token).ConfigureAwait(false);

                // Step 4: Batch update term frequencies (single UPDATE)
                await _Driver.Terms.IncrementFrequenciesBatchAsync(_TenantId, _IndexId, frequencyUpdates, token).ConfigureAwait(false);

                // Step 5: Stop timing and update document metadata
                stopwatch.Stop();
                await _Driver.Documents.UpdateAsync(
                    _TenantId,
                    _IndexId,
                    documentId,
                    documentPath,
                    ComputeContentHash(content),
                    content.Length,
                    distinctTermCount,
                    null,
                    (decimal)stopwatch.Elapsed.TotalMilliseconds,
                    token).ConfigureAwait(false);

                await _Driver.CommitTransactionAsync(token).ConfigureAwait(false);
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
