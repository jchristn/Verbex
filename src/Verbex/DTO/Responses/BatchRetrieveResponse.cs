namespace Verbex.DTO.Responses
{
    using System.Collections.Generic;
    using Verbex;

    /// <summary>
    /// Response from a batch retrieve operation.
    /// </summary>
    public class BatchRetrieveResponse
    {
        #region Public-Members

        /// <summary>
        /// List of documents that were found.
        /// </summary>
        public List<DocumentMetadata> Documents
        {
            get => _Documents;
            set => _Documents = value ?? new List<DocumentMetadata>();
        }

        /// <summary>
        /// List of document IDs that were not found.
        /// </summary>
        public List<string> NotFound
        {
            get => _NotFound;
            set => _NotFound = value ?? new List<string>();
        }

        /// <summary>
        /// Number of documents found.
        /// </summary>
        public int Count
        {
            get => _Count;
            set => _Count = value;
        }

        /// <summary>
        /// Total number of IDs in the request.
        /// </summary>
        public int RequestedCount
        {
            get => _RequestedCount;
            set => _RequestedCount = value;
        }

        #endregion

        #region Private-Members

        private List<DocumentMetadata> _Documents = new List<DocumentMetadata>();
        private List<string> _NotFound = new List<string>();
        private int _Count = 0;
        private int _RequestedCount = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchRetrieveResponse()
        {
        }

        #endregion
    }
}
