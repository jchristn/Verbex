namespace Verbex.Sdk.DTO.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// Request body for batch document existence check operation.
    /// </summary>
    public class BatchCheckExistenceRequest
    {
        /// <summary>
        /// List of document IDs to check for existence.
        /// </summary>
        public List<string> Ids { get; set; } = new List<string>();

        /// <summary>
        /// Creates a new batch check existence request.
        /// </summary>
        public BatchCheckExistenceRequest()
        {
        }

        /// <summary>
        /// Creates a new batch check existence request with the specified IDs.
        /// </summary>
        /// <param name="ids">List of document IDs to check.</param>
        public BatchCheckExistenceRequest(IEnumerable<string> ids)
        {
            Ids = new List<string>(ids);
        }
    }
}
