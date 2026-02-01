namespace Verbex.Server.Services
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Server.Classes;

    /// <summary>
    /// Interface for backup and restore operations on indices.
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// Creates a backup of an index as a ZIP archive stream.
        /// </summary>
        /// <param name="tenantId">The tenant ID.</param>
        /// <param name="indexId">The index ID to backup.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A stream containing the backup ZIP archive.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or indexId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the index is not found or is in-memory only.</exception>
        Task<Stream> CreateBackupAsync(string tenantId, string indexId, CancellationToken token = default);

        /// <summary>
        /// Restores a backup to a new index.
        /// </summary>
        /// <param name="tenantId">The tenant ID for the new index.</param>
        /// <param name="backupStream">The backup ZIP archive stream.</param>
        /// <param name="newName">Optional new name for the restored index.</param>
        /// <param name="newId">Optional specific ID for the new index.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The result of the restore operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or backupStream is null.</exception>
        Task<RestoreResult> RestoreNewAsync(string tenantId, Stream backupStream, string? newName, string? newId, CancellationToken token = default);

        /// <summary>
        /// Restores a backup by replacing an existing index.
        /// </summary>
        /// <param name="tenantId">The tenant ID.</param>
        /// <param name="indexId">The index ID to replace.</param>
        /// <param name="backupStream">The backup ZIP archive stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The result of the restore operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId, indexId, or backupStream is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the index is not found.</exception>
        Task<RestoreResult> RestoreReplaceAsync(string tenantId, string indexId, Stream backupStream, CancellationToken token = default);
    }
}
