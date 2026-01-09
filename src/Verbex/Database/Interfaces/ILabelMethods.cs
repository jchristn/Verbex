namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for label-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides operations for document and index-level labels.
    /// Labels are string tags used for categorization and filtering.
    /// </remarks>
    public interface ILabelMethods
    {
        /// <summary>
        /// Adds a label to a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="id">Label ID (k-sortable unique identifier).</param>
        /// <param name="documentId">Document ID (or null for index-level label).</param>
        /// <param name="label">The label text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddAsync(string tenantId, string indexId, string id, string? documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Adds multiple labels in a batch.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="records">The label records to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddBatchAsync(string tenantId, string indexId, IEnumerable<LabelRecord> records, CancellationToken token = default);

        /// <summary>
        /// Gets all labels for a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetByDocumentAsync(string tenantId, string indexId, string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets all index-level labels.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetIndexLabelsAsync(string tenantId, string indexId, CancellationToken token = default);

        /// <summary>
        /// Gets all distinct labels in the index.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of distinct labels.</returns>
        Task<List<string>> GetAllDistinctAsync(string tenantId, string indexId, CancellationToken token = default);

        /// <summary>
        /// Gets document IDs that have a specific label.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="label">The label to search for.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs.</returns>
        Task<List<string>> GetDocumentsByLabelAsync(string tenantId, string indexId, string label, CancellationToken token = default);

        /// <summary>
        /// Checks if a label exists on a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="label">The label to check.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the label exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string indexId, string documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Removes a label from a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="label">The label to remove.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the label was removed.</returns>
        Task<bool> RemoveAsync(string tenantId, string indexId, string documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Removes an index-level label.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="label">The label to remove.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the label was removed.</returns>
        Task<bool> RemoveIndexLabelAsync(string tenantId, string indexId, string label, CancellationToken token = default);

        /// <summary>
        /// Removes all labels from a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of labels removed.</returns>
        Task<long> RemoveAllAsync(string tenantId, string indexId, string documentId, CancellationToken token = default);

        /// <summary>
        /// Replaces all labels on a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="labels">The new labels.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceAsync(string tenantId, string indexId, string documentId, IEnumerable<string> labels, CancellationToken token = default);

        /// <summary>
        /// Deletes all labels in an index.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of labels deleted.</returns>
        Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default);
    }
}
