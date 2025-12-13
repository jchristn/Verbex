namespace VerbexCli.Infrastructure
{
    using System.Collections.Generic;

    /// <summary>
    /// Persisted CLI configuration containing all indices.
    /// </summary>
    internal class PersistedConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the current active index.
        /// </summary>
        public string? CurrentIndex { get; set; }

        /// <summary>
        /// Gets or sets the list of index configurations.
        /// </summary>
        public List<SerializableIndexConfiguration> Indices { get; set; } = new List<SerializableIndexConfiguration>();
    }
}
