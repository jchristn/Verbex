namespace VerbexCli.Infrastructure
{
    /// <summary>
    /// Result of a benchmark operation.
    /// </summary>
    public class BenchmarkResult
    {
        /// <summary>
        /// The index that was benchmarked.
        /// </summary>
        public string Index { get; set; } = string.Empty;

        /// <summary>
        /// Number of documents used in the benchmark.
        /// </summary>
        public int Documents { get; set; }

        /// <summary>
        /// Time taken for indexing operations.
        /// </summary>
        public string IndexingTime { get; set; } = string.Empty;

        /// <summary>
        /// Average time for search operations.
        /// </summary>
        public string SearchTime { get; set; } = string.Empty;

        /// <summary>
        /// Documents indexed per second.
        /// </summary>
        public double ThroughputDocsPerSecond { get; set; }

        /// <summary>
        /// Total time for the benchmark.
        /// </summary>
        public string TotalTime { get; set; } = string.Empty;
    }
}
