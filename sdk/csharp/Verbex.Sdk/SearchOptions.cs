namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Optional search request settings.
    /// </summary>
    public class SearchOptions
    {
        /// <summary>
        /// Maximum number of results to return.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// If true, documents must contain all query terms.
        /// </summary>
        public bool UseAndLogic { get; set; }

        /// <summary>
        /// Optional labels to filter by.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Optional tags to filter by.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }

        /// <summary>
        /// Include matched query terms on each result.
        /// </summary>
        public bool IncludeMatchedTerms { get; set; }

        /// <summary>
        /// Include per-term score and frequency details on each result.
        /// </summary>
        public bool IncludeTermDetails { get; set; }

        /// <summary>
        /// Include whole-document aggregate term statistics on each result.
        /// </summary>
        public bool IncludeDocumentTermStats { get; set; }
    }
}
