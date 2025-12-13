namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// List indices response data.
    /// </summary>
    public class IndicesListData
    {
        /// <summary>
        /// List of available indices.
        /// </summary>
        public List<IndexInfo> Indices { get; set; } = new List<IndexInfo>();

        /// <summary>
        /// Total count of indices.
        /// </summary>
        public int Count { get; set; }
    }
}
