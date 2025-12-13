namespace Verbex.Sdk
{
    using System;

    /// <summary>
    /// Exception thrown for Verbex API errors.
    /// </summary>
    public class VerbexException : Exception
    {
        /// <summary>
        /// HTTP status code from the API response.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// The API response associated with this error.
        /// </summary>
        public ApiResponse? Response { get; }

        /// <summary>
        /// Creates a new VerbexException.
        /// </summary>
        /// <param name="message">The error message.</param>
        public VerbexException(string message) : base(message)
        {
            StatusCode = 0;
            Response = null;
        }

        /// <summary>
        /// Creates a new VerbexException with status code.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        public VerbexException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
            Response = null;
        }

        /// <summary>
        /// Creates a new VerbexException with status code and response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="response">The API response.</param>
        public VerbexException(string message, int statusCode, ApiResponse? response) : base(message)
        {
            StatusCode = statusCode;
            Response = response;
        }

        /// <summary>
        /// Creates a new VerbexException with an inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public VerbexException(string message, Exception innerException) : base(message, innerException)
        {
            StatusCode = 0;
            Response = null;
        }
    }
}
