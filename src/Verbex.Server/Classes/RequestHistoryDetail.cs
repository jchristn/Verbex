namespace Verbex.Server.Classes
{
    using System.Collections.Generic;

    /// <summary>
    /// Request history detail payload.
    /// </summary>
    public class RequestHistoryDetail
    {
        /// <summary>
        /// Entry identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Route parameters.
        /// </summary>
        public Dictionary<string, string> RouteParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Query parameters.
        /// </summary>
        public Dictionary<string, string> QueryParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Request headers.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Stored request body.
        /// </summary>
        public string? RequestBody { get; set; }

        /// <summary>
        /// Request body byte count.
        /// </summary>
        public long RequestBodyBytes { get; set; }

        /// <summary>
        /// Request body truncation indicator.
        /// </summary>
        public bool RequestBodyTruncated { get; set; }

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Stored response body.
        /// </summary>
        public string? ResponseBody { get; set; }

        /// <summary>
        /// Response body byte count.
        /// </summary>
        public long ResponseBodyBytes { get; set; }

        /// <summary>
        /// Response body truncation indicator.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; }

        /// <summary>
        /// Optional binary filename.
        /// </summary>
        public string? ResponseFilename { get; set; }

        /// <summary>
        /// Optional notes about capture.
        /// </summary>
        public string? Notes { get; set; }
    }
}
