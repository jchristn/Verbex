namespace Verbex.Sdk
{
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
        /// Document content or excerpt.
        /// </summary>
        public string? Content { get; set; }
    }
}
