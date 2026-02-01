namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response object for index metadata.
    /// </summary>
    public class IndexMetadataResponse
    {
        /// <summary>
        /// Unique identifier for the index.
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier that owns this index.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the index.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the index.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether the index is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Whether the index is stored in memory.
        /// </summary>
        public bool InMemory { get; set; }

        /// <summary>
        /// UTC timestamp when the index was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Labels associated with this index.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Tags associated with this index.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }
    }
}
