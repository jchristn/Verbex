namespace Verbex.Repositories.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for tag-related repository operations.
    /// Provides operations for managing key-value tags on documents and the index.
    /// </summary>
    public interface ITagMethods
    {
        /// <summary>
        /// Sets a tag on a document or the index.
        /// </summary>
        /// <param name="documentId">Document ID, or null for index-level tag.</param>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task SetAsync(string? documentId, string key, string? value, CancellationToken token = default);

        /// <summary>
        /// Gets a tag value.
        /// </summary>
        /// <param name="documentId">Document ID, or null for index-level.</param>
        /// <param name="key">Tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tag value or null.</returns>
        Task<string?> GetAsync(string? documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Gets all tags for a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string?>> GetByDocumentAsync(string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets index-level tags.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string?>> GetIndexTagsAsync(CancellationToken token = default);

        /// <summary>
        /// Gets all distinct tag keys.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of unique keys.</returns>
        Task<List<string>> GetAllDistinctKeysAsync(CancellationToken token = default);

        /// <summary>
        /// Gets documents with a specific tag key.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        Task<List<DocumentMetadata>> GetDocumentsByKeyAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Gets documents with a specific tag key-value pair.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        Task<List<DocumentMetadata>> GetDocumentsByTagAsync(string key, string value, CancellationToken token = default);

        /// <summary>
        /// Checks if a tag exists.
        /// </summary>
        /// <param name="documentId">Document ID, or null for index-level.</param>
        /// <param name="key">Tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if tag exists.</returns>
        Task<bool> ExistsAsync(string? documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Removes a tag.
        /// </summary>
        /// <param name="documentId">Document ID, or null for index-level.</param>
        /// <param name="key">Tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if tag was removed.</returns>
        Task<bool> RemoveAsync(string? documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Removes all tags from a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of tags removed.</returns>
        Task<long> RemoveAllAsync(string documentId, CancellationToken token = default);

        /// <summary>
        /// Batch adds tags to a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="tags">Dictionary of tag key-value pairs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddBatchAsync(string documentId, Dictionary<string, string> tags, CancellationToken token = default);

        /// <summary>
        /// Replaces all tags for a document with new tags.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="tags">Dictionary of new tag key-value pairs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceAsync(string documentId, Dictionary<string, string> tags, CancellationToken token = default);
    }
}
