namespace Verbex.Models
{
    using System;

    /// <summary>
    /// Statistics for a single term returned by the repository.
    /// </summary>
    public class TermStatisticsResult
    {
        #region Private-Members

        private double _InverseDocumentFrequency = 0.0;
        private double _AverageFrequencyPerDocument = 0.0;

        #endregion

        #region Public-Members

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
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double InverseDocumentFrequency
        {
            get { return _InverseDocumentFrequency; }
            set { _InverseDocumentFrequency = Math.Round(value, 4); }
        }

        /// <summary>
        /// Average term frequency per document containing the term.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double AverageFrequencyPerDocument
        {
            get { return _AverageFrequencyPerDocument; }
            set { _AverageFrequencyPerDocument = Math.Round(value, 4); }
        }

        /// <summary>
        /// Maximum frequency in any single document.
        /// </summary>
        public int MaxFrequencyInDocument { get; set; }

        /// <summary>
        /// Minimum frequency in any document containing the term.
        /// </summary>
        public int MinFrequencyInDocument { get; set; }

        #endregion
    }
}
