namespace VerbexCli.Infrastructure
{
    using Verbex;

    /// <summary>
    /// Serializable version of VerbexConfiguration for JSON persistence.
    /// </summary>
    internal class SerializableVerbexConfiguration
    {
        /// <summary>
        /// Gets or sets the storage mode.
        /// </summary>
        public StorageMode StorageMode { get; set; }

        /// <summary>
        /// Gets or sets the minimum token length.
        /// </summary>
        public int MinTokenLength { get; set; }

        /// <summary>
        /// Gets or sets the maximum token length.
        /// </summary>
        public int MaxTokenLength { get; set; }

        /// <summary>
        /// Gets or sets whether a lemmatizer is configured.
        /// </summary>
        public bool HasLemmatizer { get; set; }

        /// <summary>
        /// Gets or sets whether a stop word remover is configured.
        /// </summary>
        public bool HasStopWordRemover { get; set; }

        /// <summary>
        /// Gets or sets the storage directory path.
        /// </summary>
        public string? StorageDirectory { get; set; }
    }
}
