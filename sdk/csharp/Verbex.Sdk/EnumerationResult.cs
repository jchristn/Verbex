namespace Verbex.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result container for paginated enumeration of collections.
    /// </summary>
    /// <typeparam name="T">The type of objects in the collection.</typeparam>
    public class EnumerationResult<T>
    {
        /// <summary>
        /// Indicates whether the request was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Timestamp of when the result was generated.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Maximum number of results requested.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Number of records skipped.
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Number of iterations required to produce this result.
        /// </summary>
        public int IterationsRequired { get; set; } = 1;

        /// <summary>
        /// Opaque continuation token for fetching the next page.
        /// Null when EndOfResults is true.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Indicates whether there are no more records to fetch.
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

        /// <summary>
        /// Returns true if there are more records available to fetch.
        /// </summary>
        public bool HasMore => !EndOfResults && !string.IsNullOrEmpty(ContinuationToken);

        /// <summary>
        /// Creates EnumerationOptions to fetch the next page.
        /// </summary>
        /// <returns>Options for fetching the next page, or null if at end of results.</returns>
        public EnumerationOptions? GetNextPageOptions()
        {
            if (EndOfResults || string.IsNullOrEmpty(ContinuationToken))
            {
                return null;
            }

            return new EnumerationOptions
            {
                MaxResults = MaxResults,
                ContinuationToken = ContinuationToken
            };
        }
    }
}
