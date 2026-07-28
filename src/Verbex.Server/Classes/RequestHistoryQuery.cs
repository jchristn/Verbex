namespace Verbex.Server.Classes
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Request history search and summary query parameters.
    /// </summary>
    public class RequestHistoryQuery
    {
        /// <summary>
        /// Page number.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 25;

        /// <summary>
        /// HTTP method filter.
        /// </summary>
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Route filter.
        /// </summary>
        public string? Route { get; set; }

        /// <summary>
        /// Tenant filter.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// User filter.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Credential filter.
        /// </summary>
        public string? CredentialId { get; set; }

        /// <summary>
        /// Index filter.
        /// </summary>
        public string? IndexId { get; set; }

        /// <summary>
        /// Source IP filter.
        /// </summary>
        public string? SourceIp { get; set; }

        /// <summary>
        /// Principal display filter.
        /// </summary>
        public string? Principal { get; set; }

        /// <summary>
        /// Optional status code filter.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Optional success flag filter.
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Optional range start.
        /// </summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>
        /// Optional range end.
        /// </summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>
        /// Bucket width in minutes for summary aggregation.
        /// </summary>
        public int BucketMinutes { get; set; } = 60;

        /// <summary>
        /// Query offset.
        /// </summary>
        public int Skip => (Page - 1) * PageSize;

        /// <summary>
        /// Parse request history query parameters.
        /// </summary>
        public static RequestHistoryQuery Parse(
            string? page,
            string? pageSize,
            string? httpMethod,
            string? route,
            string? tenantId,
            string? userId,
            string? credentialId,
            string? indexId,
            string? sourceIp,
            string? principal,
            string? statusCode,
            string? success,
            string? fromUtc,
            string? toUtc,
            string? bucketMinutes)
        {
            RequestHistoryQuery query = new RequestHistoryQuery
            {
                HttpMethod = Normalize(httpMethod),
                Route = Normalize(route),
                TenantId = Normalize(tenantId),
                UserId = Normalize(userId),
                CredentialId = Normalize(credentialId),
                IndexId = Normalize(indexId),
                SourceIp = Normalize(sourceIp),
                Principal = Normalize(principal)
            };

            if (Int32.TryParse(page, out int parsedPage) && parsedPage > 0)
            {
                query.Page = parsedPage;
            }

            if (Int32.TryParse(pageSize, out int parsedPageSize) && parsedPageSize > 0)
            {
                query.PageSize = Math.Min(parsedPageSize, 250);
            }

            if (Int32.TryParse(statusCode, out int parsedStatusCode))
            {
                query.StatusCode = parsedStatusCode;
            }

            if (Boolean.TryParse(success, out bool parsedSuccess))
            {
                query.Success = parsedSuccess;
            }
            else if (String.Equals(success, "1", StringComparison.OrdinalIgnoreCase))
            {
                query.Success = true;
            }
            else if (String.Equals(success, "0", StringComparison.OrdinalIgnoreCase))
            {
                query.Success = false;
            }

            if (DateTime.TryParse(
                fromUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime parsedFromUtc))
            {
                query.FromUtc = parsedFromUtc.ToUniversalTime();
            }

            if (DateTime.TryParse(
                toUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime parsedToUtc))
            {
                query.ToUtc = parsedToUtc.ToUniversalTime();
            }

            if (Int32.TryParse(bucketMinutes, out int parsedBucketMinutes) && parsedBucketMinutes > 0)
            {
                query.BucketMinutes = Math.Min(parsedBucketMinutes, 1440);
            }

            return query;
        }

        private static string? Normalize(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
