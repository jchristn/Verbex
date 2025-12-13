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
        /// Unique identifier for the index.
        /// </summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                _Id = value ?? "";
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
        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = value ?? "";
            }
        }

        /// <summary>
        /// Repository filename for persistence.
        /// </summary>
        public string RepositoryFilename
        {
            get
            {
                return _RepositoryFilename;
            }
            set
            {
                _RepositoryFilename = value ?? "";
            }
        }

        /// <summary>
        /// Whether this index should be in-memory only.
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
        /// Storage mode for the index (MemoryOnly, PersistenceOnly, Hybrid).
        /// Default is MemoryOnly.
        /// </summary>
        public string StorageMode
        {
            get
            {
                return _StorageMode;
            }
            set
            {
                _StorageMode = value ?? "MemoryOnly";
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

        #endregion

        #region Private-Members

        private string _Id = "";
        private string _Name = "";
        private string _Description = "";
        private string _RepositoryFilename = "";
        private bool _InMemory = false;
        private string _StorageMode = "MemoryOnly";
        private int _MinTokenLength = 0;
        private int _MaxTokenLength = 0;
        private List<string>? _Labels = null;
        private Dictionary<string, string>? _Tags = null;

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
        /// <param name="id">Unique identifier for the index.</param>
        /// <param name="name">Display name for the index. Defaults to id if not specified.</param>
        public CreateIndexRequest(string id, string? name = null)
        {
            Id = id;
            Name = name ?? id;
            RepositoryFilename = $"{id}.db";
        }

        #endregion
    }
}
