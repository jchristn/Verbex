namespace Verbex.Models
{
    using System;

    /// <summary>
    /// Metrics captured during document ingestion.
    /// Provides detailed timing and count information for performance analysis.
    /// </summary>
    public class IngestionMetrics
    {
        #region Public-Members

        /// <summary>
        /// Total time for the entire ingestion process in milliseconds.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double TotalMs
        {
            get { return _TotalMs; }
            set { _TotalMs = Math.Round(value, 4); }
        }

        /// <summary>
        /// Detailed timing for each step of the ingestion process.
        /// </summary>
        public IngestionStepTimings Steps
        {
            get { return _Steps; }
            set { _Steps = value ?? new IngestionStepTimings(); }
        }

        /// <summary>
        /// Count metrics for the ingested document.
        /// </summary>
        public IngestionCounts Counts
        {
            get { return _Counts; }
            set { _Counts = value ?? new IngestionCounts(); }
        }

        #endregion

        #region Private-Members

        private double _TotalMs = 0;
        private IngestionStepTimings _Steps = new IngestionStepTimings();
        private IngestionCounts _Counts = new IngestionCounts();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="IngestionMetrics"/> class.
        /// </summary>
        public IngestionMetrics()
        {
        }

        #endregion
    }

    /// <summary>
    /// Timing information for each step of the ingestion process.
    /// All timing values are rounded to 4 decimal places.
    /// </summary>
    public class IngestionStepTimings
    {
        #region Public-Members

        /// <summary>
        /// Time spent waiting to acquire the write lock in milliseconds.
        /// High values indicate contention from concurrent operations.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double LockWaitMs
        {
            get { return _LockWaitMs; }
            set { _LockWaitMs = Math.Round(value, 4); }
        }

        /// <summary>
        /// Time spent tokenizing the document content in milliseconds.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double TokenizationMs
        {
            get { return _TokenizationMs; }
            set { _TokenizationMs = Math.Round(value, 4); }
        }

        /// <summary>
        /// Time spent calculating character and term positions in milliseconds.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double PositionCalculationMs
        {
            get { return _PositionCalculationMs; }
            set { _PositionCalculationMs = Math.Round(value, 4); }
        }

        /// <summary>
        /// Time spent looking up or adding terms to the vocabulary in milliseconds.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double TermLookupMs
        {
            get { return _TermLookupMs; }
            set { _TermLookupMs = Math.Round(value, 4); }
        }

        /// <summary>
        /// Time spent inserting document-term mappings in milliseconds.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double DocumentTermInsertMs
        {
            get { return _DocumentTermInsertMs; }
            set { _DocumentTermInsertMs = Math.Round(value, 4); }
        }

        /// <summary>
        /// Time spent updating term frequencies in milliseconds.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double FrequencyUpdateMs
        {
            get { return _FrequencyUpdateMs; }
            set { _FrequencyUpdateMs = Math.Round(value, 4); }
        }

        /// <summary>
        /// Time spent updating document metadata in milliseconds.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double DocumentUpdateMs
        {
            get { return _DocumentUpdateMs; }
            set { _DocumentUpdateMs = Math.Round(value, 4); }
        }

        /// <summary>
        /// Time spent committing the transaction in milliseconds.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double TransactionCommitMs
        {
            get { return _TransactionCommitMs; }
            set { _TransactionCommitMs = Math.Round(value, 4); }
        }

        #endregion

        #region Private-Members

        private double _LockWaitMs = 0;
        private double _TokenizationMs = 0;
        private double _PositionCalculationMs = 0;
        private double _TermLookupMs = 0;
        private double _DocumentTermInsertMs = 0;
        private double _FrequencyUpdateMs = 0;
        private double _DocumentUpdateMs = 0;
        private double _TransactionCommitMs = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="IngestionStepTimings"/> class.
        /// </summary>
        public IngestionStepTimings()
        {
        }

        #endregion
    }

    /// <summary>
    /// Count metrics for document ingestion.
    /// </summary>
    public class IngestionCounts
    {
        #region Public-Members

        /// <summary>
        /// Total number of tokens in the document (including duplicates).
        /// </summary>
        public int TotalTokens
        {
            get { return _TotalTokens; }
            set { _TotalTokens = value; }
        }

        /// <summary>
        /// Number of unique terms in the document.
        /// </summary>
        public int UniqueTerms
        {
            get { return _UniqueTerms; }
            set { _UniqueTerms = value; }
        }

        /// <summary>
        /// Number of new terms added to the vocabulary.
        /// </summary>
        public int NewTerms
        {
            get { return _NewTerms; }
            set { _NewTerms = value; }
        }

        /// <summary>
        /// Number of term ID lookups that were satisfied from the cache.
        /// </summary>
        public int TermCacheHits
        {
            get { return _TermCacheHits; }
            set { _TermCacheHits = value; }
        }

        /// <summary>
        /// Number of term ID lookups that required database queries.
        /// </summary>
        public int TermCacheMisses
        {
            get { return _TermCacheMisses; }
            set { _TermCacheMisses = value; }
        }

        /// <summary>
        /// Gets the term cache hit rate (0.0 to 1.0).
        /// Returns 0 if no lookups were performed.
        /// Value is rounded to 4 decimal places.
        /// </summary>
        public double TermCacheHitRate
        {
            get
            {
                int total = _TermCacheHits + _TermCacheMisses;
                return total > 0 ? Math.Round((double)_TermCacheHits / total, 4) : 0.0;
            }
        }

        #endregion

        #region Private-Members

        private int _TotalTokens = 0;
        private int _UniqueTerms = 0;
        private int _NewTerms = 0;
        private int _TermCacheHits = 0;
        private int _TermCacheMisses = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="IngestionCounts"/> class.
        /// </summary>
        public IngestionCounts()
        {
        }

        #endregion
    }
}
