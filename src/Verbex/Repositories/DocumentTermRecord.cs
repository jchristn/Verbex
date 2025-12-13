namespace Verbex.Repositories
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Record type for document_terms table rows.
    /// </summary>
    public class DocumentTermRecord
    {
        /// <summary>Record ID (k-sortable unique identifier).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Document ID (k-sortable unique identifier).</summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>Term ID (k-sortable unique identifier).</summary>
        public string TermId { get; set; } = string.Empty;

        /// <summary>Number of times the term appears in the document.</summary>
        public int TermFrequency { get; set; }

        /// <summary>Character positions (absolute offsets) where the term appears.</summary>
        public List<int> CharacterPositions { get; set; } = new List<int>();

        /// <summary>Term positions (word indices) where the term appears.</summary>
        public List<int> TermPositions { get; set; } = new List<int>();

        /// <summary>Timestamp when the record was last modified.</summary>
        public DateTime LastModifiedUtc { get; set; }

        /// <summary>Timestamp when the record was created.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Term text (joined from terms table).</summary>
        public string? Term { get; set; }

        /// <summary>Document name (joined from documents table).</summary>
        public string? DocumentName { get; set; }
    }
}
