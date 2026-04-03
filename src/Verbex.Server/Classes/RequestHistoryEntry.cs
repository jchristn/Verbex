namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// Request history entry metadata.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// Entry identifier.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// User identifier.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Credential identifier.
        /// </summary>
        public string? CredentialId { get; set; }

        /// <summary>
        /// Principal type.
        /// </summary>
        public string? PrincipalType { get; set; }

        /// <summary>
        /// Principal display name.
        /// </summary>
        public string? PrincipalName { get; set; }

        /// <summary>
        /// Request classification.
        /// </summary>
        public string RequestType { get; set; } = RequestTypeEnum.Unknown.ToString();

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string HttpMethod { get; set; } = "GET";

        /// <summary>
        /// Route template or normalized path.
        /// </summary>
        public string RouteTemplate { get; set; } = "/";

        /// <summary>
        /// Full request URL.
        /// </summary>
        public string RequestUrl { get; set; } = "/";

        /// <summary>
        /// Source IP.
        /// </summary>
        public string? SourceIp { get; set; }

        /// <summary>
        /// Index identifier if present.
        /// </summary>
        public string? IndexId { get; set; }

        /// <summary>
        /// Document identifier if present.
        /// </summary>
        public string? DocumentId { get; set; }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Success flag.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Request content type.
        /// </summary>
        public string? RequestContentType { get; set; }

        /// <summary>
        /// Response content type.
        /// </summary>
        public string? ResponseContentType { get; set; }

        /// <summary>
        /// Request size in bytes.
        /// </summary>
        public long RequestSizeBytes { get; set; }

        /// <summary>
        /// Response size in bytes.
        /// </summary>
        public long ResponseSizeBytes { get; set; }

        /// <summary>
        /// Indicates whether the stored request body is truncated.
        /// </summary>
        public bool RequestBodyTruncated { get; set; }

        /// <summary>
        /// Indicates whether the stored response body is truncated.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; }

        /// <summary>
        /// Indicates whether the response is binary.
        /// </summary>
        public bool IsBinaryResponse { get; set; }

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }
}
