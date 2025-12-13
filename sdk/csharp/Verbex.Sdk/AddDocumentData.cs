namespace Verbex.Sdk
{
    /// <summary>
    /// Add document response data.
    /// </summary>
    public class AddDocumentData
    {
        /// <summary>
        /// The created document identifier.
        /// </summary>
        public string? DocumentId { get; set; }

        /// <summary>
        /// Success message.
        /// </summary>
        public string? Message { get; set; }
    }
}
