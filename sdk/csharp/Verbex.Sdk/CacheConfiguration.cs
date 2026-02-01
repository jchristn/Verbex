namespace Verbex.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Configuration settings for index caching.
    /// </summary>
    public class CacheConfiguration
    {
        #region Public-Members

        /// <summary>
        /// Whether caching is enabled.
        /// Default: false
        /// </summary>
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// Whether term caching is enabled.
        /// Default: true (when caching is enabled)
        /// </summary>
        public bool EnableTermCache
        {
            get => _EnableTermCache;
            set => _EnableTermCache = value;
        }

        /// <summary>
        /// Maximum number of terms to cache.
        /// Default: 10000
        /// </summary>
        public int TermCacheCapacity
        {
            get => _TermCacheCapacity;
            set => _TermCacheCapacity = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Number of terms to evict when cache is full.
        /// Default: 100
        /// </summary>
        public int TermCacheEvictCount
        {
            get => _TermCacheEvictCount;
            set => _TermCacheEvictCount = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Time-to-live in seconds for term cache entries.
        /// Default: 300 (5 minutes)
        /// </summary>
        public int TermCacheTtlSeconds
        {
            get => _TermCacheTtlSeconds;
            set => _TermCacheTtlSeconds = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Whether term cache uses sliding expiration.
        /// Default: true
        /// </summary>
        public bool TermCacheSlidingExpiration
        {
            get => _TermCacheSlidingExpiration;
            set => _TermCacheSlidingExpiration = value;
        }

        /// <summary>
        /// Whether document caching is enabled.
        /// Default: true (when caching is enabled)
        /// </summary>
        public bool EnableDocumentCache
        {
            get => _EnableDocumentCache;
            set => _EnableDocumentCache = value;
        }

        /// <summary>
        /// Maximum number of documents to cache.
        /// Default: 5000
        /// </summary>
        public int DocumentCacheCapacity
        {
            get => _DocumentCacheCapacity;
            set => _DocumentCacheCapacity = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Number of documents to evict when cache is full.
        /// Default: 50
        /// </summary>
        public int DocumentCacheEvictCount
        {
            get => _DocumentCacheEvictCount;
            set => _DocumentCacheEvictCount = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Time-to-live in seconds for document cache entries.
        /// Default: 600 (10 minutes)
        /// </summary>
        public int DocumentCacheTtlSeconds
        {
            get => _DocumentCacheTtlSeconds;
            set => _DocumentCacheTtlSeconds = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Whether document cache uses sliding expiration.
        /// Default: true
        /// </summary>
        public bool DocumentCacheSlidingExpiration
        {
            get => _DocumentCacheSlidingExpiration;
            set => _DocumentCacheSlidingExpiration = value;
        }

        /// <summary>
        /// Whether statistics caching is enabled.
        /// Default: true (when caching is enabled)
        /// </summary>
        public bool EnableStatisticsCache
        {
            get => _EnableStatisticsCache;
            set => _EnableStatisticsCache = value;
        }

        /// <summary>
        /// Time-to-live in seconds for statistics cache.
        /// Default: 60 (1 minute)
        /// </summary>
        public int StatisticsCacheTtlSeconds
        {
            get => _StatisticsCacheTtlSeconds;
            set => _StatisticsCacheTtlSeconds = value < 0 ? 0 : value;
        }

        #endregion

        #region Private-Members

        private bool _Enabled = false;
        private bool _EnableTermCache = true;
        private int _TermCacheCapacity = 10000;
        private int _TermCacheEvictCount = 100;
        private int _TermCacheTtlSeconds = 300;
        private bool _TermCacheSlidingExpiration = true;
        private bool _EnableDocumentCache = true;
        private int _DocumentCacheCapacity = 5000;
        private int _DocumentCacheEvictCount = 50;
        private int _DocumentCacheTtlSeconds = 600;
        private bool _DocumentCacheSlidingExpiration = true;
        private bool _EnableStatisticsCache = true;
        private int _StatisticsCacheTtlSeconds = 60;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a CacheConfiguration with default values (caching disabled).
        /// </summary>
        public CacheConfiguration()
        {
        }

        /// <summary>
        /// Instantiate a CacheConfiguration with caching enabled.
        /// </summary>
        /// <param name="enabled">Whether caching is enabled.</param>
        public CacheConfiguration(bool enabled)
        {
            _Enabled = enabled;
        }

        /// <summary>
        /// Creates a CacheConfiguration with default settings and caching enabled.
        /// </summary>
        /// <returns>A CacheConfiguration with caching enabled.</returns>
        public static CacheConfiguration CreateEnabled()
        {
            return new CacheConfiguration(true);
        }

        #endregion
    }
}
