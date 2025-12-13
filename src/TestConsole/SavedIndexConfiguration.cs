namespace TestConsole
{
    using System;
    using Verbex;

    /// <summary>
    /// Serializable configuration for saving index metadata to disk.
    /// Used for persisting index configurations across program restarts.
    /// </summary>
    public class SavedIndexConfiguration
    {
        /// <summary>
        /// Gets or sets the description of the saved configuration.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the storage mode used by this index.
        /// </summary>
        public StorageMode StorageMode { get; set; }

        /// <summary>
        /// Gets or sets the minimum token length filter setting.
        /// </summary>
        public int MinTokenLength { get; set; }

        /// <summary>
        /// Gets or sets the maximum token length filter setting.
        /// </summary>
        public int MaxTokenLength { get; set; }

        /// <summary>
        /// Gets or sets whether this index configuration uses a lemmatizer.
        /// </summary>
        public bool HasLemmatizer { get; set; }

        /// <summary>
        /// Gets or sets whether this index configuration uses stop word removal.
        /// </summary>
        public bool HasStopWordRemover { get; set; }

        /// <summary>
        /// Gets or sets when this configuration was originally created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when this configuration was last accessed.
        /// </summary>
        public DateTime LastAccessedAt { get; set; }
    }
}
