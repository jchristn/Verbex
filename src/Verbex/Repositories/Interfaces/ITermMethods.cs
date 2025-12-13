namespace Verbex.Repositories.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for term-related repository operations.
    /// Provides operations for managing terms in the index vocabulary.
    /// </summary>
    public interface ITermMethods
    {
        /// <summary>
        /// Adds or gets a term, returning its ID.
        /// </summary>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term ID (k-sortable unique identifier).</returns>
        Task<string> AddOrGetAsync(string term, CancellationToken token = default);

        /// <summary>
        /// Gets a term by its text.
        /// </summary>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term record or null.</returns>
        Task<TermRecord?> GetAsync(string term, CancellationToken token = default);

        /// <summary>
        /// Gets a term by ID.
        /// </summary>
        /// <param name="id">Term ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term record or null.</returns>
        Task<TermRecord?> GetByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Gets multiple terms by their text.
        /// </summary>
        /// <param name="terms">Term texts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping term text to term record.</returns>
        Task<Dictionary<string, TermRecord>> GetMultipleAsync(IEnumerable<string> terms, CancellationToken token = default);

        /// <summary>
        /// Gets terms matching a prefix.
        /// </summary>
        /// <param name="prefix">Prefix to match.</param>
        /// <param name="limit">Maximum number of terms.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching terms.</returns>
        Task<List<TermRecord>> GetByPrefixAsync(string prefix, int limit = 100, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of unique terms.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term count.</returns>
        Task<long> GetCountAsync(CancellationToken token = default);

        /// <summary>
        /// Checks if a term exists.
        /// </summary>
        /// <param name="term">Term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if term exists.</returns>
        Task<bool> ExistsAsync(string term, CancellationToken token = default);

        /// <summary>
        /// Updates term frequencies.
        /// </summary>
        /// <param name="termId">Term ID.</param>
        /// <param name="documentFrequency">New document frequency.</param>
        /// <param name="totalFrequency">New total frequency.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateFrequenciesAsync(string termId, int documentFrequency, int totalFrequency, CancellationToken token = default);

        /// <summary>
        /// Increments term frequencies.
        /// </summary>
        /// <param name="termId">Term ID.</param>
        /// <param name="documentFrequencyDelta">Document frequency delta.</param>
        /// <param name="totalFrequencyDelta">Total frequency delta.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task IncrementFrequenciesAsync(string termId, int documentFrequencyDelta, int totalFrequencyDelta, CancellationToken token = default);

        /// <summary>
        /// Batch inserts terms that don't exist and returns all term IDs (existing or new).
        /// </summary>
        /// <param name="terms">List of term strings to add or get.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping term string to term ID.</returns>
        Task<Dictionary<string, string>> AddOrGetBatchAsync(List<string> terms, CancellationToken token = default);

        /// <summary>
        /// Batch increments term frequencies.
        /// </summary>
        /// <param name="updates">Dictionary mapping term ID to (documentFrequencyDelta, totalFrequencyDelta).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task IncrementFrequenciesBatchAsync(Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> updates, CancellationToken token = default);

        /// <summary>
        /// Batch decrements term frequencies.
        /// </summary>
        /// <param name="updates">Dictionary mapping term ID to (documentFrequencyDelta, totalFrequencyDelta).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DecrementFrequenciesBatchAsync(Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> updates, CancellationToken token = default);

        /// <summary>
        /// Gets top terms by document frequency.
        /// </summary>
        /// <param name="limit">Maximum number of terms.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of top terms.</returns>
        Task<List<TermRecord>> GetTopAsync(int limit = 100, CancellationToken token = default);

        /// <summary>
        /// Deletes terms with zero document frequency.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of terms deleted.</returns>
        Task<long> DeleteOrphanedAsync(CancellationToken token = default);
    }
}
