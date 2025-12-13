namespace VerbexCli.Infrastructure
{
    using System;

    /// <summary>
    /// Serializable version of IndexConfiguration for JSON persistence.
    /// </summary>
    internal class SerializableIndexConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the index.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the description of the index.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the serializable Verbex configuration.
        /// </summary>
        public SerializableVerbexConfiguration VerbexConfig { get; set; } = new SerializableVerbexConfiguration();

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the last accessed timestamp.
        /// </summary>
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    }
}
