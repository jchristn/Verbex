namespace Verbex.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using PrettyId;
    using Verbex;

    /// <summary>
    /// Represents metadata and configuration for a search index within a tenant.
    /// </summary>
    /// <remarks>
    /// An index is a container for documents, terms, and their relationships.
    /// Each index belongs to a specific tenant and provides document search functionality.
    /// This class combines both identity/metadata and runtime configuration settings.
    /// </remarks>
    public class IndexMetadata
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "idx_";

        private string _Identifier = string.Empty;
        private string _TenantId = string.Empty;
        private string _Name = string.Empty;
        private string _Description = string.Empty;
        private string _SchemaVersion = "3.0";
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;
        private bool _InMemory = false;
        private bool _Enabled = true;
        private bool _EnableLemmatizer = false;
        private bool _EnableStopWordRemover = false;
        private int _MinTokenLength = 0;
        private int _MaxTokenLength = 0;
        private int _ExpectedTerms = 1000000;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private object? _CustomMetadata = null;
        private CacheConfiguration? _CacheConfiguration = null;

        /// <summary>
        /// Gets or sets the unique identifier for the index.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "idx_" prefix.
        /// Example: "idx_01ar5xxlajk1sxr6hzf29ksz4o01234567890abc".
        /// </value>
        [JsonPropertyName("identifier")]
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tenant ID this index belongs to.
        /// </summary>
        /// <value>The identifier of the tenant. Must reference a valid tenant.</value>
        [JsonPropertyName("tenantId")]
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the display name of the index.
        /// </summary>
        /// <value>A human-readable name for the index. Must be unique within the tenant.</value>
        [JsonPropertyName("name")]
        public string Name
        {
            get => _Name;
            set => _Name = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the description of the index.
        /// </summary>
        /// <value>An optional description for the index.</value>
        [JsonPropertyName("description")]
        public string Description
        {
            get => _Description;
            set => _Description = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the schema version of the index.
        /// </summary>
        /// <value>The schema version string. Default is "3.0".</value>
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion
        {
            get => _SchemaVersion;
            set => _SchemaVersion = value ?? "3.0";
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the index was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the index was last updated.
        /// </summary>
        /// <value>The last update timestamp in UTC.</value>
        [JsonPropertyName("lastUpdateUtc")]
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Gets or sets whether this index should be stored in memory only (SQLite).
        /// </summary>
        /// <value>True for in-memory storage, false for disk-based storage. Default is false.</value>
        /// <remarks>When true, index data is not persisted and will be lost on restart.</remarks>
        [JsonPropertyName("inMemory")]
        public bool InMemory
        {
            get => _InMemory;
            set => _InMemory = value;
        }

        /// <summary>
        /// Gets or sets whether this index is enabled.
        /// </summary>
        /// <value>True if the index is enabled and can be used. Default is true.</value>
        [JsonPropertyName("enabled")]
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// Gets or sets whether to enable lemmatization for this index.
        /// </summary>
        /// <value>True to enable lemmatization during indexing. Default is false.</value>
        [JsonPropertyName("enableLemmatizer")]
        public bool EnableLemmatizer
        {
            get => _EnableLemmatizer;
            set => _EnableLemmatizer = value;
        }

        /// <summary>
        /// Gets or sets whether to enable stop word removal for this index.
        /// </summary>
        /// <value>True to enable stop word removal during indexing. Default is false.</value>
        [JsonPropertyName("enableStopWordRemover")]
        public bool EnableStopWordRemover
        {
            get => _EnableStopWordRemover;
            set => _EnableStopWordRemover = value;
        }

        /// <summary>
        /// Gets or sets the minimum token length for indexing.
        /// </summary>
        /// <value>Minimum token length. Tokens shorter than this are ignored. 0 means disabled. Default is 0.</value>
        [JsonPropertyName("minTokenLength")]
        public int MinTokenLength
        {
            get => _MinTokenLength;
            set => _MinTokenLength = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets the maximum token length for indexing.
        /// </summary>
        /// <value>Maximum token length. Tokens longer than this are ignored. 0 means disabled. Default is 0.</value>
        [JsonPropertyName("maxTokenLength")]
        public int MaxTokenLength
        {
            get => _MaxTokenLength;
            set => _MaxTokenLength = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets the expected number of terms for bloom filter sizing.
        /// </summary>
        /// <value>Expected number of unique terms. Default is 1000000.</value>
        [JsonPropertyName("expectedTerms")]
        public int ExpectedTerms
        {
            get => _ExpectedTerms;
            set => _ExpectedTerms = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets labels for categorizing the index.
        /// </summary>
        /// <value>List of string labels. Default is empty list.</value>
        [JsonPropertyName("labels")]
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Gets or sets custom tags (key-value pairs) for the index.
        /// </summary>
        /// <value>Dictionary of string key-value pairs. Default is empty dictionary.</value>
        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets custom metadata for the index.
        /// Can be any JSON-serializable value (object, array, string, number, boolean, null).
        /// </summary>
        /// <value>Any JSON-serializable value. Default is null.</value>
        [JsonPropertyName("customMetadata")]
        public object? CustomMetadata
        {
            get => _CustomMetadata;
            set => _CustomMetadata = value;
        }

        /// <summary>
        /// Gets or sets the cache configuration for this index.
        /// When null, caching is disabled.
        /// </summary>
        /// <value>Cache configuration settings, or null for disabled caching. Default is null.</value>
        [JsonPropertyName("cacheConfiguration")]
        public CacheConfiguration? CacheConfiguration
        {
            get => _CacheConfiguration;
            set => _CacheConfiguration = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexMetadata"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "idx_" prefix.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public IndexMetadata()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _CreatedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexMetadata"/> class with tenant and name.
        /// </summary>
        /// <param name="tenantId">The tenant ID this index belongs to.</param>
        /// <param name="name">The display name for the index.</param>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or name is null or whitespace.</exception>
        public IndexMetadata(string tenantId, string name) : this()
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name), "Index name cannot be null or whitespace.");
            }

            _TenantId = tenantId;
            _Name = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexMetadata"/> class with tenant, name, and description.
        /// </summary>
        /// <param name="tenantId">The tenant ID this index belongs to.</param>
        /// <param name="name">The display name for the index.</param>
        /// <param name="description">The description for the index.</param>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or name is null or whitespace.</exception>
        public IndexMetadata(string tenantId, string name, string description) : this(tenantId, name)
        {
            _Description = description ?? string.Empty;
        }
    }
}
