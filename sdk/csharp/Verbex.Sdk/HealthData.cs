namespace Verbex.Sdk
{
    /// <summary>
    /// Health check response data.
    /// </summary>
    public class HealthData
    {
        /// <summary>
        /// Server health status (e.g., "Healthy").
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Server version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Server timestamp.
        /// </summary>
        public string? Timestamp { get; set; }
    }
}
