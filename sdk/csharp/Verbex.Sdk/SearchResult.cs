namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Individual search result model.
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// The document identifier.
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Relevance score for the result.
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Document metadata, when returned by the server.
        /// </summary>
        public DocumentInfo? Document { get; set; }

        /// <summary>
        /// Number of query terms matched by this result.
        /// </summary>
        public int MatchedTermCount { get; set; }

        /// <summary>
        /// Term-specific score contributions.
        /// </summary>
        public Dictionary<string, double>? TermScores { get; set; }

        /// <summary>
        /// Term frequencies in the matched document.
        /// </summary>
        public Dictionary<string, int>? TermFrequencies { get; set; }

        /// <summary>
        /// Total matched term occurrences across all query terms.
        /// </summary>
        public int TotalTermMatches { get; set; }

        /// <summary>
        /// Optional matched query terms, returned when requested.
        /// </summary>
        public List<string>? MatchedTerms { get; set; }

        /// <summary>
        /// Optional per-term details, returned when requested.
        /// </summary>
        public List<SearchTermDetail>? TermDetails { get; set; }

        /// <summary>
        /// Optional whole-document term statistics, returned when requested.
        /// </summary>
        public SearchDocumentTermStats? DocumentTermStats { get; set; }

        /// <summary>
        /// Document content or excerpt.
        /// </summary>
        public string? Content { get; set; }
    }

    /// <summary>
    /// Per-term search result detail.
    /// </summary>
    public class SearchTermDetail
    {
        /// <summary>
        /// Matched query term.
        /// </summary>
        public string Term { get; set; } = string.Empty;

        /// <summary>
        /// Term score contribution.
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Term frequency in the document.
        /// </summary>
        public int Frequency { get; set; }
    }

    /// <summary>
    /// Whole-document term statistics.
    /// </summary>
    public class SearchDocumentTermStats
    {
        /// <summary>
        /// Number of unique terms indexed for the document.
        /// </summary>
        public long UniqueTermCount { get; set; }

        /// <summary>
        /// Total indexed term occurrences for the document.
        /// </summary>
        public long TotalTermOccurrences { get; set; }
    }
}
