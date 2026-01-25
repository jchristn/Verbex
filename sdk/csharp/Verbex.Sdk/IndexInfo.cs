namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Index information model.
    /// Matches the server's index metadata serialization format.
    /// </summary>
    public class IndexInfo
    {
        /// <summary>
        /// Unique identifier for the index (auto-generated).
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier the index belongs to.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Display name for the index.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Description of the index.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether the index is enabled.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Whether the index uses in-memory storage only.
        /// </summary>
        public bool? InMemory { get; set; }

        /// <summary>
        /// UTC timestamp when the index was created.
        /// </summary>
        public string? CreatedUtc { get; set; }

        /// <summary>
        /// Index statistics (document count, term count, etc.).
        /// </summary>
        public IndexStatistics? Statistics { get; set; }

        /// <summary>
        /// Labels associated with the index.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Tags (key-value pairs) associated with the index.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }

        /// <summary>
        /// Custom metadata object associated with the index.
        /// Can be any JSON-serializable object.
        /// </summary>
        public object? CustomMetadata { get; set; }
    }
}
