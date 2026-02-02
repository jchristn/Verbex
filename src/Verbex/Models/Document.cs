namespace Verbex.Models
{
    using System;
    using PrettyId;

    /// <summary>
    /// Represents a document stored within a search index.
    /// </summary>
    /// <remarks>
    /// Documents are the primary searchable entities within an index.
    /// Each document has a unique name within its index and contains indexed content.
    /// </remarks>
    public class Document
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "doc_";

        private string _Identifier = string.Empty;
        private string _TenantId = string.Empty;
        private string _IndexId = string.Empty;
        private string _Name = string.Empty;
        private string _ContentSha256 = string.Empty;
        private int _DocumentLength = 0;
        private int _TermCount = 0;
        private DateTime _IndexedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the unique identifier for the document.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "doc_" prefix.
        /// Example: "doc_01ar5xxlajk1sxr6hzf29ksz4o01234567890abc".
        /// </value>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tenant ID this document belongs to.
        /// </summary>
        /// <value>The identifier of the tenant. Must reference a valid tenant.</value>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the index ID this document belongs to.
        /// </summary>
        /// <value>The identifier of the index. Must reference a valid index within the tenant.</value>
        public string IndexId
        {
            get => _IndexId;
            set => _IndexId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the name of the document.
        /// </summary>
        /// <value>A unique name for the document within its index.</value>
        public string Name
        {
            get => _Name;
            set => _Name = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the SHA-256 hash of the document content.
        /// </summary>
        /// <value>
        /// A 64-character hexadecimal string representing the SHA-256 hash of the content.
        /// Used for duplicate detection and content verification.
        /// </value>
        public string ContentSha256
        {
            get => _ContentSha256;
            set => _ContentSha256 = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the length of the document content in characters.
        /// </summary>
        /// <value>The content length. Default is 0.</value>
        public int DocumentLength
        {
            get => _DocumentLength;
            set => _DocumentLength = value;
        }

        /// <summary>
        /// Gets or sets the number of unique terms in the document.
        /// </summary>
        /// <value>The term count. Default is 0.</value>
        public int TermCount
        {
            get => _TermCount;
            set => _TermCount = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the document was indexed.
        /// </summary>
        /// <value>The indexing timestamp in UTC.</value>
        public DateTime IndexedUtc
        {
            get => _IndexedUtc;
            set => _IndexedUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the document was last modified.
        /// </summary>
        /// <value>The last modification timestamp in UTC.</value>
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the document was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Document"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "doc_" prefix.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public Document()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _CreatedUtc = DateTime.UtcNow;
            _IndexedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Document"/> class with tenant, index, and name.
        /// </summary>
        /// <param name="tenantId">The tenant ID this document belongs to.</param>
        /// <param name="indexId">The index ID this document belongs to.</param>
        /// <param name="name">The name for the document.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public Document(string tenantId, string indexId, string name) : this()
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(indexId))
            {
                throw new ArgumentNullException(nameof(indexId), "Index ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name), "Document name cannot be null or whitespace.");
            }

            _TenantId = tenantId;
            _IndexId = indexId;
            _Name = name;
        }
    }
}
