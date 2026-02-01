namespace Verbex.Sdk.DTO.Responses
{
    /// <summary>
    /// Result item for a single document in a batch add operation.
    /// </summary>
    public class BatchAddDocumentResult
    {
        /// <summary>
        /// Document ID (populated for successful adds).
        /// </summary>
        public string? DocumentId { get; set; }

        /// <summary>
        /// Document name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether the document was added successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the add failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
