namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.DTO;
    using Verbex.Models;

    /// <summary>
    /// Interface for term-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides CRUD operations for terms (vocabulary) within indexes.
    /// Terms are stored in tables prefixed with the index identifier.
    /// </remarks>
    public interface ITermMethods
    {
        /// <summary>
        /// Adds a term or returns existing if already present.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Term ID (k-sortable unique identifier).</param>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The term ID (new or existing).</returns>
        Task<string> AddOrGetAsync(string tablePrefix, string id, string term, CancellationToken token = default);

        /// <summary>
        /// Gets a term by its text value.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The term record or null if not found.</returns>
        Task<TermRecord?> GetAsync(string tablePrefix, string term, CancellationToken token = default);

        /// <summary>
        /// Gets a term by its ID.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Term ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The term record or null if not found.</returns>
        Task<TermRecord?> GetByIdAsync(string tablePrefix, string id, CancellationToken token = default);

        /// <summary>
        /// Gets multiple terms by their text values.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="terms">The term texts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping term text to term record.</returns>
        Task<Dictionary<string, TermRecord>> GetMultipleAsync(string tablePrefix, IEnumerable<string> terms, CancellationToken token = default);

        /// <summary>
        /// Gets terms starting with a prefix.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="prefix">The prefix to search for.</param>
        /// <param name="limit">Maximum number of terms to return.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching term records.</returns>
        Task<List<TermRecord>> GetByPrefixAsync(string tablePrefix, string prefix, int limit = 100, CancellationToken token = default);

        /// <summary>
        /// Gets the top terms by document frequency.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="limit">Maximum number of terms to return.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of term records ordered by document frequency descending.</returns>
        Task<List<TermRecord>> GetTopAsync(string tablePrefix, int limit = 100, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of unique terms.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term count.</returns>
        Task<long> GetCountAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Checks if a term exists.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if term exists.</returns>
        Task<bool> ExistsAsync(string tablePrefix, string term, CancellationToken token = default);

        /// <summary>
        /// Updates frequency statistics for a term.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="termId">Term ID.</param>
        /// <param name="documentFrequency">New document frequency.</param>
        /// <param name="totalFrequency">New total frequency.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateFrequenciesAsync(string tablePrefix, string termId, int documentFrequency, int totalFrequency, CancellationToken token = default);

        /// <summary>
        /// Increments frequency statistics for a term.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="termId">Term ID.</param>
        /// <param name="documentFrequencyDelta">Document frequency increment.</param>
        /// <param name="totalFrequencyDelta">Total frequency increment.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task IncrementFrequenciesAsync(string tablePrefix, string termId, int documentFrequencyDelta, int totalFrequencyDelta, CancellationToken token = default);

        /// <summary>
        /// Adds or gets multiple terms in a batch.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="terms">Dictionary of term ID to term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping term text to term ID.</returns>
        Task<Dictionary<string, string>> AddOrGetBatchAsync(string tablePrefix, Dictionary<string, string> terms, CancellationToken token = default);

        /// <summary>
        /// Increments frequencies for multiple terms.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="updates">Dictionary mapping term ID to frequency deltas.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task IncrementFrequenciesBatchAsync(string tablePrefix, Dictionary<string, FrequencyDelta> updates, CancellationToken token = default);

        /// <summary>
        /// Decrements frequencies for multiple terms.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="updates">Dictionary mapping term ID to frequency deltas.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DecrementFrequenciesBatchAsync(string tablePrefix, Dictionary<string, FrequencyDelta> updates, CancellationToken token = default);

        /// <summary>
        /// Deletes terms with zero document frequency.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of deleted term texts (for cache invalidation).</returns>
        Task<List<string>> DeleteOrphanedAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Deletes all terms in an index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of terms deleted.</returns>
        Task<long> DeleteAllAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Gets all term IDs in an index for cache loading.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping term text to term ID.</returns>
        Task<Dictionary<string, string>> GetAllTermIdsAsync(string tablePrefix, CancellationToken token = default);
    }
}
