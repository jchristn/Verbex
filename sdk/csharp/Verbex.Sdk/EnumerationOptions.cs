namespace Verbex.Sdk
{
    /// <summary>
    /// Options for paginated enumeration requests.
    /// </summary>
    public class EnumerationOptions
    {
        /// <summary>
        /// Maximum number of results to return per page.
        /// Must be between 1 and 1000, inclusive.
        /// Default is 100.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Number of records to skip before returning results.
        /// Default is 0.
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Opaque continuation token for pagination.
        /// Use the token from a previous result to fetch the next page.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Ordering for the results.
        /// Default is CreatedDescending (newest first).
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        /// <summary>
        /// Optional list of labels to filter by.
        /// Documents must have ALL specified labels to be included (AND logic).
        /// Label matching is case-insensitive.
        /// If null or empty, no label filtering is applied.
        /// </summary>
        public System.Collections.Generic.List<string>? Labels { get; set; } = null;

        /// <summary>
        /// Optional dictionary of tags (key-value pairs) to filter by.
        /// Documents must have ALL specified tags with matching values to be included (AND logic).
        /// Tag matching is exact (case-sensitive for both key and value).
        /// If null or empty, no tag filtering is applied.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, string>? Tags { get; set; } = null;

        /// <summary>
        /// Instantiate with default values.
        /// </summary>
        public EnumerationOptions()
        {
        }

        /// <summary>
        /// Instantiate with specified parameters.
        /// </summary>
        /// <param name="maxResults">Maximum number of results per page.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="continuationToken">Optional continuation token.</param>
        /// <param name="ordering">Result ordering.</param>
        public EnumerationOptions(int maxResults = 100, int skip = 0, string? continuationToken = null, EnumerationOrderEnum ordering = EnumerationOrderEnum.CreatedDescending)
        {
            MaxResults = maxResults;
            Skip = skip;
            ContinuationToken = continuationToken;
            Ordering = ordering;
        }

        /// <summary>
        /// Builds the query string portion for the URL.
        /// </summary>
        /// <returns>Query string without leading '?'.</returns>
        internal string ToQueryString()
        {
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();

            if (MaxResults != 100)
            {
                parts.Add($"maxResults={MaxResults}");
            }

            if (Skip > 0)
            {
                parts.Add($"skip={Skip}");
            }

            if (!string.IsNullOrEmpty(ContinuationToken))
            {
                parts.Add($"continuationToken={System.Uri.EscapeDataString(ContinuationToken)}");
            }

            if (Ordering != EnumerationOrderEnum.CreatedDescending)
            {
                parts.Add($"ordering={Ordering}");
            }

            if (Labels != null && Labels.Count > 0)
            {
                parts.Add($"labels={System.Uri.EscapeDataString(string.Join(",", Labels))}");
            }

            if (Tags != null && Tags.Count > 0)
            {
                foreach (System.Collections.Generic.KeyValuePair<string, string> tag in Tags)
                {
                    parts.Add($"tag.{System.Uri.EscapeDataString(tag.Key)}={System.Uri.EscapeDataString(tag.Value)}");
                }
            }

            return parts.Count > 0 ? string.Join("&", parts) : string.Empty;
        }
    }
}
