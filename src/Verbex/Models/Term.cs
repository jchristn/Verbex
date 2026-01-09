namespace Verbex.Models
{
    using System;
    using System.Text.Json.Serialization;
    using PrettyId;

    /// <summary>
    /// Represents a term (word) in the vocabulary of a search index.
    /// </summary>
    /// <remarks>
    /// Terms are the indexed vocabulary of an index. Each unique word in the indexed content
    /// becomes a term with associated frequency statistics used for scoring.
    /// </remarks>
    public class Term
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "trm_";

        private string _Identifier = string.Empty;
        private string _TenantId = string.Empty;
        private string _IndexId = string.Empty;
        private string _TermText = string.Empty;
        private int _DocumentFrequency = 0;
        private long _TotalFrequency = 0;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the unique identifier for the term.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "trm_" prefix.
        /// Example: "trm_01ar5xxlajk1sxr6hzf29ksz4o01234567890abc".
        /// </value>
        [JsonPropertyName("identifier")]
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tenant ID this term belongs to.
        /// </summary>
        /// <value>The identifier of the tenant. Must reference a valid tenant.</value>
        [JsonPropertyName("tenantId")]
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the index ID this term belongs to.
        /// </summary>
        /// <value>The identifier of the index. Must reference a valid index within the tenant.</value>
        [JsonPropertyName("indexId")]
        public string IndexId
        {
            get => _IndexId;
            set => _IndexId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the term text.
        /// </summary>
        /// <value>The actual term string. Unique within an index.</value>
        [JsonPropertyName("term")]
        public string TermText
        {
            get => _TermText;
            set => _TermText = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the document frequency of the term.
        /// </summary>
        /// <value>
        /// The number of documents containing this term.
        /// Used for IDF (Inverse Document Frequency) calculation in TF-IDF scoring.
        /// </value>
        [JsonPropertyName("documentFrequency")]
        public int DocumentFrequency
        {
            get => _DocumentFrequency;
            set => _DocumentFrequency = value;
        }

        /// <summary>
        /// Gets or sets the total frequency of the term across all documents.
        /// </summary>
        /// <value>
        /// The total number of times this term appears across all documents in the index.
        /// </value>
        [JsonPropertyName("totalFrequency")]
        public long TotalFrequency
        {
            get => _TotalFrequency;
            set => _TotalFrequency = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the term was last updated.
        /// </summary>
        /// <value>The last update timestamp in UTC.</value>
        [JsonPropertyName("lastUpdateUtc")]
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the term was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Term"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "trm_" prefix.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public Term()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _CreatedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Term"/> class with tenant, index, and term text.
        /// </summary>
        /// <param name="tenantId">The tenant ID this term belongs to.</param>
        /// <param name="indexId">The index ID this term belongs to.</param>
        /// <param name="termText">The term text.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public Term(string tenantId, string indexId, string termText) : this()
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(indexId))
            {
                throw new ArgumentNullException(nameof(indexId), "Index ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(termText))
            {
                throw new ArgumentNullException(nameof(termText), "Term text cannot be null or whitespace.");
            }

            _TenantId = tenantId;
            _IndexId = indexId;
            _TermText = termText;
        }
    }
}
