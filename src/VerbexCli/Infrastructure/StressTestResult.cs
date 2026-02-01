namespace VerbexCli.Infrastructure
{
    /// <summary>
    /// Result of a stress test operation.
    /// </summary>
    public class StressTestResult
    {
        /// <summary>
        /// The index that was stress tested.
        /// </summary>
        public string Index { get; set; } = string.Empty;

        /// <summary>
        /// Number of documents in the stress test.
        /// </summary>
        public int Documents { get; set; }

        /// <summary>
        /// Total time for the stress test.
        /// </summary>
        public string TotalTime { get; set; } = string.Empty;

        /// <summary>
        /// Actual number of documents in the index after the test.
        /// </summary>
        public int ActualDocuments { get; set; }

        /// <summary>
        /// Number of errors encountered during the test.
        /// </summary>
        public int Errors { get; set; }

        /// <summary>
        /// Status of the test (PASSED, PASSED_WITH_ERRORS, FAILED).
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Total memory usage after the test.
        /// </summary>
        public string MemoryUsage { get; set; } = string.Empty;
    }
}
