namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Create index request.
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
        /// Repository filename for this index.
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
        /// Storage mode for the index.
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
        /// Whether to enable lemmatization.
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
        /// Custom tags (key-value pairs) for the index.
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

        private string _Id = "";
        private string _Name = "";
        private string _Description = "";
        private string _RepositoryFilename = "";
        private bool _InMemory = false;
        private string _StorageMode = "MemoryOnly";
        private int _MinTokenLength = 0;
        private int _MaxTokenLength = 0;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CreateIndexRequest()
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
            if (String.IsNullOrEmpty(_Id))
            {
                errorMessage = "Id is required";
                return false;
            }

            if (String.IsNullOrEmpty(_Name))
            {
                errorMessage = "Name is required";
                return false;
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// Convert to IndexConfiguration.
        /// </summary>
        /// <returns>IndexConfiguration instance.</returns>
        public IndexConfiguration ToIndexConfiguration()
        {
            string repoFilename = String.IsNullOrEmpty(_RepositoryFilename) ? $"{_Id}.db" : _RepositoryFilename;

            return new IndexConfiguration(_Id, _Name, _Description, repoFilename)
            {
                InMemory = _InMemory,
                StorageMode = _StorageMode,
                EnableLemmatizer = EnableLemmatizer,
                EnableStopWordRemover = EnableStopWordRemover,
                MinTokenLength = _MinTokenLength,
                MaxTokenLength = _MaxTokenLength,
                Labels = _Labels,
                Tags = _Tags
            };
        }

        #endregion
    }
}
