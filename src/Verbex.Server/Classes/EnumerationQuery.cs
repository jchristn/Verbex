namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// Query parameters for paginated enumeration of collections.
    /// </summary>
    public class EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of results to return.
        /// Must be between 1 and 1000, inclusive.
        /// Default is 100.
        /// </summary>
        public int MaxResults
        {
            get { return _MaxResults; }
            set
            {
                if (value < 1 || value > 1000)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxResults), "MaxResults must be between 1 and 1000.");
                }
                _MaxResults = value;
            }
        }

        /// <summary>
        /// Number of records to skip before returning results.
        /// Must be non-negative.
        /// Default is 0.
        /// </summary>
        public int Skip
        {
            get { return _Skip; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Skip), "Skip must be non-negative.");
                }
                _Skip = value;
            }
        }

        /// <summary>
        /// Opaque continuation token for pagination.
        /// When provided, indicates the position to continue from in a previous result set.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Ordering for the results.
        /// Default is CreatedDescending (newest first).
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        #endregion

        #region Private-Members

        private int _MaxResults = 100;
        private int _Skip = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with default values.
        /// </summary>
        public EnumerationQuery()
        {
        }

        /// <summary>
        /// Instantiate with specified parameters.
        /// </summary>
        /// <param name="maxResults">Maximum number of results (1-1000).</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="continuationToken">Optional continuation token.</param>
        /// <param name="ordering">Result ordering.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when maxResults or skip is invalid.</exception>
        public EnumerationQuery(int maxResults, int skip = 0, string? continuationToken = null, EnumerationOrderEnum ordering = EnumerationOrderEnum.CreatedDescending)
        {
            MaxResults = maxResults;
            Skip = skip;
            ContinuationToken = continuationToken;
            Ordering = ordering;
        }

        /// <summary>
        /// Parse query parameters from string values (typically from HTTP query strings).
        /// </summary>
        /// <param name="maxResults">String value for maxResults parameter.</param>
        /// <param name="skip">String value for skip parameter.</param>
        /// <param name="continuationToken">String value for continuationToken parameter.</param>
        /// <param name="ordering">String value for ordering parameter.</param>
        /// <returns>Parsed EnumerationQuery with validated values.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when parsed values are out of valid range.</exception>
        public static EnumerationQuery Parse(string? maxResults, string? skip, string? continuationToken, string? ordering)
        {
            EnumerationQuery query = new EnumerationQuery();

            // Parse MaxResults
            if (!String.IsNullOrEmpty(maxResults))
            {
                if (Int32.TryParse(maxResults, out int parsedMaxResults))
                {
                    query.MaxResults = parsedMaxResults; // Setter validates range
                }
            }

            // Parse Skip
            if (!String.IsNullOrEmpty(skip))
            {
                if (Int32.TryParse(skip, out int parsedSkip))
                {
                    query.Skip = parsedSkip; // Setter validates range
                }
            }

            // ContinuationToken is passed through as-is
            if (!String.IsNullOrEmpty(continuationToken))
            {
                query.ContinuationToken = continuationToken;
            }

            // Parse Ordering
            if (!String.IsNullOrEmpty(ordering))
            {
                if (Enum.TryParse<EnumerationOrderEnum>(ordering, ignoreCase: true, out EnumerationOrderEnum parsedOrdering))
                {
                    query.Ordering = parsedOrdering;
                }
                else if (ordering.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                         ordering.Equals("ascending", StringComparison.OrdinalIgnoreCase))
                {
                    query.Ordering = EnumerationOrderEnum.CreatedAscending;
                }
                else if (ordering.Equals("desc", StringComparison.OrdinalIgnoreCase) ||
                         ordering.Equals("descending", StringComparison.OrdinalIgnoreCase))
                {
                    query.Ordering = EnumerationOrderEnum.CreatedDescending;
                }
            }

            return query;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validates the query parameters and returns error message if invalid.
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = "";

            if (_MaxResults < 1 || _MaxResults > 1000)
            {
                errorMessage = "MaxResults must be between 1 and 1000.";
                return false;
            }

            if (_Skip < 0)
            {
                errorMessage = "Skip must be non-negative.";
                return false;
            }

            return true;
        }

        #endregion
    }
}
