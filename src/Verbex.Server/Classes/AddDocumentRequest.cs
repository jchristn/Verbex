namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;
    using Verbex.Utilities;

    /// <summary>
    /// Add document request.
    /// </summary>
    public class AddDocumentRequest
    {
        #region Public-Members

        /// <summary>
        /// Document identifier (optional, will be auto-generated if not provided).
        /// </summary>
        public string? Id
        {
            get
            {
                return _Id;
            }
            set
            {
                _Id = value;
            }
        }

        /// <summary>
        /// Document content.
        /// </summary>
        public string Content
        {
            get
            {
                return _Content;
            }
            set
            {
                _Content = value ?? "";
            }
        }

        /// <summary>
        /// Labels for categorizing the document.
        /// </summary>
        public List<string> Labels
        {
            get
            {
                return _Labels;
            }
            set
            {
                _Labels = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Custom tags (key-value pairs) for the document.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                _Tags = value ?? new Dictionary<string, string>();
            }
        }

        #endregion

        #region Private-Members

        private string? _Id = null;
        private string _Content = "";
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AddDocumentRequest()
        {

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
            if (String.IsNullOrEmpty(_Content))
            {
                errorMessage = "Content is required";
                return false;
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// Get the document ID, generating one if not provided.
        /// </summary>
        /// <returns>Document ID string.</returns>
        public string GetDocumentId()
        {
            if (!String.IsNullOrEmpty(_Id))
            {
                return _Id;
            }

            return IdGenerator.GenerateDocumentId();
        }

        #endregion
    }
}
