namespace Verbex.DTO.Requests
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Request to delete multiple documents in a batch operation.
    /// </summary>
    public class BatchDeleteDocumentsRequest
    {
        #region Public-Members

        /// <summary>
        /// Document identifiers to delete.
        /// </summary>
        public List<string> DocumentIds
        {
            get => _DocumentIds;
            set => _DocumentIds = value ?? new List<string>();
        }

        /// <summary>
        /// Alternate identifier list property accepted for compatibility with generic bulk-delete clients.
        /// </summary>
        public List<string> Ids
        {
            get => _Ids;
            set => _Ids = value ?? new List<string>();
        }

        #endregion

        #region Private-Members

        private List<string> _DocumentIds = new List<string>();
        private List<string> _Ids = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchDeleteDocumentsRequest()
        {
        }

        /// <summary>
        /// Instantiate with document identifiers.
        /// </summary>
        /// <param name="documentIds">Document identifiers to delete.</param>
        public BatchDeleteDocumentsRequest(List<string> documentIds)
        {
            _DocumentIds = documentIds ?? new List<string>();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Gets the normalized distinct document IDs from the request.
        /// </summary>
        /// <returns>Distinct non-empty document IDs.</returns>
        public List<string> GetDocumentIds()
        {
            IEnumerable<string> ids = _DocumentIds.Count > 0 ? _DocumentIds : _Ids;
            return ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Validate the request.
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool Validate(out string errorMessage)
        {
            if (GetDocumentIds().Count == 0)
            {
                errorMessage = "At least one document ID is required";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        #endregion
    }
}
