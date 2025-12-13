namespace Verbex.Repositories
{
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
    }
}
