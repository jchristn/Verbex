namespace Verbex.Server.Classes
{
    using System.Collections.Generic;

    /// <summary>
    /// Request history summary response.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>
        /// Time buckets.
        /// </summary>
        public List<RequestHistorySummaryBucket> Buckets { get; set; } = new List<RequestHistorySummaryBucket>();

        /// <summary>
        /// Total requests.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Successful requests.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Failed requests.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Average duration.
        /// </summary>
        public double AverageDurationMs { get; set; }
    }
}
