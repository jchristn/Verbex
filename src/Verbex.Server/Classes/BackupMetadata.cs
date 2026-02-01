namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Verbex;

    /// <summary>
    /// Represents the index configuration and metadata snapshot in a backup archive.
    /// </summary>
    public class BackupMetadata
    {
        #region Private-Members

        private string _Identifier = string.Empty;
        private string _Name = string.Empty;
        private string _Description = string.Empty;
        private bool _Enabled = true;
        private bool _InMemory = false;
        private BackupConfiguration? _Configuration = null;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private object? _CustomMetadata = null;

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets the original identifier of the index.
        /// </summary>
        [JsonPropertyName("identifier")]
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the display name of the index.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name
        {
            get => _Name;
            set => _Name = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the description of the index.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description
        {
            get => _Description;
            set => _Description = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets whether the index was enabled.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// Gets or sets whether the index was in-memory only.
        /// </summary>
        [JsonPropertyName("inMemory")]
        public bool InMemory
        {
            get => _InMemory;
            set => _InMemory = value;
        }

        /// <summary>
        /// Gets or sets the index configuration settings.
        /// </summary>
        [JsonPropertyName("configuration")]
        public BackupConfiguration? Configuration
        {
            get => _Configuration;
            set => _Configuration = value;
        }

        /// <summary>
        /// Gets or sets the index labels.
        /// </summary>
        [JsonPropertyName("labels")]
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Gets or sets the index tags.
        /// </summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets custom metadata for the index.
        /// </summary>
        [JsonPropertyName("customMetadata")]
        public object? CustomMetadata
        {
            get => _CustomMetadata;
            set => _CustomMetadata = value;
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the BackupMetadata class.
        /// </summary>
        public BackupMetadata()
        {
        }

        #endregion
    }

    /// <summary>
    /// Represents index configuration settings in a backup.
    /// </summary>
    public class BackupConfiguration
    {
        #region Private-Members

        private int _MinTokenLength = 0;
        private int _MaxTokenLength = 0;
        private bool _EnableLemmatizer = false;
        private bool _EnableStopWordRemover = false;
        private CacheConfiguration? _CacheConfiguration = null;

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets the minimum token length.
        /// </summary>
        [JsonPropertyName("minTokenLength")]
        public int MinTokenLength
        {
            get => _MinTokenLength;
            set => _MinTokenLength = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets the maximum token length.
        /// </summary>
        [JsonPropertyName("maxTokenLength")]
        public int MaxTokenLength
        {
            get => _MaxTokenLength;
            set => _MaxTokenLength = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets whether lemmatization is enabled.
        /// </summary>
        [JsonPropertyName("enableLemmatizer")]
        public bool EnableLemmatizer
        {
            get => _EnableLemmatizer;
            set => _EnableLemmatizer = value;
        }

        /// <summary>
        /// Gets or sets whether stop word removal is enabled.
        /// </summary>
        [JsonPropertyName("enableStopWordRemover")]
        public bool EnableStopWordRemover
        {
            get => _EnableStopWordRemover;
            set => _EnableStopWordRemover = value;
        }

        /// <summary>
        /// Gets or sets the cache configuration.
        /// </summary>
        [JsonPropertyName("cacheConfiguration")]
        public CacheConfiguration? CacheConfiguration
        {
            get => _CacheConfiguration;
            set => _CacheConfiguration = value;
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the BackupConfiguration class.
        /// </summary>
        public BackupConfiguration()
        {
        }

        #endregion
    }
}
