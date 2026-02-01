namespace Verbex.Server.Classes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents the manifest file included in backup archives.
    /// Contains version information, checksums, and statistics for validation.
    /// </summary>
    public class BackupManifest
    {
        #region Private-Members

        private string _Version = "1.0";
        private DateTime _BackupTimestamp = DateTime.UtcNow;
        private string _VerbexVersion = "0.1.8";
        private string _SchemaVersion = "3.0";
        private string _IndexId = string.Empty;
        private string _TenantId = string.Empty;
        private BackupChecksum? _Checksum = null;
        private BackupStatistics? _Statistics = null;

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets the backup format version.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version
        {
            get => _Version;
            set => _Version = value ?? "1.0";
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the backup was created.
        /// </summary>
        [JsonPropertyName("backupTimestamp")]
        public DateTime BackupTimestamp
        {
            get => _BackupTimestamp;
            set => _BackupTimestamp = value;
        }

        /// <summary>
        /// Gets or sets the Verbex server version that created the backup.
        /// </summary>
        [JsonPropertyName("verbexVersion")]
        public string VerbexVersion
        {
            get => _VerbexVersion;
            set => _VerbexVersion = value ?? "0.1.8";
        }

        /// <summary>
        /// Gets or sets the database schema version of the backed up index.
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion
        {
            get => _SchemaVersion;
            set => _SchemaVersion = value ?? "3.0";
        }

        /// <summary>
        /// Gets or sets the identifier of the backed up index.
        /// </summary>
        [JsonPropertyName("indexId")]
        public string IndexId
        {
            get => _IndexId;
            set => _IndexId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tenant ID that owns the backed up index.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the checksum information for the backup.
        /// </summary>
        [JsonPropertyName("checksum")]
        public BackupChecksum? Checksum
        {
            get => _Checksum;
            set => _Checksum = value;
        }

        /// <summary>
        /// Gets or sets the index statistics at backup time.
        /// </summary>
        [JsonPropertyName("statistics")]
        public BackupStatistics? Statistics
        {
            get => _Statistics;
            set => _Statistics = value;
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the BackupManifest class.
        /// </summary>
        public BackupManifest()
        {
            _BackupTimestamp = DateTime.UtcNow;
        }

        #endregion
    }

    /// <summary>
    /// Represents checksum information for backup validation.
    /// </summary>
    public class BackupChecksum
    {
        #region Private-Members

        private string _Algorithm = "SHA256";
        private string _IndexDb = string.Empty;

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets the hashing algorithm used.
        /// </summary>
        [JsonPropertyName("algorithm")]
        public string Algorithm
        {
            get => _Algorithm;
            set => _Algorithm = value ?? "SHA256";
        }

        /// <summary>
        /// Gets or sets the hash of the index.db file.
        /// </summary>
        [JsonPropertyName("indexDb")]
        public string IndexDb
        {
            get => _IndexDb;
            set => _IndexDb = value ?? string.Empty;
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the BackupChecksum class.
        /// </summary>
        public BackupChecksum()
        {
        }

        /// <summary>
        /// Initializes a new instance of the BackupChecksum class with a hash value.
        /// </summary>
        /// <param name="indexDbHash">The SHA256 hash of the index.db file.</param>
        public BackupChecksum(string indexDbHash)
        {
            _IndexDb = indexDbHash ?? string.Empty;
        }

        #endregion
    }
}
