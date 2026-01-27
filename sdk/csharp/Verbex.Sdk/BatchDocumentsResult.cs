namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a batch document retrieval operation.
    /// </summary>
    public class BatchDocumentsResult
    {
        /// <summary>
        /// List of documents that were found.
        /// </summary>
        public List<DocumentInfo> Documents { get; set; } = new List<DocumentInfo>();

        /// <summary>
        /// List of document IDs that were not found.
        /// </summary>
        public List<string> NotFound { get; set; } = new List<string>();

        /// <summary>
        /// Number of documents returned.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Total number of document IDs that were requested.
        /// </summary>
        public int RequestedCount { get; set; }
    }
}
