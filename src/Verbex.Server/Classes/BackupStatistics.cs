namespace Verbex.Server.Classes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents index statistics at the time of backup creation.
    /// </summary>
    public class BackupStatistics
    {
        #region Private-Members

        private long _DocumentCount = 0;
        private long _TermCount = 0;
        private long _TotalSize = 0;

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets the total number of documents in the index.
        /// </summary>
        [JsonPropertyName("documentCount")]
        public long DocumentCount
        {
            get => _DocumentCount;
            set => _DocumentCount = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets the total number of unique terms in the index.
        /// </summary>
        [JsonPropertyName("termCount")]
        public long TermCount
        {
            get => _TermCount;
            set => _TermCount = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets the total size of the index in bytes.
        /// </summary>
        [JsonPropertyName("totalSize")]
        public long TotalSize
        {
            get => _TotalSize;
            set => _TotalSize = value < 0 ? 0 : value;
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the BackupStatistics class.
        /// </summary>
        public BackupStatistics()
        {
        }

        /// <summary>
        /// Initializes a new instance of the BackupStatistics class with values.
        /// </summary>
        /// <param name="documentCount">Number of documents.</param>
        /// <param name="termCount">Number of unique terms.</param>
        /// <param name="totalSize">Total size in bytes.</param>
        public BackupStatistics(long documentCount, long termCount, long totalSize)
        {
            _DocumentCount = documentCount < 0 ? 0 : documentCount;
            _TermCount = termCount < 0 ? 0 : termCount;
            _TotalSize = totalSize < 0 ? 0 : totalSize;
        }

        #endregion
    }
}
