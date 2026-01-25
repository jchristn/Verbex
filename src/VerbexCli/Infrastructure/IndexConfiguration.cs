namespace VerbexCli.Infrastructure
{
    using System;
    using Verbex;

    /// <summary>
    /// Configuration for an index
    /// </summary>
    public class IndexConfiguration
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
        /// Gets or sets the Verbex configuration for the index.
        /// </summary>
        public VerbexConfiguration VerbexConfig { get; set; } = new VerbexConfiguration();

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the last accessed timestamp.
        /// </summary>
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the custom metadata for the index.
        /// Can be any JSON-serializable value (object, array, string, number, boolean, null).
        /// </summary>
        public object? CustomMetadata { get; set; } = null;
    }
}
