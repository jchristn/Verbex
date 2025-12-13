namespace Verbex.Repositories
{
    using System;

    /// <summary>
    /// Record type for labels table rows.
    /// </summary>
    public class LabelRecord
    {
        /// <summary>Record ID (k-sortable unique identifier).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Document ID (null for index-level labels).</summary>
        public string? DocumentId { get; set; }

        /// <summary>Label text.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Timestamp when the record was last modified.</summary>
        public DateTime LastModifiedUtc { get; set; }

        /// <summary>Timestamp when the record was created.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
