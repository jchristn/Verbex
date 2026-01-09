namespace Verbex.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for statistics-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides operations for retrieving index and term statistics.
    /// </remarks>
    public interface IStatisticsMethods
    {
        /// <summary>
        /// Gets overall index statistics.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Index statistics.</returns>
        Task<IndexStatistics> GetIndexStatisticsAsync(string tenantId, string indexId, CancellationToken token = default);

        /// <summary>
        /// Gets statistics for a specific term.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="term">The term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term statistics or null if term not found.</returns>
        Task<TermStatisticsResult?> GetTermStatisticsAsync(string tenantId, string indexId, string term, CancellationToken token = default);

        /// <summary>
        /// Gets global statistics across all indexes for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Aggregated statistics for the tenant.</returns>
        Task<TenantStatistics> GetTenantStatisticsAsync(string tenantId, CancellationToken token = default);
    }

    /// <summary>
    /// Represents aggregated statistics for a tenant.
    /// </summary>
    public class TenantStatistics
    {
        /// <summary>
        /// Gets or sets the tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of indexes.
        /// </summary>
        public long IndexCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of documents across all indexes.
        /// </summary>
        public long TotalDocumentCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of unique terms across all indexes.
        /// </summary>
        public long TotalTermCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of users in the tenant.
        /// </summary>
        public long UserCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of credentials in the tenant.
        /// </summary>
        public long CredentialCount { get; set; }
    }
}
