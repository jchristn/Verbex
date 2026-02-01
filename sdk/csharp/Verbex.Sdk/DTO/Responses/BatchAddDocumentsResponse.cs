namespace Verbex.Sdk.DTO.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a batch add documents operation.
    /// </summary>
    public class BatchAddDocumentsResponse
    {
        /// <summary>
        /// List of successfully added documents.
        /// </summary>
        public List<BatchAddDocumentResult> Added { get; set; } = new List<BatchAddDocumentResult>();

        /// <summary>
        /// List of documents that failed to be added.
        /// </summary>
        public List<BatchAddDocumentResult> Failed { get; set; } = new List<BatchAddDocumentResult>();

        /// <summary>
        /// Number of documents successfully added.
        /// </summary>
        public int AddedCount { get; set; }

        /// <summary>
        /// Number of documents that failed to be added.
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Total number of documents in the request.
        /// </summary>
        public int RequestedCount { get; set; }
    }
}
