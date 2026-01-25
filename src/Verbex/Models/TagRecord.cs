namespace Verbex.Models
{
    using System;

    /// <summary>
    /// Record type for tags table rows.
    /// </summary>
    public class TagRecord
    {
        /// <summary>Record ID (k-sortable unique identifier).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Tenant ID (null for non-tenant tags).</summary>
        public string? TenantId { get; set; }

        /// <summary>User ID (null for non-user tags).</summary>
        public string? UserId { get; set; }

        /// <summary>Credential ID (null for non-credential tags).</summary>
        public string? CredentialId { get; set; }

        /// <summary>Document ID (null for index-level tags).</summary>
        public string? DocumentId { get; set; }

        /// <summary>Tag key.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Tag value.</summary>
        public string? Value { get; set; }

        /// <summary>Timestamp when the record was last modified.</summary>
        public DateTime LastUpdateUtc { get; set; }

        /// <summary>Timestamp when the record was created.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
