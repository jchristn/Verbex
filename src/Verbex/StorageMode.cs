namespace Verbex
{
    /// <summary>
    /// Defines the storage mode for the inverted index.
    /// </summary>
    public enum StorageMode
    {
        /// <summary>
        /// Index is stored in an in-memory SQLite database.
        /// Fast performance but data is lost when application terminates unless explicitly flushed.
        /// </summary>
        InMemory,

        /// <summary>
        /// Index is stored in a file-based SQLite database.
        /// Data is persisted immediately to disk for maximum durability.
        /// </summary>
        OnDisk
    }
}
