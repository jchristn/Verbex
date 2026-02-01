namespace Verbex.DTO.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// Request to check existence of multiple documents.
    /// </summary>
    public class BatchCheckExistenceRequest
    {
        #region Public-Members

        /// <summary>
        /// List of document IDs to check.
        /// </summary>
        public List<string> Ids
        {
            get => _Ids;
            set => _Ids = value ?? new List<string>();
        }

        #endregion

        #region Private-Members

        private List<string> _Ids = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchCheckExistenceRequest()
        {
        }

        /// <summary>
        /// Instantiate with IDs.
        /// </summary>
        /// <param name="ids">Document IDs to check.</param>
        public BatchCheckExistenceRequest(List<string> ids)
        {
            _Ids = ids ?? new List<string>();
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
            if (_Ids.Count == 0)
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
