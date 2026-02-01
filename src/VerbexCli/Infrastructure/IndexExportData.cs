namespace VerbexCli.Infrastructure
{
    using System;

    /// <summary>
    /// Data exported from an index.
    /// </summary>
    public class IndexExportData
    {
        /// <summary>
        /// Timestamp of the export.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Name of the exported index.
        /// </summary>
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// Statistics of the index at export time.
        /// </summary>
        public object? Statistics { get; set; }

        /// <summary>
        /// Configuration of the index.
        /// </summary>
        public object? Configuration { get; set; }
    }
}
