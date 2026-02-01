namespace Verbex
{
    /// <summary>
    /// Aggregate cache statistics for an index.
    /// Contains statistics for all cache types (term, document, statistics).
    /// </summary>
    public class VerbexCacheStatistics
    {
        #region Public-Members

        /// <summary>
        /// Whether caching is enabled for this index.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Term cache statistics. Null if term cache is disabled.
        /// </summary>
        public CacheStats? TermCache { get; set; }

        /// <summary>
        /// Document metadata cache statistics. Null if document cache is disabled.
        /// </summary>
        public CacheStats? DocumentCache { get; set; }

        /// <summary>
        /// Statistics cache statistics. Null if statistics cache is disabled.
        /// </summary>
        public CacheStats? StatisticsCache { get; set; }

        /// <summary>
        /// Cached document count value. Null if not cached.
        /// </summary>
        public long? CachedDocumentCount { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new VerbexCacheStatistics with default values.
        /// </summary>
        public VerbexCacheStatistics()
        {
        }

        #endregion
    }
}
