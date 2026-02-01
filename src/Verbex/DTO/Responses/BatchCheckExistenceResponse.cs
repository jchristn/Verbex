namespace Verbex.DTO.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Response from a batch existence check operation.
    /// </summary>
    public class BatchCheckExistenceResponse
    {
        #region Public-Members

        /// <summary>
        /// List of document IDs that exist.
        /// </summary>
        public List<string> Exists
        {
            get => _Exists;
            set => _Exists = value ?? new List<string>();
        }

        /// <summary>
        /// List of document IDs that do not exist.
        /// </summary>
        public List<string> NotFound
        {
            get => _NotFound;
            set => _NotFound = value ?? new List<string>();
        }

        /// <summary>
        /// Number of documents that exist.
        /// </summary>
        public int ExistsCount
        {
            get => _ExistsCount;
            set => _ExistsCount = value;
        }

        /// <summary>
        /// Number of documents that do not exist.
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

        private List<string> _Exists = new List<string>();
        private List<string> _NotFound = new List<string>();
        private int _ExistsCount = 0;
        private int _NotFoundCount = 0;
        private int _RequestedCount = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchCheckExistenceResponse()
        {
        }

        #endregion
    }
}
