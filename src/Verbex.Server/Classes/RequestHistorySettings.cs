namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// Request history settings.
    /// </summary>
    public class RequestHistorySettings
    {
        private string _Directory = "./request-history";
        private int _RetentionDays = 30;
        private int _CleanupIntervalMinutes = 60;
        private int _MaxRequestBodyBytes = 131072;
        private int _MaxResponseBodyBytes = 131072;

        /// <summary>
        /// Enable request history persistence.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Legacy no-op setting retained for backward compatibility with older configurations.
        /// </summary>
        public string Directory
        {
            get => _Directory;
            set => _Directory = String.IsNullOrWhiteSpace(value) ? "./request-history" : value;
        }

        /// <summary>
        /// Retention in days.
        /// </summary>
        public int RetentionDays
        {
            get => _RetentionDays;
            set => _RetentionDays = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Cleanup interval in minutes.
        /// </summary>
        public int CleanupIntervalMinutes
        {
            get => _CleanupIntervalMinutes;
            set => _CleanupIntervalMinutes = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Maximum request body bytes to persist.
        /// </summary>
        public int MaxRequestBodyBytes
        {
            get => _MaxRequestBodyBytes;
            set => _MaxRequestBodyBytes = value < 1024 ? 1024 : value;
        }

        /// <summary>
        /// Maximum response body bytes to persist.
        /// </summary>
        public int MaxResponseBodyBytes
        {
            get => _MaxResponseBodyBytes;
            set => _MaxResponseBodyBytes = value < 1024 ? 1024 : value;
        }
    }
}
