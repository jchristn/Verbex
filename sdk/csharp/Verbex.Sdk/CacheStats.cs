namespace Verbex.Sdk
{
    /// <summary>
    /// Statistics for a single cache instance.
    /// </summary>
    public class CacheStats
    {
        /// <summary>
        /// Whether this cache is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Number of cache hits.
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Number of cache misses.
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Cache hit rate (0.0 to 1.0).
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Current number of items in the cache.
        /// </summary>
        public int CurrentCount { get; set; }

        /// <summary>
        /// Maximum capacity of the cache.
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// Number of items evicted from the cache.
        /// </summary>
        public long EvictionCount { get; set; }

        /// <summary>
        /// Number of items expired from the cache.
        /// </summary>
        public long ExpiredCount { get; set; }
    }
}
