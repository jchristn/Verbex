namespace Verbex
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Handles cache invalidation for all write operations.
    /// Ensures cache coherency by invalidating or updating cache entries when data changes.
    /// </summary>
    internal class CacheInvalidator
    {
        #region Private-Members

        private readonly IndexCacheManager _CacheManager;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new CacheInvalidator.
        /// </summary>
        /// <param name="cacheManager">The cache manager to invalidate.</param>
        /// <exception cref="ArgumentNullException">Thrown when cacheManager is null.</exception>
        public CacheInvalidator(IndexCacheManager cacheManager)
        {
            ArgumentNullException.ThrowIfNull(cacheManager);
            _CacheManager = cacheManager;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Called when a document is added to the index.
        /// Updates caches appropriately.
        /// </summary>
        /// <param name="documentId">The added document ID.</param>
        /// <param name="affectedTerms">Terms that were added or updated.</param>
        public void OnDocumentAdded(string documentId, IEnumerable<string>? affectedTerms = null)
        {
            if (!_CacheManager.Enabled)
                return;

            // Invalidate statistics cache (document count changed)
            _CacheManager.InvalidateStatistics();

            // Increment document count
            _CacheManager.IncrementDocumentCount();

            // Invalidate affected term caches (frequencies changed)
            if (affectedTerms != null)
            {
                _CacheManager.RemoveTerms(affectedTerms);
            }
        }

        /// <summary>
        /// Called when multiple documents are added to the index.
        /// Updates caches appropriately.
        /// </summary>
        /// <param name="documentIds">The added document IDs.</param>
        /// <param name="affectedTerms">Terms that were added or updated.</param>
        public void OnDocumentsAdded(IEnumerable<string> documentIds, IEnumerable<string>? affectedTerms = null)
        {
            if (!_CacheManager.Enabled)
                return;

            // Invalidate statistics cache
            _CacheManager.InvalidateStatistics();

            // Increment document count by batch size
            int count = 0;
            foreach (string _ in documentIds)
            {
                count++;
            }
            _CacheManager.IncrementDocumentCount(count);

            // Invalidate affected term caches
            if (affectedTerms != null)
            {
                _CacheManager.RemoveTerms(affectedTerms);
            }
        }

        /// <summary>
        /// Called when a document is removed from the index.
        /// Updates caches appropriately.
        /// </summary>
        /// <param name="documentId">The removed document ID.</param>
        /// <param name="affectedTerms">Terms that were affected (frequencies changed).</param>
        public void OnDocumentRemoved(string documentId, IEnumerable<string>? affectedTerms = null)
        {
            if (!_CacheManager.Enabled)
                return;

            // Invalidate statistics cache
            _CacheManager.InvalidateStatistics();

            // Decrement document count
            _CacheManager.DecrementDocumentCount();

            // Remove document from cache
            _CacheManager.RemoveDocument(documentId);

            // Invalidate affected term caches
            if (affectedTerms != null)
            {
                _CacheManager.RemoveTerms(affectedTerms);
            }
        }

        /// <summary>
        /// Called when multiple documents are removed from the index.
        /// Updates caches appropriately.
        /// </summary>
        /// <param name="documentIds">The removed document IDs.</param>
        /// <param name="affectedTerms">Terms that were affected (frequencies changed).</param>
        public void OnDocumentsRemoved(IEnumerable<string> documentIds, IEnumerable<string>? affectedTerms = null)
        {
            if (!_CacheManager.Enabled)
                return;

            // Invalidate statistics cache
            _CacheManager.InvalidateStatistics();

            // Remove documents from cache and count
            int count = 0;
            foreach (string docId in documentIds)
            {
                _CacheManager.RemoveDocument(docId);
                count++;
            }
            _CacheManager.DecrementDocumentCount(count);

            // Invalidate affected term caches
            if (affectedTerms != null)
            {
                _CacheManager.RemoveTerms(affectedTerms);
            }
        }

        /// <summary>
        /// Called when a document's metadata is updated (labels, tags, custom metadata).
        /// Does not affect term caches.
        /// </summary>
        /// <param name="documentId">The updated document ID.</param>
        public void OnDocumentUpdated(string documentId)
        {
            if (!_CacheManager.Enabled)
                return;

            // Remove document from cache (metadata changed)
            _CacheManager.RemoveDocument(documentId);
        }

        /// <summary>
        /// Called when the index is cleared.
        /// Invalidates all caches.
        /// </summary>
        public void OnIndexCleared()
        {
            if (!_CacheManager.Enabled)
                return;

            // Clear all caches
            _CacheManager.ClearAll();
        }

        /// <summary>
        /// Called when terms are updated (frequencies changed).
        /// </summary>
        /// <param name="affectedTerms">Terms that were updated.</param>
        public void OnTermsUpdated(IEnumerable<string> affectedTerms)
        {
            if (!_CacheManager.Enabled)
                return;

            _CacheManager.RemoveTerms(affectedTerms);
        }

        #endregion
    }
}
