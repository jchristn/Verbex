namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents the result of a restore operation.
    /// </summary>
    public class RestoreResult
    {
        #region Private-Members

        private bool _Success = false;
        private string _IndexId = string.Empty;
        private string _Message = string.Empty;
        private List<string> _Warnings = new List<string>();

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets whether the restore operation was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success
        {
            get => _Success;
            set => _Success = value;
        }

        /// <summary>
        /// Gets or sets the identifier of the restored index.
        /// </summary>
        [JsonPropertyName("indexId")]
        public string IndexId
        {
            get => _IndexId;
            set => _IndexId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets a message describing the result.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message
        {
            get => _Message;
            set => _Message = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets any warnings that occurred during restore.
        /// </summary>
        [JsonPropertyName("warnings")]
        public List<string> Warnings
        {
            get => _Warnings;
            set => _Warnings = value ?? new List<string>();
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the RestoreResult class.
        /// </summary>
        public RestoreResult()
        {
        }

        /// <summary>
        /// Creates a successful restore result.
        /// </summary>
        /// <param name="indexId">The restored index identifier.</param>
        /// <param name="message">Success message.</param>
        /// <returns>A successful RestoreResult instance.</returns>
        public static RestoreResult Successful(string indexId, string message = "Index restored successfully")
        {
            return new RestoreResult
            {
                Success = true,
                IndexId = indexId,
                Message = message
            };
        }

        /// <summary>
        /// Creates a failed restore result.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <returns>A failed RestoreResult instance.</returns>
        public static RestoreResult Failed(string message)
        {
            return new RestoreResult
            {
                Success = false,
                Message = message
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Adds a warning message to the result.
        /// </summary>
        /// <param name="warning">The warning message.</param>
        public void AddWarning(string warning)
        {
            if (!String.IsNullOrEmpty(warning))
            {
                _Warnings.Add(warning);
            }
        }

        #endregion
    }
}
