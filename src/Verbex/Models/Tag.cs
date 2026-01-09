namespace Verbex.Models
{
    using System;
    using System.Text.Json.Serialization;
    using PrettyId;

    /// <summary>
    /// Represents a key-value tag attached to a document or index.
    /// </summary>
    /// <remarks>
    /// Tags are key-value pairs that can be attached to documents or indexes
    /// for rich metadata and filtering. Multiple tags with the same key can exist on a single entity.
    /// </remarks>
    public class Tag
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "tag_";

        private string _Identifier = string.Empty;
        private string _DocumentId = string.Empty;
        private string _IndexId = string.Empty;
        private string _Key = string.Empty;
        private string _Value = string.Empty;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the unique identifier for the tag.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "tag_" prefix.
        /// Example: "tag_01ar5xxlajk1sxr6hzf29ksz4o01234567890abc".
        /// </value>
        [JsonPropertyName("identifier")]
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the document ID this tag is attached to.
        /// </summary>
        /// <value>
        /// The identifier of the document, or empty string if this is an index-level tag.
        /// </value>
        [JsonPropertyName("documentId")]
        public string DocumentId
        {
            get => _DocumentId;
            set => _DocumentId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the index ID this tag is attached to.
        /// </summary>
        /// <value>
        /// The identifier of the index. Required for both document and index-level tags.
        /// </value>
        [JsonPropertyName("indexId")]
        public string IndexId
        {
            get => _IndexId;
            set => _IndexId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tag key.
        /// </summary>
        /// <value>The key part of the key-value pair.</value>
        [JsonPropertyName("key")]
        public string Key
        {
            get => _Key;
            set => _Key = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tag value.
        /// </summary>
        /// <value>The value part of the key-value pair. May be null or empty.</value>
        [JsonPropertyName("value")]
        public string Value
        {
            get => _Value;
            set => _Value = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the tag was last modified.
        /// </summary>
        /// <value>The last modification timestamp in UTC.</value>
        [JsonPropertyName("lastUpdateUtc")]
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the tag was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Gets a value indicating whether this is a document-level tag.
        /// </summary>
        /// <value>True if this tag is attached to a document; false if index-level.</value>
        [JsonIgnore]
        public bool IsDocumentTag => !string.IsNullOrEmpty(_DocumentId);

        /// <summary>
        /// Initializes a new instance of the <see cref="Tag"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "tag_" prefix.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public Tag()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _CreatedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tag"/> class for a document.
        /// </summary>
        /// <param name="documentId">The document ID this tag is attached to.</param>
        /// <param name="indexId">The index ID this tag belongs to.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        /// <exception cref="ArgumentNullException">Thrown when documentId, indexId, or key is null or whitespace.</exception>
        public Tag(string documentId, string indexId, string key, string value) : this()
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentNullException(nameof(documentId), "Document ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(indexId))
            {
                throw new ArgumentNullException(nameof(indexId), "Index ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key), "Tag key cannot be null or whitespace.");
            }

            _DocumentId = documentId;
            _IndexId = indexId;
            _Key = key;
            _Value = value ?? string.Empty;
        }

        /// <summary>
        /// Creates an index-level tag.
        /// </summary>
        /// <param name="indexId">The index ID this tag is attached to.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        /// <returns>A new Tag instance for the index.</returns>
        /// <exception cref="ArgumentNullException">Thrown when indexId or key is null or whitespace.</exception>
        public static Tag CreateIndexTag(string indexId, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(indexId))
            {
                throw new ArgumentNullException(nameof(indexId), "Index ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key), "Tag key cannot be null or whitespace.");
            }

            Tag tag = new Tag();
            tag._IndexId = indexId;
            tag._Key = key;
            tag._Value = value ?? string.Empty;
            return tag;
        }
    }
}
