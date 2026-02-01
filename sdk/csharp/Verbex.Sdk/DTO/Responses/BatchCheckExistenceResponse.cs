namespace Verbex.Sdk.DTO.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a batch existence check operation.
    /// </summary>
    public class BatchCheckExistenceResponse
    {
        /// <summary>
        /// List of document IDs that exist.
        /// </summary>
        public List<string> Exists { get; set; } = new List<string>();

        /// <summary>
        /// List of document IDs that do not exist.
        /// </summary>
        public List<string> NotFound { get; set; } = new List<string>();

        /// <summary>
        /// Number of documents that exist.
        /// </summary>
        public int ExistsCount { get; set; }

        /// <summary>
        /// Number of documents that do not exist.
        /// </summary>
        public int NotFoundCount { get; set; }

        /// <summary>
        /// Total number of IDs in the request.
        /// </summary>
        public int RequestedCount { get; set; }
    }
}
