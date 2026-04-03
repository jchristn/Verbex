namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// Aggregated request history bucket.
    /// </summary>
    public class RequestHistorySummaryBucket
    {
        /// <summary>
        /// Bucket start.
        /// </summary>
        public DateTime BucketStartUtc { get; set; }

        /// <summary>
        /// Bucket end.
        /// </summary>
        public DateTime BucketEndUtc { get; set; }

        /// <summary>
        /// Total requests in the bucket.
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
