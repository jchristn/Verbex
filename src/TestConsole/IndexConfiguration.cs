namespace TestConsole
{
    using System;
    using Verbex;

    /// <summary>
    /// Represents a complete index configuration including metadata and Verbex settings.
    /// </summary>
    public class IndexConfiguration
    {
        /// <summary>
        /// Gets or sets the unique name of this index configuration.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the human-readable description of this configuration.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the Verbex configuration settings.
        /// </summary>
        public VerbexConfiguration VerbexConfig { get; set; } = new VerbexConfiguration();

        /// <summary>
        /// Gets or sets the tokenizer for this index.
        /// </summary>
        public ITokenizer? Tokenizer { get; set; }

        /// <summary>
        /// Gets or sets whether this index is persistent (saved to disk).
        /// </summary>
        public bool IsPersistent { get; set; } = false;

        /// <summary>
        /// Gets or sets when this configuration was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when this configuration was last accessed.
        /// </summary>
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    }
}
