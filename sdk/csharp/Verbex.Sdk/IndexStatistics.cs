namespace Verbex.Sdk
{
    using System;

    /// <summary>
    /// Index statistics model.
    /// </summary>
    public class IndexStatistics
    {
        /// <summary>
        /// Number of documents in the index.
        /// </summary>
        public long DocumentCount { get; set; }

        /// <summary>
        /// Number of unique terms in the index.
        /// </summary>
        public long TermCount { get; set; }

        /// <summary>
        /// Total number of postings (term-document pairs) in the index.
        /// </summary>
        public long PostingCount { get; set; }

        /// <summary>
        /// Average document length in terms.
        /// </summary>
        public double AverageDocumentLength { get; set; }

        /// <summary>
        /// Total size of all documents in characters.
        /// </summary>
        public long TotalDocumentSize { get; set; }

        /// <summary>
        /// Total number of term occurrences across all documents.
        /// </summary>
        public long TotalTermOccurrences { get; set; }

        /// <summary>
        /// Average terms per document.
        /// </summary>
        public double AverageTermsPerDocument { get; set; }

        /// <summary>
        /// Average document frequency across all terms.
        /// </summary>
        public double AverageDocumentFrequency { get; set; }

        /// <summary>
        /// Maximum document frequency (most common term).
        /// </summary>
        public long MaxDocumentFrequency { get; set; }

        /// <summary>
        /// Minimum document length.
        /// </summary>
        public long MinDocumentLength { get; set; }

        /// <summary>
        /// Maximum document length.
        /// </summary>
        public long MaxDocumentLength { get; set; }

        /// <summary>
        /// Cache statistics for this index.
        /// Null if caching is disabled.
        /// </summary>
        public VerbexCacheStatistics? CacheStatistics { get; set; }

        /// <summary>
        /// Timestamp when these statistics were generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Memory usage in megabytes.
        /// </summary>
        [Obsolete("Use detailed memory statistics when available.")]
        public double MemoryUsageMb { get; set; }
    }
}
