namespace Verbex.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for searching documents in an index with optional label and tag filters.
    /// </summary>
    public class SearchRequest
    {
        #region Public-Members

        /// <summary>
        /// Search query string.
        /// </summary>
        public string Query
        {
            get
            {
                return _Query;
            }
            set
            {
                _Query = value ?? "";
            }
        }

        /// <summary>
        /// Maximum number of results to return.
        /// Default value is 100.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                _MaxResults = value < 1 ? 100 : value;
            }
        }

        /// <summary>
        /// Optional list of labels to filter by.
        /// Documents must have ALL specified labels to match (AND logic).
        /// Label matching is case-insensitive.
        /// If null or empty, no label filtering is applied.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Labels
        {
            get
            {
                return _Labels;
            }
            set
            {
                _Labels = value;
            }
        }

        /// <summary>
        /// Optional dictionary of tags (key-value pairs) to filter by.
        /// Documents must have ALL specified tags with matching values to match (AND logic).
        /// Tag matching is exact (case-sensitive for both key and value).
        /// If null or empty, no tag filtering is applied.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                _Tags = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Query = "";
        private int _MaxResults = 100;
        private List<string>? _Labels = null;
        private Dictionary<string, object>? _Tags = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty SearchRequest.
        /// </summary>
        public SearchRequest()
        {
        }

        /// <summary>
        /// Instantiate a SearchRequest with query parameters.
        /// </summary>
        /// <param name="query">Search query string.</param>
        /// <param name="maxResults">Maximum number of results to return. Default is 100.</param>
        /// <param name="labels">Optional labels to filter by (AND logic, case-insensitive).</param>
        /// <param name="tags">Optional tags to filter by (AND logic, exact match).</param>
        public SearchRequest(string query, int maxResults = 100, List<string>? labels = null, Dictionary<string, object>? tags = null)
        {
            Query = query;
            MaxResults = maxResults;
            Labels = labels;
            Tags = tags;
        }

        #endregion
    }
}
