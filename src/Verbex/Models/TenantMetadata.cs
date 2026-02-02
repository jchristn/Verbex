namespace Verbex.Models
{
    using System;
    using System.Collections.Generic;
    using PrettyId;

    /// <summary>
    /// Represents metadata for a tenant in the multi-tenant system.
    /// </summary>
    /// <remarks>
    /// A tenant is the top-level organizational unit that provides isolation for users,
    /// credentials, indexes, documents, and all associated data.
    /// </remarks>
    public class TenantMetadata
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "ten_";

        private string _Identifier = string.Empty;
        private string _Name = string.Empty;
        private string _Description = string.Empty;
        private bool _Active = true;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the unique identifier for the tenant.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "ten_" prefix.
        /// Example: "ten_01ar5xxlajk1sxr6hzf29ksz4o".
        /// </value>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tenant ID (alias for <see cref="Identifier"/>).
        /// </summary>
        /// <value>Same as <see cref="Identifier"/>.</value>
        public string TenantId
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the display name of the tenant.
        /// </summary>
        /// <value>A human-readable name for the tenant. Must be unique across all tenants.</value>
        public string Name
        {
            get => _Name;
            set => _Name = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the description of the tenant.
        /// </summary>
        /// <value>An optional description for the tenant.</value>
        public string Description
        {
            get => _Description;
            set => _Description = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets whether the tenant is active.
        /// </summary>
        /// <value>
        /// True if the tenant is active and can be accessed; false if the tenant is disabled.
        /// Default is true.
        /// </value>
        public bool Active
        {
            get => _Active;
            set => _Active = value;
        }

        /// <summary>
        /// Gets or sets the labels for this tenant.
        /// </summary>
        /// <value>A list of string labels for categorization and filtering.</value>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Gets or sets the tags for this tenant.
        /// </summary>
        /// <value>A dictionary of key-value pairs for rich metadata.</value>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the tenant was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the tenant was last updated.
        /// </summary>
        /// <value>The last update timestamp in UTC.</value>
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantMetadata"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "ten_" prefix.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public TenantMetadata()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _CreatedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantMetadata"/> class with a name.
        /// </summary>
        /// <param name="name">The display name for the tenant.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or whitespace.</exception>
        public TenantMetadata(string name) : this()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name), "Tenant name cannot be null or whitespace.");
            }

            _Name = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantMetadata"/> class with a name and description.
        /// </summary>
        /// <param name="name">The display name for the tenant.</param>
        /// <param name="description">The description for the tenant.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or whitespace.</exception>
        public TenantMetadata(string name, string description) : this(name)
        {
            _Description = description ?? string.Empty;
        }
    }
}
