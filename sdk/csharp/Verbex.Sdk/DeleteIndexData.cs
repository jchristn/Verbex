namespace Verbex.Sdk
{
    /// <summary>
    /// Delete index response data.
    /// </summary>
    public class DeleteIndexData
    {
        /// <summary>
        /// Success message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// The deleted index identifier.
        /// </summary>
        public string? IndexId { get; set; }
    }
}
