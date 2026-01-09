namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for term-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides CRUD operations for terms (vocabulary) within indexes.
    /// Terms are scoped to a specific tenant and index.
    /// </remarks>
    public interface ITermMethods
    {
        /// <summary>
        /// Adds a term or returns existing if already present.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="id">Term ID (k-sortable unique identifier).</param>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The term ID (new or existing).</returns>
        Task<string> AddOrGetAsync(string tenantId, string indexId, string id, string term, CancellationToken token = default);

        /// <summary>
        /// Gets a term by its text value.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The term record or null if not found.</returns>
        Task<TermRecord?> GetAsync(string tenantId, string indexId, string term, CancellationToken token = default);

        /// <summary>
        /// Gets a term by its ID.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="id">Term ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The term record or null if not found.</returns>
        Task<TermRecord?> GetByIdAsync(string tenantId, string indexId, string id, CancellationToken token = default);

        /// <summary>
        /// Gets multiple terms by their text values.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="terms">The term texts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping term text to term record.</returns>
        Task<Dictionary<string, TermRecord>> GetMultipleAsync(string tenantId, string indexId, IEnumerable<string> terms, CancellationToken token = default);

        /// <summary>
        /// Gets terms starting with a prefix.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="prefix">The prefix to search for.</param>
        /// <param name="limit">Maximum number of terms to return.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching term records.</returns>
        Task<List<TermRecord>> GetByPrefixAsync(string tenantId, string indexId, string prefix, int limit = 100, CancellationToken token = default);

        /// <summary>
        /// Gets the top terms by document frequency.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="limit">Maximum number of terms to return.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of term records ordered by document frequency descending.</returns>
        Task<List<TermRecord>> GetTopAsync(string tenantId, string indexId, int limit = 100, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of unique terms.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term count.</returns>
        Task<long> GetCountAsync(string tenantId, string indexId, CancellationToken token = default);

        /// <summary>
        /// Checks if a term exists.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if term exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string indexId, string term, CancellationToken token = default);

        /// <summary>
        /// Updates frequency statistics for a term.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="termId">Term ID.</param>
        /// <param name="documentFrequency">New document frequency.</param>
        /// <param name="totalFrequency">New total frequency.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateFrequenciesAsync(string tenantId, string indexId, string termId, int documentFrequency, int totalFrequency, CancellationToken token = default);

        /// <summary>
        /// Increments frequency statistics for a term.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="termId">Term ID.</param>
        /// <param name="documentFrequencyDelta">Document frequency increment.</param>
        /// <param name="totalFrequencyDelta">Total frequency increment.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task IncrementFrequenciesAsync(string tenantId, string indexId, string termId, int documentFrequencyDelta, int totalFrequencyDelta, CancellationToken token = default);

        /// <summary>
        /// Adds or gets multiple terms in a batch.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="terms">Dictionary of term ID to term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping term text to term ID.</returns>
        Task<Dictionary<string, string>> AddOrGetBatchAsync(string tenantId, string indexId, Dictionary<string, string> terms, CancellationToken token = default);

        /// <summary>
        /// Increments frequencies for multiple terms.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="updates">Dictionary mapping term ID to (documentFrequencyDelta, totalFrequencyDelta).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task IncrementFrequenciesBatchAsync(string tenantId, string indexId, Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> updates, CancellationToken token = default);

        /// <summary>
        /// Decrements frequencies for multiple terms.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="updates">Dictionary mapping term ID to (documentFrequencyDelta, totalFrequencyDelta).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DecrementFrequenciesBatchAsync(string tenantId, string indexId, Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> updates, CancellationToken token = default);

        /// <summary>
        /// Deletes terms with zero document frequency.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of terms deleted.</returns>
        Task<long> DeleteOrphanedAsync(string tenantId, string indexId, CancellationToken token = default);

        /// <summary>
        /// Deletes all terms in an index.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of terms deleted.</returns>
        Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default);
    }
}
