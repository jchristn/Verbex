namespace Verbex.Models
{
    /// <summary>
    /// Aggregate term statistics for a document.
    /// </summary>
    public class DocumentTermStats
    {
        /// <summary>
        /// Document ID.
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

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
