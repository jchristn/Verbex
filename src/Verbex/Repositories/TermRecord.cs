namespace Verbex.Repositories
{
    using System;

    /// <summary>
    /// Record type for term table rows.
    /// </summary>
    public class TermRecord
    {
        /// <summary>Term ID (k-sortable unique identifier).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Term text.</summary>
        public string Term { get; set; } = string.Empty;

        /// <summary>Number of documents containing this term.</summary>
        public int DocumentFrequency { get; set; }

        /// <summary>Total occurrences of this term across all documents.</summary>
        public int TotalFrequency { get; set; }

        /// <summary>Timestamp when the term was last updated.</summary>
        public DateTime LastUpdatedUtc { get; set; }

        /// <summary>Timestamp when the record was created.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
