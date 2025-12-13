namespace Verbex
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Container for search results with metadata.
    /// </summary>
    public class SearchResults
    {
        private List<SearchResult> _Results;
        private int _TotalCount;
        private TimeSpan _SearchTime;

        /// <summary>
        /// Initializes a new instance of the SearchResults class.
        /// </summary>
        /// <param name="results">The search results.</param>
        /// <param name="totalCount">Total number of matching documents.</param>
        /// <param name="searchTime">Time taken to perform the search.</param>
        /// <exception cref="ArgumentNullException">Thrown when results is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when totalCount is negative.</exception>
        public SearchResults(IEnumerable<SearchResult> results, int totalCount, TimeSpan searchTime)
        {
            ArgumentNullException.ThrowIfNull(results);

            if (totalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCount), "Total count cannot be negative.");
            }

            _Results = new List<SearchResult>(results);
            _TotalCount = totalCount;
            _SearchTime = searchTime;
        }

        /// <summary>
        /// Gets the search results.
        /// </summary>
        public IReadOnlyList<SearchResult> Results
        {
            get { return _Results; }
        }

        /// <summary>
        /// Gets the total number of matching documents.
        /// </summary>
        public int TotalCount
        {
            get { return _TotalCount; }
        }

        /// <summary>
        /// Gets the time taken to perform the search.
        /// </summary>
        public TimeSpan SearchTime
        {
            get { return _SearchTime; }
        }
    }
}