namespace Verbex.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for creating a new index.
    /// </summary>
    public class CreateIndexRequest
    {
        #region Public-Members

        /// <summary>
        /// Tenant ID for the index.
        /// Required for global admin users, ignored for tenant users.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                _TenantId = value;
            }
        }

        /// <summary>
        /// Display name for the index.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value ?? "";
            }
        }

        /// <summary>
        /// Description of the index.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = value;
            }
        }

        /// <summary>
        /// Whether this index should be in-memory only.
        /// When true, index data is not persisted and will be lost on restart.
        /// </summary>
        public bool InMemory
        {
            get
            {
                return _InMemory;
            }
            set
            {
                _InMemory = value;
            }
        }

        /// <summary>
        /// Whether to enable word lemmatization.
        /// </summary>
        public bool EnableLemmatizer { get; set; } = false;

        /// <summary>
        /// Whether to enable stop word removal.
        /// </summary>
        public bool EnableStopWordRemover { get; set; } = false;

        /// <summary>
        /// Minimum token length (0 = disabled).
        /// </summary>
        public int MinTokenLength
        {
            get
            {
                return _MinTokenLength;
            }
            set
            {
                _MinTokenLength = value < 0 ? 0 : value;
            }
        }

        /// <summary>
        /// Maximum token length (0 = disabled).
        /// </summary>
        public int MaxTokenLength
        {
            get
            {
                return _MaxTokenLength;
            }
            set
            {
                _MaxTokenLength = value < 0 ? 0 : value;
            }
        }

        /// <summary>
        /// Labels for categorizing the index.
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
        /// Custom tags (key-value pairs) for the index.
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

        /// <summary>
        /// Custom metadata object for the index.
        /// Can be any JSON-serializable object.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? CustomMetadata
        {
            get
            {
                return _CustomMetadata;
            }
            set
            {
                _CustomMetadata = value;
            }
        }

        #endregion

        #region Private-Members

        private string? _TenantId = null;
        private string _Name = "";
        private string? _Description = null;
        private bool _InMemory = false;
        private int _MinTokenLength = 0;
        private int _MaxTokenLength = 0;
        private List<string>? _Labels = null;
        private Dictionary<string, string>? _Tags = null;
        private object? _CustomMetadata = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty CreateIndexRequest.
        /// </summary>
        public CreateIndexRequest()
        {
        }

        /// <summary>
        /// Instantiate a CreateIndexRequest with required parameters.
        /// </summary>
        /// <param name="name">Display name for the index.</param>
        public CreateIndexRequest(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Instantiate a CreateIndexRequest with name and description.
        /// </summary>
        /// <param name="name">Display name for the index.</param>
        /// <param name="description">Description of the index.</param>
        public CreateIndexRequest(string name, string description)
        {
            Name = name;
            Description = description;
        }

        #endregion
    }
}
