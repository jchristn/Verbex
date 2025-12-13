namespace Verbex.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// List documents response data.
    /// </summary>
    public class DocumentsListData
    {
        /// <summary>
        /// List of documents in the index.
        /// </summary>
        public List<DocumentInfo> Documents { get; set; } = new List<DocumentInfo>();

        /// <summary>
        /// Total count of documents.
        /// </summary>
        public int Count { get; set; }
    }
}
