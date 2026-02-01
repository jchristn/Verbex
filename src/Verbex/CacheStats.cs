namespace Verbex
{
    using System;

    /// <summary>
    /// Statistics for a single cache instance (term cache, document cache, etc.).
    /// </summary>
    public class CacheStats
    {
        #region Private-Members

        private double _HitRate = 0.0;

        #endregion

        #region Public-Members

        /// <summary>
        /// Whether this cache is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Total number of cache hits.
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Total number of cache misses.
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Cache hit rate (0.0 to 1.0).
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double HitRate
        {
            get { return _HitRate; }
            set { _HitRate = Math.Round(value, 4); }
        }

        /// <summary>
        /// Current number of entries in the cache.
        /// </summary>
        public int CurrentCount { get; set; }

        /// <summary>
        /// Maximum capacity of the cache.
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// Total number of entries evicted due to capacity limits.
        /// </summary>
        public long EvictionCount { get; set; }

        /// <summary>
        /// Total number of entries expired due to TTL.
        /// </summary>
        public long ExpiredCount { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new CacheStats with default values.
        /// </summary>
        public CacheStats()
        {
        }

        #endregion
    }
}
