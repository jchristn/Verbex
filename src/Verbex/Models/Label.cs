namespace Verbex.Models
{
    using System;
    using System.Text.Json.Serialization;
    using PrettyId;

    /// <summary>
    /// Represents a label (string tag) attached to a tenant, user, credential, document, or index.
    /// </summary>
    /// <remarks>
    /// Labels are simple string identifiers that can be attached to entities
    /// for categorization and filtering. Multiple labels can be attached to a single entity.
    /// </remarks>
    public class Label
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "lbl_";

        private string _Identifier = string.Empty;
        private string _TenantId = string.Empty;
        private string _UserId = string.Empty;
        private string _CredentialId = string.Empty;
        private string _DocumentId = string.Empty;
        private string _IndexId = string.Empty;
        private string _LabelText = string.Empty;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the unique identifier for the label.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "lbl_" prefix.
        /// Example: "lbl_01ar5xxlajk1sxr6hzf29ksz4o01234567890abc".
        /// </value>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tenant ID this label is attached to.
        /// </summary>
        /// <value>
        /// The identifier of the tenant, or empty string if not a tenant-level label.
        /// </value>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the user ID this label is attached to.
        /// </summary>
        /// <value>
        /// The identifier of the user, or empty string if not a user-level label.
        /// </value>
        public string UserId
        {
            get => _UserId;
            set => _UserId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the credential ID this label is attached to.
        /// </summary>
        /// <value>
        /// The identifier of the credential, or empty string if not a credential-level label.
        /// </value>
        public string CredentialId
        {
            get => _CredentialId;
            set => _CredentialId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the document ID this label is attached to.
        /// </summary>
        /// <value>
        /// The identifier of the document, or empty string if this is an index-level label.
        /// </value>
        public string DocumentId
        {
            get => _DocumentId;
            set => _DocumentId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the index ID this label is attached to.
        /// </summary>
        /// <value>
        /// The identifier of the index. Required for both document and index-level labels.
        /// </value>
        public string IndexId
        {
            get => _IndexId;
            set => _IndexId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the label text.
        /// </summary>
        /// <value>The label string value.</value>
        public string LabelText
        {
            get => _LabelText;
            set => _LabelText = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the label was last modified.
        /// </summary>
        /// <value>The last modification timestamp in UTC.</value>
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the label was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Gets a value indicating whether this is a tenant-level label.
        /// </summary>
        /// <value>True if this label is attached to a tenant.</value>
        [JsonIgnore]
        public bool IsTenantLabel => !string.IsNullOrEmpty(_TenantId);

        /// <summary>
        /// Gets a value indicating whether this is a user-level label.
        /// </summary>
        /// <value>True if this label is attached to a user.</value>
        [JsonIgnore]
        public bool IsUserLabel => !string.IsNullOrEmpty(_UserId);

        /// <summary>
        /// Gets a value indicating whether this is a credential-level label.
        /// </summary>
        /// <value>True if this label is attached to a credential.</value>
        [JsonIgnore]
        public bool IsCredentialLabel => !string.IsNullOrEmpty(_CredentialId);

        /// <summary>
        /// Gets a value indicating whether this is a document-level label.
        /// </summary>
        /// <value>True if this label is attached to a document; false if index-level.</value>
        [JsonIgnore]
        public bool IsDocumentLabel => !string.IsNullOrEmpty(_DocumentId);

        /// <summary>
        /// Initializes a new instance of the <see cref="Label"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "lbl_" prefix.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public Label()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _CreatedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Label"/> class for a document.
        /// </summary>
        /// <param name="documentId">The document ID this label is attached to.</param>
        /// <param name="indexId">The index ID this label belongs to.</param>
        /// <param name="labelText">The label text.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public Label(string documentId, string indexId, string labelText) : this()
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentNullException(nameof(documentId), "Document ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(indexId))
            {
                throw new ArgumentNullException(nameof(indexId), "Index ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(labelText))
            {
                throw new ArgumentNullException(nameof(labelText), "Label text cannot be null or whitespace.");
            }

            _DocumentId = documentId;
            _IndexId = indexId;
            _LabelText = labelText;
        }

        /// <summary>
        /// Creates an index-level label.
        /// </summary>
        /// <param name="indexId">The index ID this label is attached to.</param>
        /// <param name="labelText">The label text.</param>
        /// <returns>A new Label instance for the index.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public static Label CreateIndexLabel(string indexId, string labelText)
        {
            if (string.IsNullOrWhiteSpace(indexId))
            {
                throw new ArgumentNullException(nameof(indexId), "Index ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(labelText))
            {
                throw new ArgumentNullException(nameof(labelText), "Label text cannot be null or whitespace.");
            }

            Label label = new Label();
            label._IndexId = indexId;
            label._LabelText = labelText;
            return label;
        }

        /// <summary>
        /// Creates a tenant-level label.
        /// </summary>
        /// <param name="tenantId">The tenant ID this label is attached to.</param>
        /// <param name="labelText">The label text.</param>
        /// <returns>A new Label instance for the tenant.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public static Label CreateTenantLabel(string tenantId, string labelText)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(labelText))
            {
                throw new ArgumentNullException(nameof(labelText), "Label text cannot be null or whitespace.");
            }

            Label label = new Label();
            label._TenantId = tenantId;
            label._LabelText = labelText;
            return label;
        }

        /// <summary>
        /// Creates a user-level label.
        /// </summary>
        /// <param name="userId">The user ID this label is attached to.</param>
        /// <param name="labelText">The label text.</param>
        /// <returns>A new Label instance for the user.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public static Label CreateUserLabel(string userId, string labelText)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentNullException(nameof(userId), "User ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(labelText))
            {
                throw new ArgumentNullException(nameof(labelText), "Label text cannot be null or whitespace.");
            }

            Label label = new Label();
            label._UserId = userId;
            label._LabelText = labelText;
            return label;
        }

        /// <summary>
        /// Creates a credential-level label.
        /// </summary>
        /// <param name="credentialId">The credential ID this label is attached to.</param>
        /// <param name="labelText">The label text.</param>
        /// <returns>A new Label instance for the credential.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public static Label CreateCredentialLabel(string credentialId, string labelText)
        {
            if (string.IsNullOrWhiteSpace(credentialId))
            {
                throw new ArgumentNullException(nameof(credentialId), "Credential ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(labelText))
            {
                throw new ArgumentNullException(nameof(labelText), "Label text cannot be null or whitespace.");
            }

            Label label = new Label();
            label._CredentialId = credentialId;
            label._LabelText = labelText;
            return label;
        }
    }
}
