namespace Verbex.Repositories
{
    using System;

    /// <summary>
    /// Record type for document table rows.
    /// </summary>
    public class DocumentRecord
    {
        /// <summary>Document ID (k-sortable unique identifier).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Document name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>SHA-256 hash of the document content.</summary>
        public string? ContentSha256 { get; set; }

        /// <summary>Document length in characters.</summary>
        public int DocumentLength { get; set; }

        /// <summary>Number of unique terms in the document.</summary>
        public int TermCount { get; set; }

        /// <summary>Timestamp when the document was indexed.</summary>
        public DateTime IndexedUtc { get; set; }

        /// <summary>Timestamp when the document was last modified.</summary>
        public DateTime? LastModifiedUtc { get; set; }

        /// <summary>Timestamp when the record was created.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
