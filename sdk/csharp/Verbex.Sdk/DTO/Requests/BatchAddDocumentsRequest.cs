namespace Verbex.Sdk.DTO.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// Request body for batch add documents operation.
    /// </summary>
    public class BatchAddDocumentsRequest
    {
        /// <summary>
        /// List of documents to add.
        /// </summary>
        public List<BatchAddDocumentItem> Documents { get; set; } = new List<BatchAddDocumentItem>();

        /// <summary>
        /// Creates a new batch add documents request.
        /// </summary>
        public BatchAddDocumentsRequest()
        {
        }

        /// <summary>
        /// Creates a new batch add documents request with the specified documents.
        /// </summary>
        /// <param name="documents">List of documents to add.</param>
        public BatchAddDocumentsRequest(IEnumerable<BatchAddDocumentItem> documents)
        {
            Documents = new List<BatchAddDocumentItem>(documents);
        }
    }
}
