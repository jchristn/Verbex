namespace Verbex.DTO.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a single document to be added in a batch operation.
    /// </summary>
    public class BatchAddDocumentItem
    {
        #region Public-Members

        /// <summary>
        /// Document identifier (optional, will be auto-generated if not provided).
        /// </summary>
        public string? Id
        {
            get => _Id;
            set => _Id = value;
        }

        /// <summary>
        /// Document name/path.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = value ?? string.Empty;
        }

        /// <summary>
        /// Document content.
        /// </summary>
        public string Content
        {
            get => _Content;
            set => _Content = value ?? string.Empty;
        }

        /// <summary>
        /// Labels for categorizing the document.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Custom tags (key-value pairs) for the document.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Custom metadata for the document.
        /// Can be any JSON-serializable value.
        /// </summary>
        public object? CustomMetadata
        {
            get => _CustomMetadata;
            set => _CustomMetadata = value;
        }

        #endregion

        #region Private-Members

        private string? _Id = null;
        private string _Name = string.Empty;
        private string _Content = string.Empty;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private object? _CustomMetadata = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchAddDocumentItem()
        {
        }

        /// <summary>
        /// Instantiate with name and content.
        /// </summary>
        /// <param name="name">Document name.</param>
        /// <param name="content">Document content.</param>
        public BatchAddDocumentItem(string name, string content)
        {
            _Name = name ?? string.Empty;
            _Content = content ?? string.Empty;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate the document item.
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrEmpty(_Name))
            {
                errorMessage = "Document name is required";
                return false;
            }

            if (string.IsNullOrEmpty(_Content))
            {
                errorMessage = "Document content is required";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        #endregion
    }
}
