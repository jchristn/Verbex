namespace Verbex.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Document information model.
    /// Matches the server's DocumentMetadata serialization format.
    /// </summary>
    public class DocumentInfo
    {
        /// <summary>
        /// Unique identifier for the document.
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Document ID as string (alias for compatibility).
        /// </summary>
        public string Id => DocumentId;

        /// <summary>
        /// Document path.
        /// </summary>
        public string? DocumentPath { get; set; }

        /// <summary>
        /// Original file name.
        /// </summary>
        public string? OriginalFileName { get; set; }

        /// <summary>
        /// Document length in characters.
        /// </summary>
        public long DocumentLength { get; set; }

        /// <summary>
        /// UTC timestamp when the document was indexed.
        /// </summary>
        public DateTime? IndexedDate { get; set; }

        /// <summary>
        /// UTC timestamp when the document was last modified.
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// SHA-256 hash of the document content.
        /// </summary>
        public string? ContentSha256 { get; set; }

        /// <summary>
        /// Terms in the document.
        /// </summary>
        public List<string>? Terms { get; set; }

        /// <summary>
        /// Whether the document is deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Custom metadata (key-value pairs) associated with the document.
        /// </summary>
        public Dictionary<string, object>? CustomMetadata { get; set; }

        /// <summary>
        /// Labels associated with the document.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Tags (key-value pairs) associated with the document.
        /// Alias for CustomMetadata for API consistency.
        /// </summary>
        public Dictionary<string, object>? Tags { get; set; }
    }
}
