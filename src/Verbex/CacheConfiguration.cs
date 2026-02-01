namespace Verbex
{
    using System;

    /// <summary>
    /// Configuration settings for per-index caching.
    /// </summary>
    public class CacheConfiguration
    {
        #region Private-Members

        private bool _Enabled = false;
        private bool _EnableTermCache = true;
        private int _TermCacheCapacity = 10000;
        private int _TermCacheEvictCount = 1000;
        private int _TermCacheTtlSeconds = 300;
        private bool _TermCacheSlidingExpiration = true;
        private bool _EnableDocumentCache = true;
        private int _DocumentCacheCapacity = 5000;
        private int _DocumentCacheEvictCount = 500;
        private int _DocumentCacheTtlSeconds = 600;
        private bool _DocumentCacheSlidingExpiration = true;
        private bool _EnableStatisticsCache = true;
        private int _StatisticsCacheTtlSeconds = 60;
        private bool _EnableTermIdCache = true;
        private int _TermIdCacheInitialCapacity = 10000;

        #endregion

        #region Public-Members

        /// <summary>
        /// Master switch to enable or disable all caching.
        /// Default: false
        /// </summary>
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// Enable term record caching for search operations.
        /// Default: true (when caching is enabled)
        /// </summary>
        public bool EnableTermCache
        {
            get => _EnableTermCache;
            set => _EnableTermCache = value;
        }

        /// <summary>
        /// Maximum number of term records to cache.
        /// Default: 10000
        /// Minimum: 1
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
        public int TermCacheCapacity
        {
            get => _TermCacheCapacity;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Capacity must be at least 1.");
                _TermCacheCapacity = value;
            }
        }

        /// <summary>
        /// Number of term entries to evict when capacity is reached.
        /// Default: 1000
        /// Minimum: 1
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
        public int TermCacheEvictCount
        {
            get => _TermCacheEvictCount;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Evict count must be at least 1.");
                _TermCacheEvictCount = value;
            }
        }

        /// <summary>
        /// Time-to-live for term cache entries in seconds.
        /// Default: 300 (5 minutes)
        /// Minimum: 1
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
        public int TermCacheTtlSeconds
        {
            get => _TermCacheTtlSeconds;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "TTL must be at least 1 second.");
                _TermCacheTtlSeconds = value;
            }
        }

        /// <summary>
        /// Whether term cache TTL refreshes on access.
        /// Default: true
        /// </summary>
        public bool TermCacheSlidingExpiration
        {
            get => _TermCacheSlidingExpiration;
            set => _TermCacheSlidingExpiration = value;
        }

        /// <summary>
        /// Enable document metadata caching.
        /// Default: true (when caching is enabled)
        /// </summary>
        public bool EnableDocumentCache
        {
            get => _EnableDocumentCache;
            set => _EnableDocumentCache = value;
        }

        /// <summary>
        /// Maximum number of document metadata entries to cache.
        /// Default: 5000
        /// Minimum: 1
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
        public int DocumentCacheCapacity
        {
            get => _DocumentCacheCapacity;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Capacity must be at least 1.");
                _DocumentCacheCapacity = value;
            }
        }

        /// <summary>
        /// Number of document entries to evict when capacity is reached.
        /// Default: 500
        /// Minimum: 1
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
        public int DocumentCacheEvictCount
        {
            get => _DocumentCacheEvictCount;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Evict count must be at least 1.");
                _DocumentCacheEvictCount = value;
            }
        }

        /// <summary>
        /// Time-to-live for document cache entries in seconds.
        /// Default: 600 (10 minutes)
        /// Minimum: 1
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
        public int DocumentCacheTtlSeconds
        {
            get => _DocumentCacheTtlSeconds;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "TTL must be at least 1 second.");
                _DocumentCacheTtlSeconds = value;
            }
        }

        /// <summary>
        /// Whether document cache TTL refreshes on access.
        /// Default: true
        /// </summary>
        public bool DocumentCacheSlidingExpiration
        {
            get => _DocumentCacheSlidingExpiration;
            set => _DocumentCacheSlidingExpiration = value;
        }

        /// <summary>
        /// Enable index statistics caching.
        /// Default: true (when caching is enabled)
        /// </summary>
        public bool EnableStatisticsCache
        {
            get => _EnableStatisticsCache;
            set => _EnableStatisticsCache = value;
        }

        /// <summary>
        /// Time-to-live for statistics cache in seconds.
        /// Default: 60 (1 minute)
        /// Minimum: 1
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
        public int StatisticsCacheTtlSeconds
        {
            get => _StatisticsCacheTtlSeconds;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "TTL must be at least 1 second.");
                _StatisticsCacheTtlSeconds = value;
            }
        }

        /// <summary>
        /// Enable term ID caching for optimized document ingestion.
        /// When enabled, term ID lookups are cached in memory to avoid database queries.
        /// This significantly improves ingestion performance for documents with overlapping terms.
        /// Default: true
        /// </summary>
        public bool EnableTermIdCache
        {
            get => _EnableTermIdCache;
            set => _EnableTermIdCache = value;
        }

        /// <summary>
        /// Initial capacity for the term ID cache dictionary.
        /// This is the number of entries the dictionary will pre-allocate.
        /// Default: 10000
        /// Minimum: 100
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 100.</exception>
        public int TermIdCacheInitialCapacity
        {
            get => _TermIdCacheInitialCapacity;
            set
            {
                if (value < 100)
                    throw new ArgumentOutOfRangeException(nameof(value), "Initial capacity must be at least 100.");
                _TermIdCacheInitialCapacity = value;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new CacheConfiguration with default values.
        /// </summary>
        public CacheConfiguration()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validates the cache configuration.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when values are invalid.</exception>
        public void Validate()
        {
            if (_TermCacheEvictCount > _TermCacheCapacity)
                throw new ArgumentOutOfRangeException(nameof(TermCacheEvictCount),
                    "Term cache evict count cannot exceed capacity.");

            if (_DocumentCacheEvictCount > _DocumentCacheCapacity)
                throw new ArgumentOutOfRangeException(nameof(DocumentCacheEvictCount),
                    "Document cache evict count cannot exceed capacity.");
        }

        /// <summary>
        /// Creates a deep copy of this configuration.
        /// </summary>
        /// <returns>A new CacheConfiguration instance with the same values.</returns>
        public CacheConfiguration Clone()
        {
            return new CacheConfiguration
            {
                Enabled = _Enabled,
                EnableTermCache = _EnableTermCache,
                TermCacheCapacity = _TermCacheCapacity,
                TermCacheEvictCount = _TermCacheEvictCount,
                TermCacheTtlSeconds = _TermCacheTtlSeconds,
                TermCacheSlidingExpiration = _TermCacheSlidingExpiration,
                EnableDocumentCache = _EnableDocumentCache,
                DocumentCacheCapacity = _DocumentCacheCapacity,
                DocumentCacheEvictCount = _DocumentCacheEvictCount,
                DocumentCacheTtlSeconds = _DocumentCacheTtlSeconds,
                DocumentCacheSlidingExpiration = _DocumentCacheSlidingExpiration,
                EnableStatisticsCache = _EnableStatisticsCache,
                StatisticsCacheTtlSeconds = _StatisticsCacheTtlSeconds,
                EnableTermIdCache = _EnableTermIdCache,
                TermIdCacheInitialCapacity = _TermIdCacheInitialCapacity
            };
        }

        #endregion
    }
}
