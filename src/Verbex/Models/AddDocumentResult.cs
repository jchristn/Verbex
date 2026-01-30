namespace Verbex.Models
{
    using System;

    /// <summary>
    /// Result of adding a document to the index.
    /// Contains the document ID and ingestion metrics.
    /// </summary>
    public class AddDocumentResult
    {
        #region Public-Members

        /// <summary>
        /// The document identifier.
        /// </summary>
        public string DocumentId
        {
            get { return _DocumentId; }
            set { _DocumentId = value ?? string.Empty; }
        }

        /// <summary>
        /// Ingestion metrics containing timing and count information.
        /// </summary>
        public IngestionMetrics Metrics
        {
            get { return _Metrics; }
            set { _Metrics = value ?? new IngestionMetrics(); }
        }

        #endregion

        #region Private-Members

        private string _DocumentId = string.Empty;
        private IngestionMetrics _Metrics = new IngestionMetrics();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="AddDocumentResult"/> class.
        /// </summary>
        public AddDocumentResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddDocumentResult"/> class.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="metrics">The ingestion metrics.</param>
        public AddDocumentResult(string documentId, IngestionMetrics metrics)
        {
            _DocumentId = documentId ?? string.Empty;
            _Metrics = metrics ?? new IngestionMetrics();
        }

        #endregion
    }
}
