namespace Verbex.Server.Classes
{
    /// <summary>
    /// Request type enumeration.
    /// </summary>
    public enum RequestTypeEnum
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// Authentication.
        /// </summary>
        Authentication,
        /// <summary>
        /// Index management.
        /// </summary>
        IndexManagement,
        /// <summary>
        /// Search operation.
        /// </summary>
        Search,
        /// <summary>
        /// Document operations.
        /// </summary>
        Document,
        /// <summary>
        /// Health check.
        /// </summary>
        HealthCheck
    }
}