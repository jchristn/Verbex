namespace Verbex.Sdk
{
    /// <summary>
    /// Standard API response wrapper for all Verbex API calls.
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// Unique request tracking identifier.
        /// </summary>
        public string? Guid { get; set; }

        /// <summary>
        /// Indicates whether the request was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// ISO 8601 timestamp of the response.
        /// </summary>
        public string? TimestampUtc { get; set; }

        /// <summary>
        /// HTTP status code of the response.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Error message if the request failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Response payload data. Structure varies by endpoint.
        /// </summary>
        public object? Data { get; set; }

        /// <summary>
        /// Total count for paginated results.
        /// </summary>
        public int? TotalCount { get; set; }

        /// <summary>
        /// Request processing time in milliseconds.
        /// </summary>
        public double? ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Typed API response with strongly-typed data.
    /// </summary>
    /// <typeparam name="T">The type of the data payload.</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// Unique request tracking identifier.
        /// </summary>
        public string? Guid { get; set; }

        /// <summary>
        /// Indicates whether the request was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// ISO 8601 timestamp of the response.
        /// </summary>
        public string? TimestampUtc { get; set; }

        /// <summary>
        /// HTTP status code of the response.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Error message if the request failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Response payload data.
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// Total count for paginated results.
        /// </summary>
        public int? TotalCount { get; set; }

        /// <summary>
        /// Request processing time in milliseconds.
        /// </summary>
        public double? ProcessingTimeMs { get; set; }
    }
}
