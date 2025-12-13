namespace Verbex
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Comprehensive statistics for the inverted index
    /// </summary>
    public class IndexStatistics
    {
        /// <summary>
        /// Gets or sets the total number of documents in the index
        /// </summary>
        public long DocumentCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of unique terms in the index
        /// </summary>
        public long TermCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of postings (term-document pairs) in the index
        /// </summary>
        public long PostingCount { get; set; }

        /// <summary>
        /// Gets or sets the average document length in terms
        /// </summary>
        public double AverageDocumentLength { get; set; }

        /// <summary>
        /// Gets or sets the total size of all documents in characters
        /// </summary>
        public long TotalDocumentSize { get; set; }

        /// <summary>
        /// Gets or sets the total number of term occurrences across all documents
        /// </summary>
        public long TotalTermOccurrences { get; set; }

        /// <summary>
        /// Gets or sets the average terms per document
        /// </summary>
        public double AverageTermsPerDocument { get; set; }

        /// <summary>
        /// Gets or sets the average document frequency across all terms
        /// </summary>
        public double AverageDocumentFrequency { get; set; }

        /// <summary>
        /// Gets or sets the maximum document frequency (most common term)
        /// </summary>
        public long MaxDocumentFrequency { get; set; }

        /// <summary>
        /// Gets or sets the minimum document length
        /// </summary>
        public long MinDocumentLength { get; set; }

        /// <summary>
        /// Gets or sets the maximum document length
        /// </summary>
        public long MaxDocumentLength { get; set; }

        /// <summary>
        /// Gets or sets the number of terms currently in the hot cache (deprecated, always 0)
        /// </summary>
        public int HotCacheSize { get; set; }

        /// <summary>
        /// Gets or sets the number of documents currently in the recent documents cache (deprecated, always 0)
        /// </summary>
        public int DocumentCacheSize { get; set; }

        /// <summary>
        /// Gets or sets the current write buffer size (deprecated, always 0)
        /// </summary>
        public int WriteBufferSize { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when these statistics were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Gets or sets memory usage statistics
        /// </summary>
        public MemoryStatistics? Memory { get; set; }

        /// <summary>
        /// Gets or sets the top N most frequent terms
        /// </summary>
        public IReadOnlyList<TermFrequencyInfo>? TopTerms { get; set; }

        /// <summary>
        /// Initializes a new instance of the IndexStatistics class
        /// </summary>
        public IndexStatistics()
        {
            GeneratedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the IndexStatistics class
        /// </summary>
        /// <param name="documentCount">Total number of documents</param>
        /// <param name="termCount">Total number of unique terms</param>
        /// <param name="postingCount">Total number of postings</param>
        /// <param name="averageDocumentLength">Average document length</param>
        /// <param name="totalDocumentSize">Total document size in characters</param>
        /// <param name="hotCacheSize">Hot cache size</param>
        /// <param name="documentCacheSize">Document cache size</param>
        /// <param name="writeBufferSize">Write buffer size</param>
        /// <param name="memory">Memory statistics</param>
        /// <param name="topTerms">Top frequent terms</param>
        public IndexStatistics(
            long documentCount,
            long termCount,
            long postingCount,
            double averageDocumentLength,
            long totalDocumentSize,
            int hotCacheSize,
            int documentCacheSize,
            int writeBufferSize,
            MemoryStatistics memory,
            IReadOnlyList<TermFrequencyInfo> topTerms)
        {
            DocumentCount = documentCount;
            TermCount = termCount;
            PostingCount = postingCount;
            AverageDocumentLength = averageDocumentLength;
            TotalDocumentSize = totalDocumentSize;
            HotCacheSize = hotCacheSize;
            DocumentCacheSize = documentCacheSize;
            WriteBufferSize = writeBufferSize;
            GeneratedAt = DateTime.UtcNow;
            Memory = memory;
            TopTerms = topTerms;
        }
    }
}
