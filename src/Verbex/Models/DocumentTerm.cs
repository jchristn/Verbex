namespace Verbex.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using PrettyId;

    /// <summary>
    /// Represents the relationship between a document and a term in the inverted index.
    /// </summary>
    /// <remarks>
    /// DocumentTerm records form the core of the inverted index, mapping terms to documents
    /// with position information for phrase queries and proximity searches.
    /// </remarks>
    public class DocumentTerm
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "dtrm_";

        private string _Identifier = string.Empty;
        private string _DocumentId = string.Empty;
        private string _TermId = string.Empty;
        private int _TermFrequency = 0;
        private List<int> _CharacterPositions = new List<int>();
        private List<int> _TermPositions = new List<int>();
        private DateTime _LastUpdateUtc = DateTime.UtcNow;
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the unique identifier for the document-term mapping.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "dtrm_" prefix.
        /// Example: "dtrm_01ar5xxlajk1sxr6hzf29ksz4o01234567890abc".
        /// </value>
        [JsonPropertyName("identifier")]
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the document ID.
        /// </summary>
        /// <value>The identifier of the document. Must reference a valid document.</value>
        [JsonPropertyName("documentId")]
        public string DocumentId
        {
            get => _DocumentId;
            set => _DocumentId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the term ID.
        /// </summary>
        /// <value>The identifier of the term. Must reference a valid term.</value>
        [JsonPropertyName("termId")]
        public string TermId
        {
            get => _TermId;
            set => _TermId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the frequency of the term in this document.
        /// </summary>
        /// <value>
        /// The number of times the term appears in the document.
        /// Used for TF (Term Frequency) calculation in TF-IDF scoring.
        /// </value>
        [JsonPropertyName("termFrequency")]
        public int TermFrequency
        {
            get => _TermFrequency;
            set => _TermFrequency = value;
        }

        /// <summary>
        /// Gets or sets the character positions where the term appears in the document.
        /// </summary>
        /// <value>
        /// A list of character offsets (0-based) where the term starts in the document.
        /// Used for highlighting and snippet extraction.
        /// </value>
        [JsonPropertyName("characterPositions")]
        public List<int> CharacterPositions
        {
            get => _CharacterPositions;
            set => _CharacterPositions = value ?? new List<int>();
        }

        /// <summary>
        /// Gets or sets the term positions where the term appears in the document.
        /// </summary>
        /// <value>
        /// A list of term positions (0-based) indicating the term's ordinal position.
        /// Used for phrase queries and proximity searches.
        /// </value>
        [JsonPropertyName("termPositions")]
        public List<int> TermPositions
        {
            get => _TermPositions;
            set => _TermPositions = value ?? new List<int>();
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the document-term mapping was last modified.
        /// </summary>
        /// <value>The last modification timestamp in UTC.</value>
        [JsonPropertyName("lastUpdateUtc")]
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the document-term mapping was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentTerm"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "dtrm_" prefix.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public DocumentTerm()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _CreatedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentTerm"/> class with document and term IDs.
        /// </summary>
        /// <param name="documentId">The document ID.</param>
        /// <param name="termId">The term ID.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public DocumentTerm(string documentId, string termId) : this()
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentNullException(nameof(documentId), "Document ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(termId))
            {
                throw new ArgumentNullException(nameof(termId), "Term ID cannot be null or whitespace.");
            }

            _DocumentId = documentId;
            _TermId = termId;
        }
    }
}
