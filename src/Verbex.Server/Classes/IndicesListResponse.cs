namespace Verbex.Server.Classes
{
    using System.Collections.Generic;

    /// <summary>
    /// Response object for listing indices.
    /// </summary>
    public class IndicesListResponse
    {
        /// <summary>
        /// The list of indices.
        /// </summary>
        public List<IndexMetadataResponse> Indices { get; set; } = new List<IndexMetadataResponse>();

        /// <summary>
        /// The total count of indices.
        /// </summary>
        public int Count { get; set; }
    }
}
