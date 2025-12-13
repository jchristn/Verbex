namespace Verbex.Repositories.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for document-related repository operations.
    /// Provides CRUD operations for documents in the index.
    /// </summary>
    public interface IDocumentMethods
    {
        /// <summary>
        /// Adds a new document to the index.
        /// </summary>
        /// <param name="id">Document ID (k-sortable unique identifier).</param>
        /// <param name="name">Document name.</param>
        /// <param name="contentSha256">SHA-256 hash for duplicate detection.</param>
        /// <param name="documentLength">Character count of document.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddAsync(string id, string name, string? contentSha256, int documentLength, CancellationToken token = default);

        /// <summary>
        /// Gets a document by ID.
        /// </summary>
        /// <param name="id">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata or null if not found.</returns>
        Task<DocumentMetadata?> GetAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Gets a document by name.
        /// </summary>
        /// <param name="name">Document name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata or null if not found.</returns>
        Task<DocumentMetadata?> GetByNameAsync(string name, CancellationToken token = default);

        /// <summary>
        /// Gets a document by ID with all metadata (labels, tags, terms) in a single query.
        /// </summary>
        /// <param name="id">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document metadata with populated labels, tags, and terms, or null if not found.</returns>
        Task<DocumentMetadata?> GetWithMetadataAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Gets documents by content SHA-256 hash.
        /// </summary>
        /// <param name="contentSha256">SHA-256 content hash.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching documents.</returns>
        Task<List<DocumentMetadata>> GetByContentSha256Async(string contentSha256, CancellationToken token = default);

        /// <summary>
        /// Gets all documents with pagination.
        /// </summary>
        /// <param name="limit">Maximum number of documents to return.</param>
        /// <param name="offset">Number of documents to skip.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        Task<List<DocumentMetadata>> GetAllAsync(int limit = 100, int offset = 0, CancellationToken token = default);

        /// <summary>
        /// Gets multiple documents by IDs.
        /// </summary>
        /// <param name="ids">Document IDs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of documents.</returns>
        Task<List<DocumentMetadata>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of documents.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document count.</returns>
        Task<long> GetCountAsync(CancellationToken token = default);

        /// <summary>
        /// Checks if a document exists by ID.
        /// </summary>
        /// <param name="id">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document exists.</returns>
        Task<bool> ExistsAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Checks if a document exists by name.
        /// </summary>
        /// <param name="name">Document name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document exists.</returns>
        Task<bool> ExistsByNameAsync(string name, CancellationToken token = default);

        /// <summary>
        /// Updates a document's metadata.
        /// </summary>
        /// <param name="id">Document ID.</param>
        /// <param name="name">New document name.</param>
        /// <param name="contentSha256">New SHA-256 content hash.</param>
        /// <param name="documentLength">New document length.</param>
        /// <param name="termCount">New term count.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateAsync(string id, string name, string? contentSha256, int documentLength, int termCount, CancellationToken token = default);

        /// <summary>
        /// Deletes a document and all associated data.
        /// </summary>
        /// <param name="id">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if document was deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Deletes all documents and associated data.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of documents deleted.</returns>
        Task<long> DeleteAllAsync(CancellationToken token = default);
    }
}
