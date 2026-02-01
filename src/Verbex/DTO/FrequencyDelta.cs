namespace Verbex.DTO
{
    /// <summary>
    /// Represents changes to document and total frequency counts for a term.
    /// </summary>
    public class FrequencyDelta
    {
        #region Public-Members

        /// <summary>
        /// Change in document frequency (number of documents containing the term).
        /// </summary>
        public int DocFreqDelta
        {
            get => _DocFreqDelta;
            set => _DocFreqDelta = value;
        }

        /// <summary>
        /// Change in total frequency (total occurrences of the term across all documents).
        /// </summary>
        public int TotalFreqDelta
        {
            get => _TotalFreqDelta;
            set => _TotalFreqDelta = value;
        }

        #endregion

        #region Private-Members

        private int _DocFreqDelta = 0;
        private int _TotalFreqDelta = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FrequencyDelta()
        {
        }

        /// <summary>
        /// Instantiate with values.
        /// </summary>
        /// <param name="docFreqDelta">Change in document frequency.</param>
        /// <param name="totalFreqDelta">Change in total frequency.</param>
        public FrequencyDelta(int docFreqDelta, int totalFreqDelta)
        {
            _DocFreqDelta = docFreqDelta;
            _TotalFreqDelta = totalFreqDelta;
        }

        #endregion
    }
}
