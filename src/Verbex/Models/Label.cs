namespace Verbex.Models
{
    using System;
    using System.Text.Json.Serialization;
    using PrettyId;

    /// <summary>
    /// Represents a label (string tag) attached to a document or index.
    /// </summary>
    /// <remarks>
    /// Labels are simple string identifiers that can be attached to documents or indexes
    /// for categorization and filtering. Multiple labels can be attached to a single entity.
    /// </remarks>
    public class Label
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "lbl_";

        private string _Identifier = string.Empty;
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
        [JsonPropertyName("identifier")]
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the document ID this label is attached to.
        /// </summary>
        /// <value>
        /// The identifier of the document, or empty string if this is an index-level label.
        /// </value>
        [JsonPropertyName("documentId")]
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
        [JsonPropertyName("indexId")]
        public string IndexId
        {
            get => _IndexId;
            set => _IndexId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the label text.
        /// </summary>
        /// <value>The label string value.</value>
        [JsonPropertyName("label")]
        public string LabelText
        {
            get => _LabelText;
            set => _LabelText = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the label was last modified.
        /// </summary>
        /// <value>The last modification timestamp in UTC.</value>
        [JsonPropertyName("lastUpdateUtc")]
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the label was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

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
    }
}
