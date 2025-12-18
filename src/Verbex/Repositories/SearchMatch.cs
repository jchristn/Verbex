namespace Verbex.Repositories
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a search match result from the repository.
    /// </summary>
    public class SearchMatch
    {
        /// <summary>
        /// The document ID.
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Total term frequency across matched terms.
        /// </summary>
        public int TotalFrequency { get; set; }

        /// <summary>
        /// Number of search terms matched.
        /// </summary>
        public int MatchedTermCount { get; set; }

        /// <summary>
        /// Per-term frequencies mapping term ID to frequency in this document.
        /// </summary>
        public Dictionary<string, int> TermFrequencies { get; set; } = new Dictionary<string, int>();
    }
}
