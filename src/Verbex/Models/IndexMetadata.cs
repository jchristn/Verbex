namespace Verbex.Models
{
    using System;
    using System.Text.Json.Serialization;
    using PrettyId;

    /// <summary>
    /// Represents metadata for a search index within a tenant.
    /// </summary>
    /// <remarks>
    /// An index is a container for documents, terms, and their relationships.
    /// Each index belongs to a specific tenant and provides document search functionality.
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
