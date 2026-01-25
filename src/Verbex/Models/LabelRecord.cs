namespace Verbex.Models
{
    using System;

    /// <summary>
    /// Record type for labels table rows.
    /// </summary>
    public class LabelRecord
    {
        /// <summary>Record ID (k-sortable unique identifier).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Tenant ID (null for non-tenant labels).</summary>
        public string? TenantId { get; set; }

        /// <summary>User ID (null for non-user labels).</summary>
        public string? UserId { get; set; }

        /// <summary>Credential ID (null for non-credential labels).</summary>
        public string? CredentialId { get; set; }

        /// <summary>Document ID (null for index-level labels).</summary>
        public string? DocumentId { get; set; }

        /// <summary>Label text.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Timestamp when the record was last modified.</summary>
        public DateTime LastUpdateUtc { get; set; }

        /// <summary>Timestamp when the record was created.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
