namespace Verbex.Repositories
{
    using System;

    /// <summary>
    /// Record type for index_metadata table rows.
    /// </summary>
    public class IndexMetadataRecord
    {
        /// <summary>Record ID (k-sortable unique identifier).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Index name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Timestamp when the index was last modified.</summary>
        public DateTime LastModifiedUtc { get; set; }

        /// <summary>Timestamp when the record was created.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
