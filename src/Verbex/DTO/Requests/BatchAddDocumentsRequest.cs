namespace Verbex.DTO.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// Request to add multiple documents in a batch operation.
    /// </summary>
    public class BatchAddDocumentsRequest
    {
        #region Public-Members

        /// <summary>
        /// List of documents to add.
        /// </summary>
        public List<BatchAddDocumentItem> Documents
        {
            get => _Documents;
            set => _Documents = value ?? new List<BatchAddDocumentItem>();
        }

        #endregion

        #region Private-Members

        private List<BatchAddDocumentItem> _Documents = new List<BatchAddDocumentItem>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchAddDocumentsRequest()
        {
        }

        /// <summary>
        /// Instantiate with documents.
        /// </summary>
        /// <param name="documents">Documents to add.</param>
        public BatchAddDocumentsRequest(List<BatchAddDocumentItem> documents)
        {
            _Documents = documents ?? new List<BatchAddDocumentItem>();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate the request.
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool Validate(out string errorMessage)
        {
            if (_Documents.Count == 0)
            {
                errorMessage = "At least one document is required";
                return false;
            }

            for (int i = 0; i < _Documents.Count; i++)
            {
                if (!_Documents[i].Validate(out string itemError))
                {
                    errorMessage = $"Document at index {i}: {itemError}";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        #endregion
    }
}
