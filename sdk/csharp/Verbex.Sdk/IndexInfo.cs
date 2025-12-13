namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Index information model.
    /// Matches the server's index statistics serialization format.
    /// </summary>
    public class IndexInfo
    {
        /// <summary>
        /// Unique identifier for the index.
        /// </summary>
        public string Id { get; set; } = string.Empty;

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
    }
}
