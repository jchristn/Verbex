namespace Verbex.DTO.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of adding a single document in a batch operation.
    /// </summary>
    public class BatchAddDocumentResult
    {
        #region Public-Members

        /// <summary>
        /// Document identifier (the generated or provided ID).
        /// </summary>
        public string DocumentId
        {
            get => _DocumentId;
            set => _DocumentId = value ?? string.Empty;
        }

        /// <summary>
        /// Document name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = value ?? string.Empty;
        }

        /// <summary>
        /// Whether the document was added successfully.
        /// </summary>
        public bool Success
        {
            get => _Success;
            set => _Success = value;
        }

        /// <summary>
        /// Error message if the document failed to be added.
        /// </summary>
        public string? ErrorMessage
        {
            get => _ErrorMessage;
            set => _ErrorMessage = value;
        }

        #endregion

        #region Private-Members

        private string _DocumentId = string.Empty;
        private string _Name = string.Empty;
        private bool _Success = true;
        private string? _ErrorMessage = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchAddDocumentResult()
        {
        }

        /// <summary>
        /// Create a successful result.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="name">Document name.</param>
        /// <returns>Successful result.</returns>
        public static BatchAddDocumentResult Successful(string documentId, string name)
        {
            return new BatchAddDocumentResult
            {
                DocumentId = documentId,
                Name = name,
                Success = true,
                ErrorMessage = null
            };
        }

        /// <summary>
        /// Create a failed result.
        /// </summary>
        /// <param name="name">Document name.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>Failed result.</returns>
        public static BatchAddDocumentResult Failed(string name, string errorMessage)
        {
            return new BatchAddDocumentResult
            {
                DocumentId = string.Empty,
                Name = name,
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        #endregion
    }
}
