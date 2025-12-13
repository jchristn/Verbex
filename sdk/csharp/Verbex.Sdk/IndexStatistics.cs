namespace Verbex.Sdk
{
    /// <summary>
    /// Index statistics model.
    /// </summary>
    public class IndexStatistics
    {
        /// <summary>
        /// Number of documents in the index.
        /// </summary>
        public int DocumentCount { get; set; }

        /// <summary>
        /// Number of unique terms in the index.
        /// </summary>
        public int TermCount { get; set; }

        /// <summary>
        /// Memory usage in megabytes.
        /// </summary>
        public double MemoryUsageMb { get; set; }
    }
}
