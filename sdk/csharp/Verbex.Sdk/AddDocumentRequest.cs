namespace Verbex.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for adding a document to an index.
    /// </summary>
    public class AddDocumentRequest
    {
        #region Public-Members

        /// <summary>
        /// Document identifier (optional, will be auto-generated if not provided).
        /// Must be a valid GUID format if specified.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
        /// Document content to be indexed.
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Labels
        {
            get
            {
                return _Labels;
            }
            set
            {
                _Labels = value;
            }
        }

        /// <summary>
        /// Custom tags (key-value pairs) for the document.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                _Tags = value;
            }
        }

        #endregion

        #region Private-Members

        private string? _Id = null;
        private string _Content = "";
        private List<string>? _Labels = null;
        private Dictionary<string, string>? _Tags = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty AddDocumentRequest.
        /// </summary>
        public AddDocumentRequest()
        {
        }

        /// <summary>
        /// Instantiate an AddDocumentRequest with content.
        /// </summary>
        /// <param name="content">Document content to be indexed.</param>
        public AddDocumentRequest(string content)
        {
            Content = content;
        }

        /// <summary>
        /// Instantiate an AddDocumentRequest with content and optional parameters.
        /// </summary>
        /// <param name="content">Document content to be indexed.</param>
        /// <param name="id">Optional document identifier (GUID format).</param>
        /// <param name="labels">Optional labels for categorization.</param>
        /// <param name="tags">Optional custom tags.</param>
        public AddDocumentRequest(string content, string? id = null, List<string>? labels = null, Dictionary<string, string>? tags = null)
        {
            Content = content;
            Id = id;
            Labels = labels;
            Tags = tags;
        }

        #endregion
    }
}
