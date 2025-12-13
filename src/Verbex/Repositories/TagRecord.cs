namespace Verbex.Repositories
{
    using System;

    /// <summary>
    /// Record type for tags table rows.
    /// </summary>
    public class TagRecord
    {
        /// <summary>Record ID (k-sortable unique identifier).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Document ID (null for index-level tags).</summary>
        public string? DocumentId { get; set; }

        /// <summary>Tag key.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Tag value.</summary>
        public string? Value { get; set; }

        /// <summary>Timestamp when the record was last modified.</summary>
        public DateTime LastModifiedUtc { get; set; }

        /// <summary>Timestamp when the record was created.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
