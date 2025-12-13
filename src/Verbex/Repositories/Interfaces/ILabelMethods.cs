namespace Verbex.Repositories.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for label-related repository operations.
    /// Provides operations for managing labels on documents and the index.
    /// </summary>
    public interface ILabelMethods
    {
        /// <summary>
        /// Adds a label to a document or the index.
        /// </summary>
        /// <param name="documentId">Document ID, or null for index-level label.</param>
        /// <param name="label">Label text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddAsync(string? documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Gets labels for a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetByDocumentAsync(string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets index-level labels.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetIndexLabelsAsync(CancellationToken token = default);

        /// <summary>
        /// Gets all distinct labels.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of unique labels.</returns>
        Task<List<string>> GetAllDistinctAsync(CancellationToken token = default);

        /// <summary>
        /// Gets documents with a specific label.
        /// </summary>
        /// <param name="label">Label to filter by.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        Task<List<DocumentMetadata>> GetDocumentsByLabelAsync(string label, CancellationToken token = default);

        /// <summary>
        /// Checks if a label exists for a document.
        /// </summary>
        /// <param name="documentId">Document ID, or null for index-level.</param>
        /// <param name="label">Label text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if label exists.</returns>
        Task<bool> ExistsAsync(string? documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Removes a label from a document or the index.
        /// </summary>
        /// <param name="documentId">Document ID, or null for index-level.</param>
        /// <param name="label">Label text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if label was removed.</returns>
        Task<bool> RemoveAsync(string? documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Removes all labels from a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of labels removed.</returns>
        Task<long> RemoveAllAsync(string documentId, CancellationToken token = default);

        /// <summary>
        /// Batch adds labels to a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="labels">List of labels to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddBatchAsync(string documentId, List<string> labels, CancellationToken token = default);

        /// <summary>
        /// Replaces all labels for a document with new labels.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="labels">List of new labels.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceAsync(string documentId, List<string> labels, CancellationToken token = default);
    }
}
