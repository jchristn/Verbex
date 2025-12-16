namespace Verbex
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Repositories;
    using Verbex.Utilities;

    /// <summary>
    /// Main inverted index implementation using SQLite storage.
    /// Thread-safe for concurrent operations.
    /// </summary>
    public class InvertedIndex : IDisposable, IAsyncDisposable
    {
        private readonly VerbexConfiguration _Configuration;
        private readonly IIndexRepository _Repository;
        private readonly ITokenizer _Tokenizer;
        private readonly string _IndexName;
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
        public bool IsOpen => _Repository.IsOpen;

        /// <summary>
        /// Initializes a new instance of the InvertedIndex class with configuration object.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="configuration">Configuration settings for the index.</param>
        /// <exception cref="ArgumentNullException">Thrown when indexName or configuration is null.</exception>
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

            if (_Configuration.StorageMode == StorageMode.InMemory)
            {
                _Repository = new MemoryIndexRepository();
            }
            else
            {
                string directory = _Configuration.StorageDirectory ?? VerbexConfiguration.GetDefaultStorageDirectory(indexName);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                _Repository = DiskIndexRepository.Create(directory, _Configuration.DatabaseFilename);
            }
        }

        /// <summary>
        /// Initializes a new instance of the InvertedIndex class with a custom repository.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="repository">Repository to use for storage.</param>
        /// <param name="configuration">Configuration settings for the index.</param>
        /// <exception cref="ArgumentNullException">Thrown when indexName or repository is null.</exception>
        public InvertedIndex(string indexName, IIndexRepository repository, VerbexConfiguration? configuration = null)
        {
            ArgumentNullException.ThrowIfNull(indexName);
            ArgumentNullException.ThrowIfNull(repository);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty or whitespace.", nameof(indexName));
            }

            _IndexName = indexName;
            _Repository = repository;
            _Configuration = configuration?.Clone() ?? new VerbexConfiguration();
            _Tokenizer = _Configuration.Tokenizer ?? new DefaultTokenizer();
        }

        /// <summary>
        /// Opens the index. Must be called before any operations.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task OpenAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (!_Repository.IsOpen)
            {
                await _Repository.OpenAsync(_IndexName, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Closes the index.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task CloseAsync(CancellationToken token = default)
        {
            if (_Repository.IsOpen)
            {
                await _Repository.CloseAsync(token).ConfigureAwait(false);
            }
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

            return await _Repository.Document.GetCountAsync(token).ConfigureAwait(false);
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

            string documentId = IdGenerator.GenerateDocumentId();
            string contentSha256 = ComputeContentHash(content);

            await _Repository.Document.AddAsync(documentId, documentName, contentSha256, content.Length, token).ConfigureAwait(false);

            await IndexDocumentContentAsync(documentId, documentName, content, token).ConfigureAwait(false);

            return documentId;
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

            string contentSha256 = ComputeContentHash(content);

            await _Repository.Document.AddAsync(documentId, documentName, contentSha256, content.Length, token).ConfigureAwait(false);

            await IndexDocumentContentAsync(documentId, documentName, content, token).ConfigureAwait(false);
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

            return await _Repository.Document.GetAsync(documentId, token).ConfigureAwait(false);
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

            return await _Repository.Document.GetWithMetadataAsync(documentId, token).ConfigureAwait(false);
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

            return await _Repository.Document.GetByNameAsync(documentName, token).ConfigureAwait(false);
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

            return await _Repository.Document.GetAllAsync(limit, offset, token).ConfigureAwait(false);
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

            return await _Repository.Document.ExistsAsync(documentId, token).ConfigureAwait(false);
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

            return await _Repository.Document.ExistsByNameAsync(documentName, token).ConfigureAwait(false);
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

            List<DocumentTermRecord> docTerms = await _Repository.DocumentTerm.GetByDocumentAsync(documentId, token).ConfigureAwait(false);

            // Batch decrement term frequencies (single UPDATE instead of N)
            if (docTerms.Count > 0)
            {
                Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> decrements =
                    new Dictionary<string, (int, int)>();
                foreach (DocumentTermRecord docTerm in docTerms)
                {
                    decrements[docTerm.TermId] = (1, docTerm.TermFrequency);
                }
                await _Repository.Term.DecrementFrequenciesBatchAsync(decrements, token).ConfigureAwait(false);
            }

            await _Repository.DocumentTerm.DeleteByDocumentAsync(documentId, token).ConfigureAwait(false);

            bool deleted = await _Repository.Document.DeleteAsync(documentId, token).ConfigureAwait(false);

            await _Repository.Term.DeleteOrphanedAsync(token).ConfigureAwait(false);

            return deleted;
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

            long documentCount = await _Repository.Document.DeleteAllAsync(token).ConfigureAwait(false);

            await _Repository.Term.DeleteAllAsync(token).ConfigureAwait(false);

            return documentCount;
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

            Dictionary<string, TermRecord> termRecords = await _Repository.Term.GetMultipleAsync(queryTerms, token).ConfigureAwait(false);

            if (termRecords.Count == 0)
            {
                return new SearchResults(new List<SearchResult>(), 0, DateTime.UtcNow - startTime);
            }

            List<string> termIds = termRecords.Values.Select(t => t.Id).ToList();

            int limit = maxResults ?? _Configuration.DefaultMaxSearchResults;
            List<SearchMatch> matches = await _Repository.DocumentTerm.SearchAsync(termIds, useAndLogic, labels, tags, limit, token).ConfigureAwait(false);

            List<string> docIds = matches.Select(m => m.DocumentId).ToList();
            List<DocumentMetadata> documents = await _Repository.Document.GetByIdsAsync(docIds, token).ConfigureAwait(false);

            Dictionary<string, DocumentMetadata> docLookup = documents.ToDictionary(d => d.DocumentId);

            long totalDocs = await _Repository.Document.GetCountAsync(token).ConfigureAwait(false);

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

            return await _Repository.Term.ExistsAsync(normalizedTerm, token).ConfigureAwait(false);
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

            return await _Repository.DocumentTerm.GetPostingsByTermAsync(normalizedTerm, token).ConfigureAwait(false);
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

            return await _Repository.Term.GetByPrefixAsync(prefix.ToLowerInvariant(), limit, token).ConfigureAwait(false);
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

            return await _Repository.DocumentTerm.GetByDocumentAsync(documentId, token).ConfigureAwait(false);
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

            await _Repository.Label.AddAsync(documentId, label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            List<string> normalizedLabels = labels.Select(l => l.ToLowerInvariant()).ToList();
            await _Repository.Label.AddBatchAsync(documentId, normalizedLabels, token).ConfigureAwait(false);
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
            await _Repository.Label.ReplaceAsync(documentId, normalizedLabels, token).ConfigureAwait(false);
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

            await _Repository.Label.AddAsync(null, label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            return await _Repository.Label.GetByDocumentAsync(documentId, token).ConfigureAwait(false);
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

            return await _Repository.Label.GetIndexLabelsAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets documents with a specific label.
        /// </summary>
        /// <param name="label">Label to filter by.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        public async Task<List<DocumentMetadata>> GetDocumentsByLabelAsync(string label, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(label);

            return await _Repository.Label.GetDocumentsByLabelAsync(label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            return await _Repository.Label.RemoveAsync(documentId, label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            return await _Repository.Label.RemoveAsync(null, label.ToLowerInvariant(), token).ConfigureAwait(false);
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

            await _Repository.Tag.SetAsync(documentId, key, value, token).ConfigureAwait(false);
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

            await _Repository.Tag.AddBatchAsync(documentId, tags, token).ConfigureAwait(false);
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

            await _Repository.Tag.ReplaceAsync(documentId, tags, token).ConfigureAwait(false);
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

            await _Repository.Tag.SetAsync(null, key, value, token).ConfigureAwait(false);
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

            return await _Repository.Tag.GetAsync(documentId, key, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all tags for a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of tags.</returns>
        public async Task<Dictionary<string, string?>> GetTagsAsync(string documentId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId);

            return await _Repository.Tag.GetByDocumentAsync(documentId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets index-level tags.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of tags.</returns>
        public async Task<Dictionary<string, string?>> GetIndexTagsAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            return await _Repository.Tag.GetIndexTagsAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets documents with a specific tag.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value to match.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        public async Task<List<DocumentMetadata>> GetDocumentsByTagAsync(string key, string value, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);

            return await _Repository.Tag.GetDocumentsByTagAsync(key, value, token).ConfigureAwait(false);
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

            return await _Repository.Tag.RemoveAsync(documentId, key, token).ConfigureAwait(false);
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

            return await _Repository.Tag.RemoveAsync(null, key, token).ConfigureAwait(false);
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

            return await _Repository.Statistics.GetIndexStatisticsAsync(token).ConfigureAwait(false);
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

            return await _Repository.Statistics.GetTermStatisticsAsync(normalizedTerm, token).ConfigureAwait(false);
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

            return await _Repository.Term.GetCountAsync(token).ConfigureAwait(false);
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

            return await _Repository.Term.GetTopAsync(limit, token).ConfigureAwait(false);
        }

        #endregion

        #region Flush Operations

        /// <summary>
        /// Flushes any pending changes. For in-memory mode, saves to the specified path.
        /// </summary>
        /// <param name="targetPath">Path to save to (for in-memory mode only).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task FlushAsync(string? targetPath = null, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await _Repository.FlushAsync(targetPath, token).ConfigureAwait(false);
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
                _Repository.Dispose();
            }

            _IsDisposed = true;
        }

        /// <summary>
        /// Disposes managed resources asynchronously.
        /// </summary>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            await _Repository.DisposeAsync().ConfigureAwait(false);
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
            if (!_Repository.IsOpen)
            {
                throw new InvalidOperationException("Index is not open. Call OpenAsync first.");
            }
        }

        private async Task IndexDocumentContentAsync(string documentId, string documentPath, string content, CancellationToken token)
        {
            List<string> tokens = TokenizeAndProcess(content);
            if (tokens.Count == 0)
            {
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

            // Step 1: Batch add/get all terms (single INSERT + single SELECT)
            List<string> termList = new List<string>(termPositions.Keys);
            Dictionary<string, string> termIds = await _Repository.Term.AddOrGetBatchAsync(termList, token).ConfigureAwait(false);

            // Step 2: Prepare document-term mappings for batch insert
            List<(string TermId, int TermFrequency, List<int> CharacterPositions, List<int> TermPositions)> docTermMappings =
                new List<(string, int, List<int>, List<int>)>();
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
                    docTermMappings.Add((termId, termFrequency, characterPositions, termPositionsList));
                    frequencyUpdates[termId] = (1, termFrequency);
                }
            }

            // Step 3: Batch insert document-term mappings (single INSERT)
            await _Repository.DocumentTerm.AddBatchAsync(documentId, docTermMappings, token).ConfigureAwait(false);

            // Step 4: Batch update term frequencies (single UPDATE)
            await _Repository.Term.IncrementFrequenciesBatchAsync(frequencyUpdates, token).ConfigureAwait(false);

            // Step 5: Update document metadata (documentPath passed in to avoid extra query)
            await _Repository.Document.UpdateAsync(
                documentId,
                documentPath,
                ComputeContentHash(content),
                content.Length,
                distinctTermCount,
                token).ConfigureAwait(false);
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
            double score = 0;

            foreach (TermRecord term in termRecords.Values)
            {
                if (term.DocumentFrequency > 0 && totalDocs > 0)
                {
                    double idf = Math.Log((double)totalDocs / term.DocumentFrequency);
                    score += idf;
                }
            }

            score *= match.TotalFrequency;

            score = 1.0 / (1.0 + Math.Exp(-score / _Configuration.SigmoidNormalizationDivisor));

            return score;
        }

        #endregion
    }
}
