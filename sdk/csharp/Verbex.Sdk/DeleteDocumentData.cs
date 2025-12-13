namespace Verbex.Sdk
{
    /// <summary>
    /// Delete document response data.
    /// </summary>
    public class DeleteDocumentData
    {
        /// <summary>
        /// The deleted document identifier.
        /// </summary>
        public string? DocumentId { get; set; }

        /// <summary>
        /// Success message.
        /// </summary>
        public string? Message { get; set; }
    }
}
