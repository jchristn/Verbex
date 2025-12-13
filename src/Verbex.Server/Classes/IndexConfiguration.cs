namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Configuration for an individual index.
    /// </summary>
    public class IndexConfiguration
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
        /// Maximum concurrent operations for this index.
        /// </summary>
        public int MaxConcurrentOperations
        {
            get
            {
                return _MaxConcurrentOperations;
            }
            set
            {
                _MaxConcurrentOperations = value < 1 ? 1 : value;
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
        /// Whether this index is enabled.
        /// </summary>
        public bool Enabled
        {
            get
            {
                return _Enabled;
            }
            set
            {
                _Enabled = value;
            }
        }

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

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
        public int MinTokenLength { get; set; } = 0;

        /// <summary>
        /// Maximum token length (0 = disabled).
        /// </summary>
        public int MaxTokenLength { get; set; } = 0;

        /// <summary>
        /// Hot cache size.
        /// </summary>
        public int HotCacheSize { get; set; } = 10000;

        /// <summary>
        /// Document cache size.
        /// </summary>
        public int DocumentCacheSize { get; set; } = 1000;

        /// <summary>
        /// Expected number of terms for bloom filter sizing.
        /// </summary>
        public int ExpectedTerms { get; set; } = 1000000;

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
        private int _MaxConcurrentOperations = 4;
        private bool _InMemory = false;
        private bool _Enabled = true;
        private string _StorageMode = "MemoryOnly";
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public IndexConfiguration()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="id">Index identifier.</param>
        /// <param name="name">Index name.</param>
        /// <param name="description">Index description.</param>
        /// <param name="repositoryFilename">Repository filename.</param>
        public IndexConfiguration(string id, string name, string description, string repositoryFilename)
        {
            Id = id;
            Name = name;
            Description = description;
            RepositoryFilename = repositoryFilename;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load index configuration from a JSON file.
        /// </summary>
        /// <param name="filename">Path to the index.json file.</param>
        /// <returns>Index configuration, or null if file does not exist or is invalid.</returns>
        public static IndexConfiguration? FromFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) return null;
            if (!File.Exists(filename)) return null;

            try
            {
                string json = File.ReadAllText(filename);
                return JsonSerializer.Deserialize<IndexConfiguration>(json, GetJsonSerializerOptions());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save index configuration to a JSON file.
        /// </summary>
        /// <param name="filename">Path to save the index.json file.</param>
        /// <exception cref="ArgumentNullException">Thrown when filename is null or empty.</exception>
        public void ToFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            string? directoryPath = Path.GetDirectoryName(filename);
            if (!String.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string json = JsonSerializer.Serialize(this, GetJsonSerializerOptions());
            File.WriteAllText(filename, json);
        }

        /// <summary>
        /// Gets the JSON serializer options used for index configuration serialization.
        /// Uses PascalCase property naming (default .NET behavior).
        /// </summary>
        /// <returns>JSON serializer options.</returns>
        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null
            };
        }

        #endregion
    }
}