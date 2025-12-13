namespace Verbex.Repositories
{
    /// <summary>
    /// Statistics for a single term returned by the repository.
    /// </summary>
    public class TermStatisticsResult
    {
        /// <summary>
        /// The term.
        /// </summary>
        public string Term { get; set; } = string.Empty;

        /// <summary>
        /// Number of documents containing this term.
        /// </summary>
        public int DocumentFrequency { get; set; }

        /// <summary>
        /// Total occurrences across all documents.
        /// </summary>
        public int TotalFrequency { get; set; }

        /// <summary>
        /// Inverse Document Frequency (log(N/df)).
        /// </summary>
        public double InverseDocumentFrequency { get; set; }

        /// <summary>
        /// Average term frequency per document containing the term.
        /// </summary>
        public double AverageFrequencyPerDocument { get; set; }

        /// <summary>
        /// Maximum frequency in any single document.
        /// </summary>
        public int MaxFrequencyInDocument { get; set; }

        /// <summary>
        /// Minimum frequency in any document containing the term.
        /// </summary>
        public int MinFrequencyInDocument { get; set; }
    }
}
