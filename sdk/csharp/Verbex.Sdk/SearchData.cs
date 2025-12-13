namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Search response data.
    /// </summary>
    public class SearchData
    {
        /// <summary>
        /// The search query that was executed.
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// List of search results.
        /// </summary>
        public List<SearchResult> Results { get; set; } = new List<SearchResult>();

        /// <summary>
        /// Total count of matching documents.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Maximum results requested.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Time taken for the search in milliseconds.
        /// </summary>
        public double SearchTime { get; set; }
    }
}
