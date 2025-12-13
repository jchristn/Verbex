namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response context.
    /// </summary>
    public class ResponseContext
    {
        #region Public-Members

        /// <summary>
        /// GUID.
        /// </summary>
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();

        /// <summary>
        /// Success.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Timestamp in UTC.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Error message.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Response data.
        /// </summary>
        public object? Data { get; set; } = null;

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Total count for pagination.
        /// </summary>
        public int? TotalCount { get; set; } = null;

        /// <summary>
        /// Skip count for pagination.
        /// </summary>
        public int? Skip { get; set; } = null;

        /// <summary>
        /// Processing time in milliseconds.
        /// </summary>
        public double ProcessingTimeMs { get; set; } = 0;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ResponseContext()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="success">Success.</param>
        /// <param name="statusCode">Status code.</param>
        /// <param name="errorMessage">Error message.</param>
        public ResponseContext(bool success, int statusCode, string? errorMessage = null)
        {
            Success = success;
            StatusCode = statusCode;
            ErrorMessage = errorMessage;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}