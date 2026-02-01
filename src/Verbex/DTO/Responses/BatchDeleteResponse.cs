namespace Verbex.DTO.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Response from a batch delete operation.
    /// </summary>
    public class BatchDeleteResponse
    {
        #region Public-Members

        /// <summary>
        /// List of document IDs that were successfully deleted.
        /// </summary>
        public List<string> Deleted
        {
            get => _Deleted;
            set => _Deleted = value ?? new List<string>();
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
        /// Number of documents successfully deleted.
        /// </summary>
        public int DeletedCount
        {
            get => _DeletedCount;
            set => _DeletedCount = value;
        }

        /// <summary>
        /// Number of documents not found.
        /// </summary>
        public int NotFoundCount
        {
            get => _NotFoundCount;
            set => _NotFoundCount = value;
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

        private List<string> _Deleted = new List<string>();
        private List<string> _NotFound = new List<string>();
        private int _DeletedCount = 0;
        private int _NotFoundCount = 0;
        private int _RequestedCount = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchDeleteResponse()
        {
        }

        #endregion
    }
}
