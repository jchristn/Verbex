namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for document-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides CRUD operations for documents within indexes.
    /// Documents are stored in tables prefixed with the index identifier.
    /// </remarks>
    public interface IDocumentMethods
    {
        /// <summary>
        /// Adds a new document to the index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Document ID (k-sortable unique identifier).</param>
        /// <param name="name">Document name.</param>
        /// <param name="contentSha256">SHA-256 hash for duplicate detection.</param>
        /// <param name="documentLength">Character count of document.</param>
        /// <param name="customMetadata">Optional custom metadata (any JSON-serializable value).</param>
        /// <param name="indexingRuntimeMs">Optional indexing runtime in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddAsync(string tablePrefix, string id, string name, string? contentSha256, int documentLength, object? customMetadata = null, decimal? indexingRuntimeMs = null, CancellationToken token = default);

        /// <summary>
        /// Gets a document by ID.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata or null if not found.</returns>
        Task<DocumentMetadata?> GetAsync(string tablePrefix, string id, CancellationToken token = default);

        /// <summary>
        /// Gets a document by name.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="name">Document name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata or null if not found.</returns>
        Task<DocumentMetadata?> GetByNameAsync(string tablePrefix, string name, CancellationToken token = default);

        /// <summary>
        /// Gets a document by ID with all metadata (labels, tags, terms) in a single query.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata with populated labels, tags, and terms, or null if not found.</returns>
        Task<DocumentMetadata?> GetWithMetadataAsync(string tablePrefix, string id, CancellationToken token = default);

        /// <summary>
        /// Gets documents by content SHA-256 hash.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="contentSha256">SHA-256 content hash.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching documents.</returns>
        Task<List<DocumentMetadata>> GetByContentSha256Async(string tablePrefix, string contentSha256, CancellationToken token = default);

        /// <summary>
        /// Gets all documents with pagination.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="limit">Maximum number of documents to return.</param>
        /// <param name="offset">Number of documents to skip.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        Task<List<DocumentMetadata>> GetAllAsync(string tablePrefix, int limit = 100, int offset = 0, CancellationToken token = default);

        /// <summary>
        /// Gets multiple documents by IDs.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="ids">Document IDs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        Task<List<DocumentMetadata>> GetByIdsAsync(string tablePrefix, IEnumerable<string> ids, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of documents.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document count.</returns>
        Task<long> GetCountAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Checks if a document exists by ID.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document exists.</returns>
        Task<bool> ExistsAsync(string tablePrefix, string id, CancellationToken token = default);

        /// <summary>
        /// Checks if multiple documents exist by their IDs in a single query.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="ids">Document IDs to check.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs that exist.</returns>
        Task<List<string>> ExistsBatchAsync(string tablePrefix, IEnumerable<string> ids, CancellationToken token = default);

        /// <summary>
        /// Checks if a document exists by name.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="name">Document name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document exists.</returns>
        Task<bool> ExistsByNameAsync(string tablePrefix, string name, CancellationToken token = default);

        /// <summary>
        /// Updates a document's metadata.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Document ID.</param>
        /// <param name="name">New document name.</param>
        /// <param name="contentSha256">New SHA-256 content hash.</param>
        /// <param name="documentLength">New document length.</param>
        /// <param name="termCount">New term count.</param>
        /// <param name="customMetadata">Optional custom metadata (any JSON-serializable value).</param>
        /// <param name="indexingRuntimeMs">Optional indexing runtime in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateAsync(string tablePrefix, string id, string name, string? contentSha256, int documentLength, int termCount, object? customMetadata = null, decimal? indexingRuntimeMs = null, CancellationToken token = default);

        /// <summary>
        /// Updates only the custom metadata for a document.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Document ID.</param>
        /// <param name="customMetadata">Custom metadata (any JSON-serializable value, or null to clear).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateCustomMetadataAsync(string tablePrefix, string id, object? customMetadata, CancellationToken token = default);

        /// <summary>
        /// Deletes a document and all associated data.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="id">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document was deleted.</returns>
        Task<bool> DeleteAsync(string tablePrefix, string id, CancellationToken token = default);

        /// <summary>
        /// Deletes all documents in an index.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of documents deleted.</returns>
        Task<long> DeleteAllAsync(string tablePrefix, CancellationToken token = default);

        /// <summary>
        /// Deletes multiple documents by IDs.
        /// </summary>
        /// <param name="tablePrefix">Table prefix (index identifier) for the prefixed tables.</param>
        /// <param name="ids">Document IDs to delete.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs that were actually deleted (existed).</returns>
        Task<List<string>> DeleteBatchAsync(string tablePrefix, IEnumerable<string> ids, CancellationToken token = default);
    }
}
