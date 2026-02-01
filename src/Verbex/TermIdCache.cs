namespace Verbex
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Thread-safe in-memory cache for term ID lookups.
    /// Implements a write-through cache pattern to eliminate database queries for known terms.
    /// </summary>
    /// <remarks>
    /// This cache is used to optimize document ingestion performance by caching
    /// the mapping between normalized terms and their database IDs.
    /// Term IDs are immutable once created, so entries never need invalidation
    /// except on explicit removal (e.g., orphan cleanup).
    /// </remarks>
    public class TermIdCache : IDisposable
    {
        #region Private-Members

        private readonly string _TenantId;
        private readonly string _IndexId;
        private readonly Dictionary<string, string> _Cache;
        private readonly ReaderWriterLockSlim _Lock;
        private bool _IsLoaded;
        private bool _IsDisposed;

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets the tenant identifier for this cache.
        /// </summary>
        public string TenantId => _TenantId;

        /// <summary>
        /// Gets the index identifier for this cache.
        /// </summary>
        public string IndexId => _IndexId;

        /// <summary>
        /// Gets whether the cache has been populated with data.
        /// </summary>
        public bool IsLoaded => _IsLoaded;

        /// <summary>
        /// Gets the number of cached entries.
        /// </summary>
        public int Count
        {
            get
            {
                _Lock.EnterReadLock();
                try
                {
                    return _Cache.Count;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="TermIdCache"/> class.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="initialCapacity">Initial capacity for the cache dictionary. Default: 10000.</param>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or indexId is null.</exception>
        public TermIdCache(string tenantId, string indexId, int initialCapacity = 10000)
        {
            ArgumentNullException.ThrowIfNull(tenantId);
            ArgumentNullException.ThrowIfNull(indexId);

            _TenantId = tenantId;
            _IndexId = indexId;
            _Cache = new Dictionary<string, string>(initialCapacity);
            _Lock = new ReaderWriterLockSlim();
            _IsLoaded = false;
            _IsDisposed = false;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Attempts to get the ID for a term from the cache.
        /// </summary>
        /// <param name="term">The normalized term to look up.</param>
        /// <param name="termId">The term ID if found, null otherwise.</param>
        /// <returns>True if the term was found in the cache.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public bool TryGetId(string term, out string? termId)
        {
            ThrowIfDisposed();

            _Lock.EnterReadLock();
            try
            {
                return _Cache.TryGetValue(term, out termId);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Attempts to get IDs for multiple terms from the cache.
        /// </summary>
        /// <param name="terms">The normalized terms to look up.</param>
        /// <returns>A tuple containing found terms (term → ID mapping) and a list of terms not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when terms is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public (Dictionary<string, string> Found, List<string> NotFound) TryGetIds(IEnumerable<string> terms)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(terms);

            Dictionary<string, string> found = new Dictionary<string, string>();
            List<string> notFound = new List<string>();

            _Lock.EnterReadLock();
            try
            {
                foreach (string term in terms)
                {
                    if (_Cache.TryGetValue(term, out string? termId))
                    {
                        found[term] = termId;
                    }
                    else
                    {
                        notFound.Add(term);
                    }
                }
            }
            finally
            {
                _Lock.ExitReadLock();
            }

            return (found, notFound);
        }

        /// <summary>
        /// Sets or updates a term ID in the cache.
        /// </summary>
        /// <param name="term">The normalized term.</param>
        /// <param name="termId">The term ID.</param>
        /// <exception cref="ArgumentNullException">Thrown when term or termId is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public void Set(string term, string termId)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(term);
            ArgumentNullException.ThrowIfNull(termId);

            _Lock.EnterWriteLock();
            try
            {
                _Cache[term] = termId;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Sets or updates multiple term IDs in the cache.
        /// </summary>
        /// <param name="termIds">Dictionary mapping term text to term ID.</param>
        /// <exception cref="ArgumentNullException">Thrown when termIds is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public void SetRange(Dictionary<string, string> termIds)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(termIds);

            if (termIds.Count == 0) return;

            _Lock.EnterWriteLock();
            try
            {
                foreach (KeyValuePair<string, string> kvp in termIds)
                {
                    _Cache[kvp.Key] = kvp.Value;
                }
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Loads the cache with the provided term IDs, replacing all existing entries.
        /// </summary>
        /// <param name="termIds">Dictionary mapping term text to term ID.</param>
        /// <exception cref="ArgumentNullException">Thrown when termIds is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public void Load(Dictionary<string, string> termIds)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(termIds);

            _Lock.EnterWriteLock();
            try
            {
                _Cache.Clear();
                foreach (KeyValuePair<string, string> kvp in termIds)
                {
                    _Cache[kvp.Key] = kvp.Value;
                }
                _IsLoaded = true;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a term from the cache.
        /// </summary>
        /// <param name="term">The normalized term to remove.</param>
        /// <returns>True if the term was found and removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public bool Remove(string term)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(term);

            _Lock.EnterWriteLock();
            try
            {
                return _Cache.Remove(term);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes multiple terms from the cache.
        /// </summary>
        /// <param name="terms">The normalized terms to remove.</param>
        /// <returns>Number of terms removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when terms is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public int RemoveRange(IEnumerable<string> terms)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(terms);

            int removedCount = 0;

            _Lock.EnterWriteLock();
            try
            {
                foreach (string term in terms)
                {
                    if (_Cache.Remove(term))
                    {
                        removedCount++;
                    }
                }
            }
            finally
            {
                _Lock.ExitWriteLock();
            }

            return removedCount;
        }

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public void Clear()
        {
            ThrowIfDisposed();

            _Lock.EnterWriteLock();
            try
            {
                _Cache.Clear();
                _IsLoaded = false;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the cache and releases resources.
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
                _Lock.EnterWriteLock();
                try
                {
                    _Cache.Clear();
                }
                finally
                {
                    _Lock.ExitWriteLock();
                }

                _Lock.Dispose();
            }

            _IsDisposed = true;
        }

        #endregion

        #region Private-Methods

        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(TermIdCache));
            }
        }

        #endregion
    }
}
