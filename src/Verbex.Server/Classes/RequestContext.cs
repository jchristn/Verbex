namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    /// <summary>
    /// Request context.
    /// </summary>
    public class RequestContext
    {
        #region Public-Members

        /// <summary>
        /// GUID.
        /// </summary>
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();

        /// <summary>
        /// API version.
        /// </summary>
        public string ApiVersion { get; set; } = "1.0";

        /// <summary>
        /// Request type.
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        /// <summary>
        /// IP address.
        /// </summary>
        public string? IpAddress { get; set; } = null;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string? Method { get; set; } = null;

        /// <summary>
        /// URL.
        /// </summary>
        public string? Url { get; set; } = null;

        /// <summary>
        /// Query parameters.
        /// </summary>
        public NameValueCollection QueryParams { get; set; } = new NameValueCollection();

        /// <summary>
        /// Authentication token.
        /// </summary>
        public string? AuthToken { get; set; } = null;

        /// <summary>
        /// Skip count for pagination.
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Maximum keys to return.
        /// </summary>
        public int MaxKeys { get; set; } = 1000;

        /// <summary>
        /// Order for results.
        /// </summary>
        public string? Order { get; set; } = null;

        /// <summary>
        /// Force operation.
        /// </summary>
        public bool Force { get; set; } = false;

        /// <summary>
        /// Include data in response.
        /// </summary>
        public bool IncludeData { get; set; } = false;

        /// <summary>
        /// Request headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestContext()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}