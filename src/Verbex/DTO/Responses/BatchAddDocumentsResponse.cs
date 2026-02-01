namespace Verbex.DTO.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Response from a batch add documents operation.
    /// </summary>
    public class BatchAddDocumentsResponse
    {
        #region Public-Members

        /// <summary>
        /// List of successfully added documents.
        /// </summary>
        public List<BatchAddDocumentResult> Added
        {
            get => _Added;
            set => _Added = value ?? new List<BatchAddDocumentResult>();
        }

        /// <summary>
        /// List of documents that failed to be added.
        /// </summary>
        public List<BatchAddDocumentResult> Failed
        {
            get => _Failed;
            set => _Failed = value ?? new List<BatchAddDocumentResult>();
        }

        /// <summary>
        /// Number of documents successfully added.
        /// </summary>
        public int AddedCount
        {
            get => _AddedCount;
            set => _AddedCount = value;
        }

        /// <summary>
        /// Number of documents that failed to be added.
        /// </summary>
        public int FailedCount
        {
            get => _FailedCount;
            set => _FailedCount = value;
        }

        /// <summary>
        /// Total number of documents in the request.
        /// </summary>
        public int RequestedCount
        {
            get => _RequestedCount;
            set => _RequestedCount = value;
        }

        #endregion

        #region Private-Members

        private List<BatchAddDocumentResult> _Added = new List<BatchAddDocumentResult>();
        private List<BatchAddDocumentResult> _Failed = new List<BatchAddDocumentResult>();
        private int _AddedCount = 0;
        private int _FailedCount = 0;
        private int _RequestedCount = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchAddDocumentsResponse()
        {
        }

        #endregion
    }
}
