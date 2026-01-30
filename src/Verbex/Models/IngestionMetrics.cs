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
        /// </summary>
        public double TotalMs
        {
            get { return _TotalMs; }
            set { _TotalMs = value; }
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
    /// </summary>
    public class IngestionStepTimings
    {
        #region Public-Members

        /// <summary>
        /// Time spent tokenizing the document content in milliseconds.
        /// </summary>
        public double TokenizationMs
        {
            get { return _TokenizationMs; }
            set { _TokenizationMs = value; }
        }

        /// <summary>
        /// Time spent calculating character and term positions in milliseconds.
        /// </summary>
        public double PositionCalculationMs
        {
            get { return _PositionCalculationMs; }
            set { _PositionCalculationMs = value; }
        }

        /// <summary>
        /// Time spent looking up or adding terms to the vocabulary in milliseconds.
        /// </summary>
        public double TermLookupMs
        {
            get { return _TermLookupMs; }
            set { _TermLookupMs = value; }
        }

        /// <summary>
        /// Time spent inserting document-term mappings in milliseconds.
        /// </summary>
        public double DocumentTermInsertMs
        {
            get { return _DocumentTermInsertMs; }
            set { _DocumentTermInsertMs = value; }
        }

        /// <summary>
        /// Time spent updating term frequencies in milliseconds.
        /// </summary>
        public double FrequencyUpdateMs
        {
            get { return _FrequencyUpdateMs; }
            set { _FrequencyUpdateMs = value; }
        }

        /// <summary>
        /// Time spent updating document metadata in milliseconds.
        /// </summary>
        public double DocumentUpdateMs
        {
            get { return _DocumentUpdateMs; }
            set { _DocumentUpdateMs = value; }
        }

        /// <summary>
        /// Time spent committing the transaction in milliseconds.
        /// </summary>
        public double TransactionCommitMs
        {
            get { return _TransactionCommitMs; }
            set { _TransactionCommitMs = value; }
        }

        #endregion

        #region Private-Members

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

        #endregion

        #region Private-Members

        private int _TotalTokens = 0;
        private int _UniqueTerms = 0;
        private int _NewTerms = 0;

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
