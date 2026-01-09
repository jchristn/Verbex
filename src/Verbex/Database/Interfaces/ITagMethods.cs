namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for tag-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides operations for document and index-level key-value tags.
    /// Tags are used for metadata and filtering.
    /// </remarks>
    public interface ITagMethods
    {
        /// <summary>
        /// Sets a tag on a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="id">Tag ID (k-sortable unique identifier).</param>
        /// <param name="documentId">Document ID (or null for index-level tag).</param>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task SetAsync(string tenantId, string indexId, string id, string? documentId, string key, string? value, CancellationToken token = default);

        /// <summary>
        /// Adds multiple tags in a batch.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="records">The tag records to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddBatchAsync(string tenantId, string indexId, IEnumerable<TagRecord> records, CancellationToken token = default);

        /// <summary>
        /// Gets a tag value by key.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tag value or null if not found.</returns>
        Task<string?> GetAsync(string tenantId, string indexId, string documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Gets all tags for a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string>> GetByDocumentAsync(string tenantId, string indexId, string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets all index-level tags.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string>> GetIndexTagsAsync(string tenantId, string indexId, CancellationToken token = default);

        /// <summary>
        /// Gets all distinct tag keys in the index.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of distinct keys.</returns>
        Task<List<string>> GetAllDistinctKeysAsync(string tenantId, string indexId, CancellationToken token = default);

        /// <summary>
        /// Gets document IDs that have a specific tag key.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs.</returns>
        Task<List<string>> GetDocumentsByKeyAsync(string tenantId, string indexId, string key, CancellationToken token = default);

        /// <summary>
        /// Gets document IDs that have a specific tag key-value pair.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs.</returns>
        Task<List<string>> GetDocumentsByTagAsync(string tenantId, string indexId, string key, string value, CancellationToken token = default);

        /// <summary>
        /// Checks if a tag exists on a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tag exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string indexId, string documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Removes a tag from a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tag was removed.</returns>
        Task<bool> RemoveAsync(string tenantId, string indexId, string documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Removes an index-level tag.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tag was removed.</returns>
        Task<bool> RemoveIndexTagAsync(string tenantId, string indexId, string key, CancellationToken token = default);

        /// <summary>
        /// Removes all tags from a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of tags removed.</returns>
        Task<long> RemoveAllAsync(string tenantId, string indexId, string documentId, CancellationToken token = default);

        /// <summary>
        /// Replaces all tags on a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="tags">The new tags.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceAsync(string tenantId, string indexId, string documentId, IDictionary<string, string> tags, CancellationToken token = default);

        /// <summary>
        /// Deletes all tags in an index.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of tags deleted.</returns>
        Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default);
    }
}
