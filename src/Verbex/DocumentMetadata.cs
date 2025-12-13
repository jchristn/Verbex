namespace Verbex
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Metadata for a document stored in the inverted index
    /// </summary>
    public class DocumentMetadata
    {
        private string _DocumentId;
        private string _DocumentPath;
        private string? _OriginalFileName;
        private long _DocumentLength;
        private DateTime _IndexedDate;
        private DateTime _LastModified;
        private string _ContentSha256;
        private HashSet<string> _Terms;
        private bool _IsDeleted;
        private Dictionary<string, object> _CustomMetadata;
        private List<string> _Labels;

        /// <summary>
        /// Initializes a new instance of the DocumentMetadata class with minimal parameters.
        /// Used primarily by the repository layer.
        /// </summary>
        /// <param name="documentId">The unique document identifier</param>
        /// <param name="documentPath">The path or location of the document</param>
        /// <exception cref="ArgumentException">Thrown when documentId is empty</exception>
        /// <exception cref="ArgumentNullException">Thrown when documentPath is null</exception>
        public DocumentMetadata(string documentId, string documentPath)
        {
            ArgumentNullException.ThrowIfNull(documentId);

            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("Document ID cannot be empty", nameof(documentId));
            }

            ArgumentNullException.ThrowIfNull(documentPath);

            _DocumentId = documentId;
            _DocumentPath = documentPath;
            _OriginalFileName = null;
            _DocumentLength = 0;
            _ContentSha256 = string.Empty;
            _IndexedDate = DateTime.UtcNow;
            _LastModified = DateTime.UtcNow;
            _Terms = new HashSet<string>();
            _IsDeleted = false;
            _CustomMetadata = new Dictionary<string, object>();
            _Labels = new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the DocumentMetadata class
        /// </summary>
        /// <param name="documentId">The unique document identifier</param>
        /// <param name="documentPath">The path or location of the document</param>
        /// <param name="documentLength">The length of the document in characters</param>
        /// <param name="contentSha256">SHA-256 hash of the document content for duplicate detection</param>
        /// <param name="originalFileName">The original file name (optional, used for retrieval by name)</param>
        /// <exception cref="ArgumentException">Thrown when documentId is empty</exception>
        /// <exception cref="ArgumentNullException">Thrown when documentPath or contentSha256 is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when documentLength is negative</exception>
        public DocumentMetadata(string documentId, string documentPath, long documentLength, string contentSha256, string? originalFileName = null)
        {
            ArgumentNullException.ThrowIfNull(documentId);

            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("Document ID cannot be empty", nameof(documentId));
            }

            ArgumentNullException.ThrowIfNull(documentPath);
            ArgumentNullException.ThrowIfNull(contentSha256);

            if (documentLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(documentLength), "Document length cannot be negative");
            }

            _DocumentId = documentId;
            _DocumentPath = documentPath;
            _OriginalFileName = originalFileName;
            _DocumentLength = documentLength;
            _ContentSha256 = contentSha256;
            _IndexedDate = DateTime.UtcNow;
            _LastModified = DateTime.UtcNow;
            _Terms = new HashSet<string>();
            _IsDeleted = false;
            _CustomMetadata = new Dictionary<string, object>();
            _Labels = new List<string>();
        }

        /// <summary>
        /// Gets the unique document identifier
        /// </summary>
        public string DocumentId
        {
            get { return _DocumentId; }
        }

        /// <summary>
        /// Gets or sets the path or location of the document
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
        public string DocumentPath
        {
            get { return _DocumentPath; }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _DocumentPath = value;
                _LastModified = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets or sets the original file name of the document.
        /// This is used for retrieval by file name and is separate from the GUID-based storage path.
        /// Can be null if no original file name was provided.
        /// </summary>
        public string? OriginalFileName
        {
            get { return _OriginalFileName; }
            set
            {
                _OriginalFileName = value;
                _LastModified = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets or sets the length of the document in characters
        /// Minimum value: 0
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative</exception>
        public long DocumentLength
        {
            get { return _DocumentLength; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Document length cannot be negative");
                }
                _DocumentLength = value;
                _LastModified = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets or sets the date when the document was first indexed
        /// </summary>
        public DateTime IndexedDate
        {
            get { return _IndexedDate; }
            set { _IndexedDate = value; }
        }

        /// <summary>
        /// Gets or sets the date when the document metadata was last modified
        /// </summary>
        public DateTime LastModified
        {
            get { return _LastModified; }
            set { _LastModified = value; }
        }

        /// <summary>
        /// Gets or sets the SHA-256 hash of the document content for duplicate detection
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
        public string ContentSha256
        {
            get { return _ContentSha256; }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _ContentSha256 = value;
                _LastModified = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets a read-only collection of terms contained in this document
        /// </summary>
        public IReadOnlyCollection<string> Terms
        {
            get { return _Terms.ToArray(); }
        }

        /// <summary>
        /// Gets or sets whether this document has been marked for deletion
        /// Default value: false
        /// When true, the document will be excluded from search results and eligible for garbage collection
        /// </summary>
        public bool IsDeleted
        {
            get { return _IsDeleted; }
            set
            {
                _IsDeleted = value;
                _LastModified = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets a read-only dictionary of custom metadata properties
        /// </summary>
        public IReadOnlyDictionary<string, object> CustomMetadata
        {
            get { return new ReadOnlyDictionary<string, object>(_CustomMetadata); }
        }

        /// <summary>
        /// Gets a read-only dictionary of tags (key-value pairs) associated with this document.
        /// This is an alias for CustomMetadata for API consistency.
        /// </summary>
        [JsonInclude]
        public IReadOnlyDictionary<string, object> Tags
        {
            get { return CustomMetadata; }
        }

        /// <summary>
        /// Gets a read-only list of labels associated with this document.
        /// Labels are case-insensitive strings used for categorization.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<string> Labels
        {
            get { return _Labels.AsReadOnly(); }
        }

        /// <summary>
        /// Adds a term to the document's term collection
        /// </summary>
        /// <param name="term">The term to add</param>
        /// <returns>True if the term was added, false if it already existed</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        /// <exception cref="ArgumentException">Thrown when term is empty or whitespace</exception>
        public bool AddTerm(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException("Term cannot be empty or whitespace", nameof(term));
            }

            bool added = _Terms.Add(term.ToLowerInvariant());
            if (added)
            {
                _LastModified = DateTime.UtcNow;
            }
            return added;
        }

        /// <summary>
        /// Removes a term from the document's term collection
        /// </summary>
        /// <param name="term">The term to remove</param>
        /// <returns>True if the term was removed, false if it didn't exist</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        public bool RemoveTerm(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            bool removed = _Terms.Remove(term.ToLowerInvariant());
            if (removed)
            {
                _LastModified = DateTime.UtcNow;
            }
            return removed;
        }

        /// <summary>
        /// Sets custom metadata for the document
        /// </summary>
        /// <param name="key">The metadata key</param>
        /// <param name="value">The metadata value</param>
        /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or whitespace</exception>
        public void SetCustomMetadata(string key, object value)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty or whitespace", nameof(key));
            }

            _CustomMetadata[key] = value;
            _LastModified = DateTime.UtcNow;
        }

        /// <summary>
        /// Removes custom metadata for the specified key
        /// </summary>
        /// <param name="key">The metadata key to remove</param>
        /// <returns>True if the metadata was removed, false if key didn't exist</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
        public bool RemoveCustomMetadata(string key)
        {
            ArgumentNullException.ThrowIfNull(key);

            bool removed = _CustomMetadata.Remove(key);
            if (removed)
            {
                _LastModified = DateTime.UtcNow;
            }
            return removed;
        }

        /// <summary>
        /// Clears all terms from the document
        /// </summary>
        public void ClearTerms()
        {
            _Terms.Clear();
            _LastModified = DateTime.UtcNow;
        }

        /// <summary>
        /// Adds a label to the document's label collection.
        /// Labels are stored in lowercase for case-insensitive matching.
        /// </summary>
        /// <param name="label">The label to add</param>
        /// <returns>True if the label was added, false if it already existed</returns>
        /// <exception cref="ArgumentNullException">Thrown when label is null</exception>
        /// <exception cref="ArgumentException">Thrown when label is empty or whitespace</exception>
        public bool AddLabel(string label)
        {
            ArgumentNullException.ThrowIfNull(label);

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Label cannot be empty or whitespace", nameof(label));
            }

            string normalizedLabel = label.Trim().ToLowerInvariant();
            if (_Labels.Contains(normalizedLabel))
            {
                return false;
            }

            _Labels.Add(normalizedLabel);
            _LastModified = DateTime.UtcNow;
            return true;
        }

        /// <summary>
        /// Removes a label from the document's label collection
        /// </summary>
        /// <param name="label">The label to remove</param>
        /// <returns>True if the label was removed, false if it didn't exist</returns>
        /// <exception cref="ArgumentNullException">Thrown when label is null</exception>
        public bool RemoveLabel(string label)
        {
            ArgumentNullException.ThrowIfNull(label);

            string normalizedLabel = label.Trim().ToLowerInvariant();
            bool removed = _Labels.Remove(normalizedLabel);
            if (removed)
            {
                _LastModified = DateTime.UtcNow;
            }
            return removed;
        }

        /// <summary>
        /// Checks if the document has a specific label
        /// </summary>
        /// <param name="label">The label to check</param>
        /// <returns>True if the label exists, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when label is null</exception>
        public bool HasLabel(string label)
        {
            ArgumentNullException.ThrowIfNull(label);
            string normalizedLabel = label.Trim().ToLowerInvariant();
            return _Labels.Contains(normalizedLabel);
        }

        /// <summary>
        /// Clears all labels from the document
        /// </summary>
        public void ClearLabels()
        {
            _Labels.Clear();
            _LastModified = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets a tag (key-value pair) for the document.
        /// This is an alias for SetCustomMetadata for API consistency.
        /// </summary>
        /// <param name="key">The tag key</param>
        /// <param name="value">The tag value</param>
        /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or whitespace</exception>
        public void SetTag(string key, object value)
        {
            SetCustomMetadata(key, value);
        }

        /// <summary>
        /// Removes a tag for the specified key.
        /// This is an alias for RemoveCustomMetadata for API consistency.
        /// </summary>
        /// <param name="key">The tag key to remove</param>
        /// <returns>True if the tag was removed, false if key didn't exist</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
        public bool RemoveTag(string key)
        {
            return RemoveCustomMetadata(key);
        }
    }
}