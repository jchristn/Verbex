namespace Verbex
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Comprehensive statistics for the inverted index
    /// </summary>
    public class IndexStatistics
    {
        #region Private-Members

        private long _DocumentCount = 0;
        private long _TermCount = 0;
        private long _PostingCount = 0;
        private double _AverageDocumentLength = 0.0;
        private long _TotalDocumentSize = 0;
        private long _TotalTermOccurrences = 0;
        private double _AverageTermsPerDocument = 0.0;
        private double _AverageDocumentFrequency = 0.0;
        private long _MaxDocumentFrequency = 0;
        private long _MinDocumentLength = 0;
        private long _MaxDocumentLength = 0;
        private VerbexCacheStatistics? _CacheStatistics = null;
        private DateTime _GeneratedAt = DateTime.UtcNow;
        private MemoryStatistics? _Memory = null;
        private IReadOnlyList<TermFrequencyInfo>? _TopTerms = null;
        private int _TermIdCacheSize = 0;
        private bool _TermIdCacheLoaded = false;

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets the total number of documents in the index.
        /// </summary>
        public long DocumentCount
        {
            get { return _DocumentCount; }
            set { _DocumentCount = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets the total number of unique terms in the index.
        /// </summary>
        public long TermCount
        {
            get { return _TermCount; }
            set { _TermCount = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets the total number of postings (term-document pairs) in the index.
        /// </summary>
        public long PostingCount
        {
            get { return _PostingCount; }
            set { _PostingCount = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets the average document length in terms.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double AverageDocumentLength
        {
            get { return _AverageDocumentLength; }
            set { _AverageDocumentLength = value < 0.0 ? 0.0 : Math.Round(value, 4); }
        }

        /// <summary>
        /// Gets or sets the total size of all documents in characters.
        /// </summary>
        public long TotalDocumentSize
        {
            get { return _TotalDocumentSize; }
            set { _TotalDocumentSize = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets the total number of term occurrences across all documents.
        /// </summary>
        public long TotalTermOccurrences
        {
            get { return _TotalTermOccurrences; }
            set { _TotalTermOccurrences = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets the average terms per document.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double AverageTermsPerDocument
        {
            get { return _AverageTermsPerDocument; }
            set { _AverageTermsPerDocument = value < 0.0 ? 0.0 : Math.Round(value, 4); }
        }

        /// <summary>
        /// Gets or sets the average document frequency across all terms.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double AverageDocumentFrequency
        {
            get { return _AverageDocumentFrequency; }
            set { _AverageDocumentFrequency = value < 0.0 ? 0.0 : Math.Round(value, 4); }
        }

        /// <summary>
        /// Gets or sets the maximum document frequency (most common term).
        /// </summary>
        public long MaxDocumentFrequency
        {
            get { return _MaxDocumentFrequency; }
            set { _MaxDocumentFrequency = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets the minimum document length.
        /// </summary>
        public long MinDocumentLength
        {
            get { return _MinDocumentLength; }
            set { _MinDocumentLength = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets the maximum document length.
        /// </summary>
        public long MaxDocumentLength
        {
            get { return _MaxDocumentLength; }
            set { _MaxDocumentLength = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets the cache statistics for this index.
        /// Null if caching is disabled.
        /// </summary>
        public VerbexCacheStatistics? CacheStatistics
        {
            get { return _CacheStatistics; }
            set { _CacheStatistics = value; }
        }

        /// <summary>
        /// Gets or sets the timestamp when these statistics were generated.
        /// </summary>
        public DateTime GeneratedAt
        {
            get { return _GeneratedAt; }
            set { _GeneratedAt = value; }
        }

        /// <summary>
        /// Gets or sets memory usage statistics.
        /// </summary>
        public MemoryStatistics? Memory
        {
            get { return _Memory; }
            set { _Memory = value; }
        }

        /// <summary>
        /// Gets or sets the top N most frequent terms.
        /// </summary>
        public IReadOnlyList<TermFrequencyInfo>? TopTerms
        {
            get { return _TopTerms; }
            set { _TopTerms = value; }
        }

        /// <summary>
        /// Gets or sets the number of entries in the term ID cache.
        /// </summary>
        public int TermIdCacheSize
        {
            get { return _TermIdCacheSize; }
            set { _TermIdCacheSize = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Gets or sets whether the term ID cache has been loaded.
        /// </summary>
        public bool TermIdCacheLoaded
        {
            get { return _TermIdCacheLoaded; }
            set { _TermIdCacheLoaded = value; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the IndexStatistics class.
        /// </summary>
        public IndexStatistics()
        {
            _GeneratedAt = DateTime.UtcNow;
        }

        #endregion
    }
}
