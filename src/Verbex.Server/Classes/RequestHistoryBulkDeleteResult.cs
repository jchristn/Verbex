namespace Verbex.Server.Classes
{
    using System.Collections.Generic;

    /// <summary>
    /// Request history bulk delete result.
    /// </summary>
    public class RequestHistoryBulkDeleteResult
    {
        /// <summary>
        /// Deleted entries.
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        /// Deleted identifiers.
        /// </summary>
        public List<string> DeletedIds { get; set; } = new List<string>();
    }
}
