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
        private SearchTimingInfo? _TimingInfo;

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
            _TimingInfo = null;
        }

        /// <summary>
        /// Initializes a new instance of the SearchResults class with timing breakdown.
        /// </summary>
        /// <param name="results">The search results.</param>
        /// <param name="totalCount">Total number of matching documents.</param>
        /// <param name="searchTime">Time taken to perform the search.</param>
        /// <param name="timingInfo">Detailed timing breakdown for each search step.</param>
        /// <exception cref="ArgumentNullException">Thrown when results is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when totalCount is negative.</exception>
        public SearchResults(IEnumerable<SearchResult> results, int totalCount, TimeSpan searchTime, SearchTimingInfo? timingInfo)
            : this(results, totalCount, searchTime)
        {
            _TimingInfo = timingInfo;
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

        /// <summary>
        /// Gets detailed timing information for each search step.
        /// May be null if timing was not collected.
        /// </summary>
        public SearchTimingInfo? TimingInfo
        {
            get { return _TimingInfo; }
        }
    }

    /// <summary>
    /// Detailed timing information for search operations.
    /// </summary>
    public class SearchTimingInfo
    {
        /// <summary>
        /// Time spent looking up search terms in the index.
        /// </summary>
        public long TermLookupMs { get; set; }

        /// <summary>
        /// Number of terms found in the index.
        /// </summary>
        public int TermsFound { get; set; }

        /// <summary>
        /// Time spent executing the main search query.
        /// </summary>
        public long MainSearchMs { get; set; }

        /// <summary>
        /// Number of documents matching the search criteria.
        /// </summary>
        public int MatchesFound { get; set; }

        /// <summary>
        /// Time spent fetching term frequency data for scoring.
        /// </summary>
        public long TermFrequenciesMs { get; set; }

        /// <summary>
        /// Number of term frequency records fetched.
        /// </summary>
        public int TermFrequencyRecords { get; set; }

        /// <summary>
        /// Time spent fetching document metadata.
        /// </summary>
        public long DocumentMetadataMs { get; set; }

        /// <summary>
        /// Number of documents fetched.
        /// </summary>
        public int DocumentsFetched { get; set; }

        /// <summary>
        /// Time spent getting total document count.
        /// </summary>
        public long DocumentCountMs { get; set; }

        /// <summary>
        /// Total documents in the index.
        /// </summary>
        public long TotalDocuments { get; set; }
    }
}