namespace Verbex
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Caching;
    using Verbex.Models;

    /// <summary>
    /// Manages per-index caches for terms, documents, and statistics.
    /// Thread-safe for concurrent access.
    /// </summary>
    internal class IndexCacheManager : IDisposable
    {
        #region Private-Members

        private readonly CacheConfiguration _Configuration;
        private readonly string _IndexId;
        private LRUCache<string, TermRecord>? _TermCache;
        private LRUCache<string, DocumentMetadata>? _DocumentCache;
        private LRUCache<string, IndexStatistics>? _StatisticsCache;
        private long? _CachedDocumentCount;
        private readonly object _DocumentCountLock = new object();
        private bool _IsDisposed;

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets whether caching is enabled.
        /// </summary>
        public bool Enabled => _Configuration.Enabled;

        /// <summary>
        /// Gets whether the term cache is enabled.
        /// </summary>
        public bool TermCacheEnabled => _Configuration.Enabled && _Configuration.EnableTermCache && _TermCache != null;

        /// <summary>
        /// Gets whether the document cache is enabled.
        /// </summary>
        public bool DocumentCacheEnabled => _Configuration.Enabled && _Configuration.EnableDocumentCache && _DocumentCache != null;

        /// <summary>
        /// Gets whether the statistics cache is enabled.
        /// </summary>
        public bool StatisticsCacheEnabled => _Configuration.Enabled && _Configuration.EnableStatisticsCache && _StatisticsCache != null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new IndexCacheManager.
        /// </summary>
        /// <param name="indexId">Index identifier for cache key prefixing.</param>
        /// <param name="configuration">Cache configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when indexId or configuration is null.</exception>
        public IndexCacheManager(string indexId, CacheConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(indexId);
            ArgumentNullException.ThrowIfNull(configuration);

            _IndexId = indexId;
            _Configuration = configuration;

            if (_Configuration.Enabled)
            {
                InitializeCaches();
            }
        }

        #endregion

        #region Public-Methods-Term-Cache

        /// <summary>
        /// Try to get a term record from cache.
        /// </summary>
        /// <param name="normalizedTerm">The normalized term to look up.</param>
        /// <param name="termRecord">The cached term record if found.</param>
        /// <returns>True if found in cache.</returns>
        public bool TryGetTerm(string normalizedTerm, out TermRecord? termRecord)
        {
            termRecord = null;
            if (!TermCacheEnabled || _TermCache == null)
                return false;

            string key = BuildTermKey(normalizedTerm);
            return _TermCache.TryGet(key, out termRecord);
        }

        /// <summary>
        /// Try to get multiple term records from cache.
        /// </summary>
        /// <param name="normalizedTerms">The normalized terms to look up.</param>
        /// <returns>Dictionary of found terms (term -> record), and list of cache misses.</returns>
        public (Dictionary<string, TermRecord> Found, List<string> NotFound) TryGetTerms(IEnumerable<string> normalizedTerms)
        {
            Dictionary<string, TermRecord> found = new Dictionary<string, TermRecord>();
            List<string> notFound = new List<string>();

            if (!TermCacheEnabled || _TermCache == null)
            {
                foreach (string term in normalizedTerms)
                {
                    notFound.Add(term);
                }
                return (found, notFound);
            }

            foreach (string term in normalizedTerms)
            {
                string key = BuildTermKey(term);
                if (_TermCache.TryGet(key, out TermRecord? record) && record != null)
                {
                    found[term] = record;
                }
                else
                {
                    notFound.Add(term);
                }
            }

            return (found, notFound);
        }

        /// <summary>
        /// Add or update a term record in cache.
        /// </summary>
        /// <param name="normalizedTerm">The normalized term.</param>
        /// <param name="termRecord">The term record to cache.</param>
        public void SetTerm(string normalizedTerm, TermRecord termRecord)
        {
            if (!TermCacheEnabled || _TermCache == null)
                return;

            string key = BuildTermKey(normalizedTerm);
            DateTime? expiration = _Configuration.TermCacheTtlSeconds > 0
                ? DateTime.UtcNow.AddSeconds(_Configuration.TermCacheTtlSeconds)
                : null;

            _TermCache.SlidingExpiration = _Configuration.TermCacheSlidingExpiration;
            _TermCache.AddReplace(key, termRecord, expiration);
        }

        /// <summary>
        /// Add or update multiple term records in cache.
        /// </summary>
        /// <param name="terms">Dictionary of term -> record to cache.</param>
        public void SetTerms(Dictionary<string, TermRecord> terms)
        {
            if (!TermCacheEnabled || _TermCache == null || terms.Count == 0)
                return;

            DateTime? expiration = _Configuration.TermCacheTtlSeconds > 0
                ? DateTime.UtcNow.AddSeconds(_Configuration.TermCacheTtlSeconds)
                : null;

            _TermCache.SlidingExpiration = _Configuration.TermCacheSlidingExpiration;
            foreach (KeyValuePair<string, TermRecord> kvp in terms)
            {
                string key = BuildTermKey(kvp.Key);
                _TermCache.AddReplace(key, kvp.Value, expiration);
            }
        }

        /// <summary>
        /// Remove a term from cache.
        /// </summary>
        /// <param name="normalizedTerm">The normalized term to remove.</param>
        public void RemoveTerm(string normalizedTerm)
        {
            if (!TermCacheEnabled || _TermCache == null)
                return;

            string key = BuildTermKey(normalizedTerm);
            _TermCache.TryRemove(key, out _);
        }

        /// <summary>
        /// Remove multiple terms from cache.
        /// </summary>
        /// <param name="normalizedTerms">The normalized terms to remove.</param>
        public void RemoveTerms(IEnumerable<string> normalizedTerms)
        {
            if (!TermCacheEnabled || _TermCache == null)
                return;

            foreach (string term in normalizedTerms)
            {
                string key = BuildTermKey(term);
                _TermCache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Clear all entries from the term cache.
        /// </summary>
        public void ClearTermCache()
        {
            if (_TermCache != null)
            {
                _TermCache.Clear();
            }
        }

        #endregion

        #region Public-Methods-Document-Cache

        /// <summary>
        /// Try to get a document from cache.
        /// </summary>
        /// <param name="documentId">The document ID to look up.</param>
        /// <param name="document">The cached document if found.</param>
        /// <returns>True if found in cache.</returns>
        public bool TryGetDocument(string documentId, out DocumentMetadata? document)
        {
            document = null;
            if (!DocumentCacheEnabled || _DocumentCache == null)
                return false;

            string key = BuildDocumentKey(documentId);
            return _DocumentCache.TryGet(key, out document);
        }

        /// <summary>
        /// Try to get multiple documents from cache.
        /// </summary>
        /// <param name="documentIds">The document IDs to look up.</param>
        /// <returns>Dictionary of found documents (id -> document), and list of cache misses.</returns>
        public (Dictionary<string, DocumentMetadata> Found, List<string> NotFound) TryGetDocuments(IEnumerable<string> documentIds)
        {
            Dictionary<string, DocumentMetadata> found = new Dictionary<string, DocumentMetadata>();
            List<string> notFound = new List<string>();

            if (!DocumentCacheEnabled || _DocumentCache == null)
            {
                foreach (string docId in documentIds)
                {
                    notFound.Add(docId);
                }
                return (found, notFound);
            }

            foreach (string docId in documentIds)
            {
                string key = BuildDocumentKey(docId);
                if (_DocumentCache.TryGet(key, out DocumentMetadata? doc) && doc != null)
                {
                    found[docId] = doc;
                }
                else
                {
                    notFound.Add(docId);
                }
            }

            return (found, notFound);
        }

        /// <summary>
        /// Add or update a document in cache.
        /// </summary>
        /// <param name="documentId">The document ID.</param>
        /// <param name="document">The document to cache.</param>
        public void SetDocument(string documentId, DocumentMetadata document)
        {
            if (!DocumentCacheEnabled || _DocumentCache == null)
                return;

            string key = BuildDocumentKey(documentId);
            DateTime? expiration = _Configuration.DocumentCacheTtlSeconds > 0
                ? DateTime.UtcNow.AddSeconds(_Configuration.DocumentCacheTtlSeconds)
                : null;

            _DocumentCache.SlidingExpiration = _Configuration.DocumentCacheSlidingExpiration;
            _DocumentCache.AddReplace(key, document, expiration);
        }

        /// <summary>
        /// Add or update multiple documents in cache.
        /// </summary>
        /// <param name="documents">List of documents to cache.</param>
        public void SetDocuments(IEnumerable<DocumentMetadata> documents)
        {
            if (!DocumentCacheEnabled || _DocumentCache == null)
                return;

            DateTime? expiration = _Configuration.DocumentCacheTtlSeconds > 0
                ? DateTime.UtcNow.AddSeconds(_Configuration.DocumentCacheTtlSeconds)
                : null;

            _DocumentCache.SlidingExpiration = _Configuration.DocumentCacheSlidingExpiration;
            foreach (DocumentMetadata doc in documents)
            {
                string key = BuildDocumentKey(doc.DocumentId);
                _DocumentCache.AddReplace(key, doc, expiration);
            }
        }

        /// <summary>
        /// Remove a document from cache.
        /// </summary>
        /// <param name="documentId">The document ID to remove.</param>
        public void RemoveDocument(string documentId)
        {
            if (!DocumentCacheEnabled || _DocumentCache == null)
                return;

            string key = BuildDocumentKey(documentId);
            _DocumentCache.TryRemove(key, out _);
        }

        /// <summary>
        /// Remove multiple documents from cache.
        /// </summary>
        /// <param name="documentIds">The document IDs to remove.</param>
        public void RemoveDocuments(IEnumerable<string> documentIds)
        {
            if (!DocumentCacheEnabled || _DocumentCache == null)
                return;

            foreach (string docId in documentIds)
            {
                string key = BuildDocumentKey(docId);
                _DocumentCache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Clear all entries from the document cache.
        /// </summary>
        public void ClearDocumentCache()
        {
            if (_DocumentCache != null)
            {
                _DocumentCache.Clear();
            }
        }

        #endregion

        #region Public-Methods-Statistics-Cache

        /// <summary>
        /// Try to get index statistics from cache.
        /// </summary>
        /// <param name="statistics">The cached statistics if found.</param>
        /// <returns>True if found in cache.</returns>
        public bool TryGetStatistics(out IndexStatistics? statistics)
        {
            statistics = null;
            if (!StatisticsCacheEnabled || _StatisticsCache == null)
                return false;

            string key = BuildStatisticsKey();
            return _StatisticsCache.TryGet(key, out statistics);
        }

        /// <summary>
        /// Add or update index statistics in cache.
        /// </summary>
        /// <param name="statistics">The statistics to cache.</param>
        public void SetStatistics(IndexStatistics statistics)
        {
            if (!StatisticsCacheEnabled || _StatisticsCache == null)
                return;

            string key = BuildStatisticsKey();
            DateTime? expiration = _Configuration.StatisticsCacheTtlSeconds > 0
                ? DateTime.UtcNow.AddSeconds(_Configuration.StatisticsCacheTtlSeconds)
                : null;

            // Statistics cache does not use sliding expiration (refresh periodically)
            _StatisticsCache.SlidingExpiration = false;
            _StatisticsCache.AddReplace(key, statistics, expiration);
        }

        /// <summary>
        /// Invalidate the statistics cache.
        /// </summary>
        public void InvalidateStatistics()
        {
            if (_StatisticsCache != null)
            {
                string key = BuildStatisticsKey();
                _StatisticsCache.TryRemove(key, out _);
            }
        }

        #endregion

        #region Public-Methods-Document-Count-Cache

        /// <summary>
        /// Try to get the cached document count.
        /// </summary>
        /// <param name="count">The cached count if available.</param>
        /// <returns>True if cached.</returns>
        public bool TryGetDocumentCount(out long count)
        {
            lock (_DocumentCountLock)
            {
                if (_Configuration.Enabled && _CachedDocumentCount.HasValue)
                {
                    count = _CachedDocumentCount.Value;
                    return true;
                }
                count = 0;
                return false;
            }
        }

        /// <summary>
        /// Set the cached document count.
        /// </summary>
        /// <param name="count">The document count.</param>
        public void SetDocumentCount(long count)
        {
            if (!_Configuration.Enabled)
                return;

            lock (_DocumentCountLock)
            {
                _CachedDocumentCount = count;
            }
        }

        /// <summary>
        /// Increment the cached document count.
        /// </summary>
        /// <param name="delta">Amount to increment (default 1).</param>
        public void IncrementDocumentCount(int delta = 1)
        {
            if (!_Configuration.Enabled)
                return;

            lock (_DocumentCountLock)
            {
                if (_CachedDocumentCount.HasValue)
                {
                    _CachedDocumentCount = _CachedDocumentCount.Value + delta;
                }
            }
        }

        /// <summary>
        /// Decrement the cached document count.
        /// </summary>
        /// <param name="delta">Amount to decrement (default 1).</param>
        public void DecrementDocumentCount(int delta = 1)
        {
            if (!_Configuration.Enabled)
                return;

            lock (_DocumentCountLock)
            {
                if (_CachedDocumentCount.HasValue)
                {
                    _CachedDocumentCount = Math.Max(0, _CachedDocumentCount.Value - delta);
                }
            }
        }

        /// <summary>
        /// Clear the cached document count.
        /// </summary>
        public void ClearDocumentCount()
        {
            lock (_DocumentCountLock)
            {
                _CachedDocumentCount = null;
            }
        }

        #endregion

        #region Public-Methods-Clear-All

        /// <summary>
        /// Clear all caches.
        /// </summary>
        public void ClearAll()
        {
            ClearTermCache();
            ClearDocumentCache();
            InvalidateStatistics();
            ClearDocumentCount();
        }

        /// <summary>
        /// Clear all caches asynchronously.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ClearAllAsync(CancellationToken token = default)
        {
            if (_TermCache != null)
            {
                await _TermCache.ClearAsync(token).ConfigureAwait(false);
            }
            if (_DocumentCache != null)
            {
                await _DocumentCache.ClearAsync(token).ConfigureAwait(false);
            }
            if (_StatisticsCache != null)
            {
                await _StatisticsCache.ClearAsync(token).ConfigureAwait(false);
            }
            ClearDocumentCount();
        }

        #endregion

        #region Public-Methods-Statistics

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        /// <returns>Cache statistics for this index.</returns>
        public VerbexCacheStatistics GetCacheStatistics()
        {
            VerbexCacheStatistics stats = new VerbexCacheStatistics
            {
                Enabled = _Configuration.Enabled
            };

            if (_TermCache != null && _Configuration.EnableTermCache)
            {
                Caching.CacheStatistics termStats = _TermCache.GetStatistics();
                stats.TermCache = new CacheStats
                {
                    Enabled = true,
                    HitCount = termStats.HitCount,
                    MissCount = termStats.MissCount,
                    HitRate = termStats.HitRate,
                    CurrentCount = termStats.CurrentCount,
                    Capacity = termStats.Capacity,
                    EvictionCount = termStats.EvictionCount,
                    ExpiredCount = termStats.ExpirationCount
                };
            }

            if (_DocumentCache != null && _Configuration.EnableDocumentCache)
            {
                Caching.CacheStatistics docStats = _DocumentCache.GetStatistics();
                stats.DocumentCache = new CacheStats
                {
                    Enabled = true,
                    HitCount = docStats.HitCount,
                    MissCount = docStats.MissCount,
                    HitRate = docStats.HitRate,
                    CurrentCount = docStats.CurrentCount,
                    Capacity = docStats.Capacity,
                    EvictionCount = docStats.EvictionCount,
                    ExpiredCount = docStats.ExpirationCount
                };
            }

            if (_StatisticsCache != null && _Configuration.EnableStatisticsCache)
            {
                Caching.CacheStatistics statsStats = _StatisticsCache.GetStatistics();
                stats.StatisticsCache = new CacheStats
                {
                    Enabled = true,
                    HitCount = statsStats.HitCount,
                    MissCount = statsStats.MissCount,
                    HitRate = statsStats.HitRate,
                    CurrentCount = statsStats.CurrentCount,
                    Capacity = statsStats.Capacity,
                    EvictionCount = statsStats.EvictionCount,
                    ExpiredCount = statsStats.ExpirationCount
                };
            }

            lock (_DocumentCountLock)
            {
                stats.CachedDocumentCount = _CachedDocumentCount;
            }

            return stats;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the cache manager and all its caches.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_IsDisposed)
                return;

            if (disposing)
            {
                _TermCache?.Dispose();
                _DocumentCache?.Dispose();
                _StatisticsCache?.Dispose();

                _TermCache = null;
                _DocumentCache = null;
                _StatisticsCache = null;
            }

            _IsDisposed = true;
        }

        #endregion

        #region Private-Methods

        private void InitializeCaches()
        {
            if (_Configuration.EnableTermCache)
            {
                _TermCache = new LRUCache<string, TermRecord>(
                    _Configuration.TermCacheCapacity,
                    _Configuration.TermCacheEvictCount);
                _TermCache.SlidingExpiration = _Configuration.TermCacheSlidingExpiration;
            }

            if (_Configuration.EnableDocumentCache)
            {
                _DocumentCache = new LRUCache<string, DocumentMetadata>(
                    _Configuration.DocumentCacheCapacity,
                    _Configuration.DocumentCacheEvictCount);
                _DocumentCache.SlidingExpiration = _Configuration.DocumentCacheSlidingExpiration;
            }

            if (_Configuration.EnableStatisticsCache)
            {
                // Statistics cache only holds one entry
                _StatisticsCache = new LRUCache<string, IndexStatistics>(1, 1);
                _StatisticsCache.SlidingExpiration = false;
            }
        }

        private string BuildTermKey(string normalizedTerm)
        {
            return $"term:{_IndexId}:{normalizedTerm}";
        }

        private string BuildDocumentKey(string documentId)
        {
            return $"doc:{_IndexId}:{documentId}";
        }

        private string BuildStatisticsKey()
        {
            return $"stats:{_IndexId}";
        }

        #endregion
    }
}
