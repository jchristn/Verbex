namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;
    using Timestamps;

    /// <summary>
    /// Result container for paginated enumeration of collections.
    /// </summary>
    /// <typeparam name="T">The type of objects in the collection.</typeparam>
    public class EnumerationResult<T>
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether the request was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Timestamp of when the result was generated.
        /// </summary>
        public Timestamp Timestamp { get; set; }

        /// <summary>
        /// Maximum number of results requested.
        /// Echoed from the query.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Number of records skipped.
        /// Echoed from the query.
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Number of iterations required to produce this result.
        /// Typically 1 for simple queries.
        /// </summary>
        public int IterationsRequired { get; set; } = 1;

        /// <summary>
        /// Opaque continuation token for fetching the next page.
        /// Null when EndOfResults is true.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Indicates whether there are no more records to fetch.
        /// True when this is the last page of results.
        /// </summary>
        public bool EndOfResults { get; set; } = false;

        /// <summary>
        /// Total number of records in the collection before pagination.
        /// </summary>
        public long TotalRecords { get; set; }

        /// <summary>
        /// Number of records remaining after this page.
        /// </summary>
        public long RecordsRemaining { get; set; }

        /// <summary>
        /// The paginated items for this result page.
        /// </summary>
        public List<T> Objects { get; set; } = new List<T>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty result.
        /// </summary>
        public EnumerationResult()
        {
            Timestamp = new Timestamp();
        }

        /// <summary>
        /// Instantiate a result from query parameters and data.
        /// Automatically calculates RecordsRemaining, EndOfResults, and ContinuationToken.
        /// </summary>
        /// <param name="query">The enumeration query parameters.</param>
        /// <param name="objects">The paginated objects for this page.</param>
        /// <param name="totalRecords">Total number of records in the collection.</param>
        /// <exception cref="ArgumentNullException">Thrown when query or objects is null.</exception>
        public EnumerationResult(EnumerationQuery query, List<T> objects, long totalRecords)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(objects);

            Timestamp = new Timestamp();
            MaxResults = query.MaxResults;
            Skip = query.Skip;
            TotalRecords = totalRecords;
            Objects = objects;

            // Calculate records remaining
            long recordsFetched = query.Skip + objects.Count;
            RecordsRemaining = Math.Max(0, totalRecords - recordsFetched);

            // Determine if this is the end of results. TotalRecords is authoritative for
            // non-empty pages; an empty page terminates to avoid repeating the same skip.
            EndOfResults = RecordsRemaining == 0 || objects.Count == 0;

            // Generate continuation token only if there are more results
            // Never generate a token if objects is empty (would cause infinite loop with same skip value)
            if (!EndOfResults && objects.Count > 0)
            {
                // Simple continuation token: encodes the next skip value
                int nextSkip = query.Skip + objects.Count;
                ContinuationToken = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"skip:{nextSkip}"));
            }
        }

        /// <summary>
        /// Create an empty result indicating no records found.
        /// </summary>
        /// <param name="query">The enumeration query parameters.</param>
        /// <returns>Empty enumeration result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null.</exception>
        public static EnumerationResult<T> Empty(EnumerationQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return new EnumerationResult<T>
            {
                Timestamp = new Timestamp(),
                MaxResults = query.MaxResults,
                Skip = query.Skip,
                TotalRecords = 0,
                RecordsRemaining = 0,
                EndOfResults = true,
                ContinuationToken = null,
                Objects = new List<T>()
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Parses a continuation token to extract the skip value.
        /// </summary>
        /// <param name="continuationToken">The continuation token to parse.</param>
        /// <param name="skip">The parsed skip value.</param>
        /// <returns>True if parsing was successful, false otherwise.</returns>
        public static bool TryParseContinuationToken(string? continuationToken, out int skip)
        {
            skip = 0;

            if (String.IsNullOrEmpty(continuationToken))
            {
                return false;
            }

            try
            {
                string decoded = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(continuationToken));

                if (decoded.StartsWith("skip:", StringComparison.Ordinal))
                {
                    return Int32.TryParse(decoded.Substring(5), out skip);
                }
            }
            catch
            {
                // Invalid token format
            }

            return false;
        }

        #endregion
    }
}
