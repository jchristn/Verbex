namespace Verbex
{
    /// <summary>
    /// Memory usage statistics
    /// </summary>
    public class MemoryStatistics
    {
        /// <summary>
        /// Gets the estimated memory usage of the main index in bytes
        /// </summary>
        public long IndexMemoryBytes { get; init; }

        /// <summary>
        /// Gets the estimated memory usage of the document store in bytes
        /// </summary>
        public long DocumentStoreMemoryBytes { get; init; }

        /// <summary>
        /// Gets the estimated memory usage of caches in bytes
        /// </summary>
        public long CacheMemoryBytes { get; init; }

        /// <summary>
        /// Gets the estimated total memory usage in bytes
        /// </summary>
        public long TotalMemoryBytes { get; init; }

        /// <summary>
        /// Initializes a new instance of the MemoryStatistics class
        /// </summary>
        /// <param name="indexMemoryBytes">Index memory usage</param>
        /// <param name="documentStoreMemoryBytes">Document store memory usage</param>
        /// <param name="cacheMemoryBytes">Cache memory usage</param>
        public MemoryStatistics(long indexMemoryBytes, long documentStoreMemoryBytes, long cacheMemoryBytes)
        {
            IndexMemoryBytes = indexMemoryBytes;
            DocumentStoreMemoryBytes = documentStoreMemoryBytes;
            CacheMemoryBytes = cacheMemoryBytes;
            TotalMemoryBytes = indexMemoryBytes + documentStoreMemoryBytes + cacheMemoryBytes;
        }
    }
}
