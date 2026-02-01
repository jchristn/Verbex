namespace Verbex.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Internal result from indexing document content.
    /// Contains both the ingestion metrics and the list of affected terms.
    /// </summary>
    internal class IndexContentResult
    {
        #region Public-Members

        /// <summary>
        /// Ingestion metrics containing timing and count information.
        /// </summary>
        public IngestionMetrics Metrics
        {
            get { return _Metrics; }
            set { _Metrics = value ?? new IngestionMetrics(); }
        }

        /// <summary>
        /// List of affected term values for cache invalidation.
        /// </summary>
        public List<string> AffectedTerms
        {
            get { return _AffectedTerms; }
            set { _AffectedTerms = value ?? new List<string>(); }
        }

        #endregion

        #region Private-Members

        private IngestionMetrics _Metrics = new IngestionMetrics();
        private List<string> _AffectedTerms = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexContentResult"/> class.
        /// </summary>
        public IndexContentResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexContentResult"/> class.
        /// </summary>
        /// <param name="metrics">The ingestion metrics.</param>
        /// <param name="affectedTerms">The list of affected terms.</param>
        public IndexContentResult(IngestionMetrics metrics, List<string> affectedTerms)
        {
            _Metrics = metrics ?? new IngestionMetrics();
            _AffectedTerms = affectedTerms ?? new List<string>();
        }

        #endregion
    }
}
