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
    /// Provides operations for document and index-level key-value tags (prefixed tables)
    /// as well as tenant, user, and credential-level tags (unprefixed tables).
    /// Tags are used for metadata and filtering.
    /// </remarks>
    public interface ITagMethods
    {
        #region Document and Index Tags (Prefixed Tables)

        /// <summary>
        /// Sets a tag on a document or index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Tag ID (k-sortable unique identifier).</param>
        /// <param name="documentId">Document ID (or null for index-level tag).</param>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task SetAsync(string tablePrefix, string id, string? documentId, string key, string? value, CancellationToken token = default);

        /// <summary>
        /// Adds multiple tags in a batch.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="records">The tag records to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddBatchAsync(string tablePrefix, IEnumerable<TagRecord> records, CancellationToken token = default);

        /// <summary>
        /// Gets a tag value by key.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tag value or null if not found.</returns>
        Task<string?> GetAsync(string tablePrefix, string documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Gets all tags for a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string>> GetByDocumentAsync(string tablePrefix, string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets all index-level tags.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string>> GetIndexTagsAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Gets all distinct tag keys in the index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of distinct keys.</returns>
        Task<List<string>> GetAllDistinctKeysAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Gets document IDs that have a specific tag key.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs.</returns>
        Task<List<string>> GetDocumentsByKeyAsync(string tablePrefix, string key, CancellationToken token = default);

        /// <summary>
        /// Gets document IDs that have a specific tag key-value pair.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs.</returns>
        Task<List<string>> GetDocumentsByTagAsync(string tablePrefix, string key, string value, CancellationToken token = default);

        /// <summary>
        /// Checks if a tag exists on a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tag exists.</returns>
        Task<bool> ExistsAsync(string tablePrefix, string documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Removes a tag from a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tag was removed.</returns>
        Task<bool> RemoveAsync(string tablePrefix, string documentId, string key, CancellationToken token = default);

        /// <summary>
        /// Removes an index-level tag.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="key">The tag key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tag was removed.</returns>
        Task<bool> RemoveIndexTagAsync(string tablePrefix, string key, CancellationToken token = default);

        /// <summary>
        /// Removes all tags from a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of tags removed.</returns>
        Task<long> RemoveAllAsync(string tablePrefix, string documentId, CancellationToken token = default);

        /// <summary>
        /// Replaces all tags on a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="tags">The new tags.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceAsync(string tablePrefix, string documentId, IDictionary<string, string> tags, CancellationToken token = default);

        /// <summary>
        /// Deletes all tags in an index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of tags deleted.</returns>
        Task<long> DeleteAllAsync(string tablePrefix, CancellationToken token = default);

        #endregion

        #region Tenant Tags (Unprefixed Tables)

        /// <summary>
        /// Gets all tags for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string>> GetTenantTagsAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Replaces all tags on a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="tags">The new tags.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceTenantTagsAsync(string tenantId, IDictionary<string, string> tags, CancellationToken token = default);

        /// <summary>
        /// Deletes all tags for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of tags deleted.</returns>
        Task<long> DeleteAllTenantTagsAsync(string tenantId, CancellationToken token = default);

        #endregion

        #region User Tags (Unprefixed Tables)

        /// <summary>
        /// Gets all tags for a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string>> GetUserTagsAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Replaces all tags on a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="tags">The new tags.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceUserTagsAsync(string tenantId, string userId, IDictionary<string, string> tags, CancellationToken token = default);

        /// <summary>
        /// Deletes all tags for a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of tags deleted.</returns>
        Task<long> DeleteAllUserTagsAsync(string tenantId, string userId, CancellationToken token = default);

        #endregion

        #region Credential Tags (Unprefixed Tables)

        /// <summary>
        /// Gets all tags for a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of key-value pairs.</returns>
        Task<Dictionary<string, string>> GetCredentialTagsAsync(string tenantId, string credentialId, CancellationToken token = default);

        /// <summary>
        /// Replaces all tags on a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="tags">The new tags.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceCredentialTagsAsync(string tenantId, string credentialId, IDictionary<string, string> tags, CancellationToken token = default);

        /// <summary>
        /// Deletes all tags for a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of tags deleted.</returns>
        Task<long> DeleteAllCredentialTagsAsync(string tenantId, string credentialId, CancellationToken token = default);

        #endregion
    }
}
