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
    /// Provides operations for document and index-level labels (prefixed tables)
    /// as well as tenant, user, and credential-level labels (unprefixed tables).
    /// Labels are string tags used for categorization and filtering.
    /// </remarks>
    public interface ILabelMethods
    {
        #region Document and Index Labels (Prefixed Tables)

        /// <summary>
        /// Adds a label to a document or index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Label ID (k-sortable unique identifier).</param>
        /// <param name="documentId">Document ID (or null for index-level label).</param>
        /// <param name="label">The label text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddAsync(string tablePrefix, string id, string? documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Adds multiple labels in a batch.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="records">The label records to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddBatchAsync(string tablePrefix, IEnumerable<LabelRecord> records, CancellationToken token = default);

        /// <summary>
        /// Gets all labels for a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetByDocumentAsync(string tablePrefix, string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets all index-level labels.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetIndexLabelsAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Gets all distinct labels in the index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of distinct labels.</returns>
        Task<List<string>> GetAllDistinctAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Gets document IDs that have a specific label.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="label">The label to search for.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs.</returns>
        Task<List<string>> GetDocumentsByLabelAsync(string tablePrefix, string label, CancellationToken token = default);

        /// <summary>
        /// Checks if a label exists on a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="label">The label to check.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the label exists.</returns>
        Task<bool> ExistsAsync(string tablePrefix, string documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Removes a label from a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="label">The label to remove.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the label was removed.</returns>
        Task<bool> RemoveAsync(string tablePrefix, string documentId, string label, CancellationToken token = default);

        /// <summary>
        /// Removes an index-level label.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="label">The label to remove.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the label was removed.</returns>
        Task<bool> RemoveIndexLabelAsync(string tablePrefix, string label, CancellationToken token = default);

        /// <summary>
        /// Removes all labels from a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of labels removed.</returns>
        Task<long> RemoveAllAsync(string tablePrefix, string documentId, CancellationToken token = default);

        /// <summary>
        /// Replaces all labels on a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="labels">The new labels.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceAsync(string tablePrefix, string documentId, IEnumerable<string> labels, CancellationToken token = default);

        /// <summary>
        /// Deletes all labels in an index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of labels deleted.</returns>
        Task<long> DeleteAllAsync(string tablePrefix, CancellationToken token = default);

        #endregion

        #region Tenant Labels (Unprefixed Tables)

        /// <summary>
        /// Gets all labels for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetTenantLabelsAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Replaces all labels on a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="labels">The new labels.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceTenantLabelsAsync(string tenantId, IEnumerable<string> labels, CancellationToken token = default);

        /// <summary>
        /// Deletes all labels for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of labels deleted.</returns>
        Task<long> DeleteAllTenantLabelsAsync(string tenantId, CancellationToken token = default);

        #endregion

        #region User Labels (Unprefixed Tables)

        /// <summary>
        /// Gets all labels for a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetUserLabelsAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Replaces all labels on a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="labels">The new labels.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceUserLabelsAsync(string tenantId, string userId, IEnumerable<string> labels, CancellationToken token = default);

        /// <summary>
        /// Deletes all labels for a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of labels deleted.</returns>
        Task<long> DeleteAllUserLabelsAsync(string tenantId, string userId, CancellationToken token = default);

        #endregion

        #region Credential Labels (Unprefixed Tables)

        /// <summary>
        /// Gets all labels for a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of labels.</returns>
        Task<List<string>> GetCredentialLabelsAsync(string tenantId, string credentialId, CancellationToken token = default);

        /// <summary>
        /// Replaces all labels on a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="labels">The new labels.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceCredentialLabelsAsync(string tenantId, string credentialId, IEnumerable<string> labels, CancellationToken token = default);

        /// <summary>
        /// Deletes all labels for a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of labels deleted.</returns>
        Task<long> DeleteAllCredentialLabelsAsync(string tenantId, string credentialId, CancellationToken token = default);

        #endregion
    }
}
