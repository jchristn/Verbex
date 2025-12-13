namespace Verbex.Sdk
{
    /// <summary>
    /// Create index response data.
    /// </summary>
    public class CreateIndexData
    {
        /// <summary>
        /// Success message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// The created index information.
        /// </summary>
        public IndexInfo? Index { get; set; }
    }
}
