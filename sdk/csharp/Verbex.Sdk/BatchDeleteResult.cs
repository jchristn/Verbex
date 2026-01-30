namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a batch document deletion operation.
    /// Contains lists of successfully deleted and not found document IDs.
    /// </summary>
    public class BatchDeleteResult
    {
        /// <summary>
        /// List of document IDs that were successfully deleted.
        /// </summary>
        public List<string> Deleted { get; set; } = new List<string>();

        /// <summary>
        /// List of document IDs that were not found in the index.
        /// </summary>
        public List<string> NotFound { get; set; } = new List<string>();

        /// <summary>
        /// Number of documents that were successfully deleted.
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        /// Number of document IDs that were not found.
        /// </summary>
        public int NotFoundCount { get; set; }

        /// <summary>
        /// Total number of document IDs that were requested for deletion.
        /// </summary>
        public int RequestedCount { get; set; }
    }
}
