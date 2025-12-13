namespace Verbex.Repositories.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for statistics-related repository operations.
    /// Provides operations for retrieving index and term statistics.
    /// </summary>
    public interface IStatisticsMethods
    {
        /// <summary>
        /// Gets comprehensive index statistics.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Index statistics.</returns>
        Task<IndexStatistics> GetIndexStatisticsAsync(CancellationToken token = default);

        /// <summary>
        /// Gets statistics for a specific term.
        /// </summary>
        /// <param name="term">Term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Term statistics or null if term not found.</returns>
        Task<TermStatisticsResult?> GetTermStatisticsAsync(string term, CancellationToken token = default);
    }
}
