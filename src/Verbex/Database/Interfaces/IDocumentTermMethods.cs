namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for document-term relationship operations.
    /// </summary>
    /// <remarks>
    /// Provides operations for the inverted index mapping documents to terms.
    /// Used for search operations and term position tracking.
    /// </remarks>
    public interface IDocumentTermMethods
    {
        /// <summary>
        /// Adds a document-term mapping.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="id">Mapping ID (k-sortable unique identifier).</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="termId">Term ID.</param>
        /// <param name="termFrequency">Number of times term appears in document.</param>
        /// <param name="characterPositions">List of character positions.</param>
        /// <param name="termPositions">List of term positions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddAsync(string tenantId, string indexId, string id, string documentId, string termId, int termFrequency, List<int> characterPositions, List<int> termPositions, CancellationToken token = default);

        /// <summary>
        /// Adds multiple document-term mappings in a batch.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="records">The records to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddBatchAsync(string tenantId, string indexId, IEnumerable<DocumentTermRecord> records, CancellationToken token = default);

        /// <summary>
        /// Gets all term mappings for a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records.</returns>
        Task<List<DocumentTermRecord>> GetByDocumentAsync(string tenantId, string indexId, string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets posting list for multiple terms.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="termIds">Term IDs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records.</returns>
        Task<List<DocumentTermRecord>> GetPostingsAsync(string tenantId, string indexId, IEnumerable<string> termIds, CancellationToken token = default);

        /// <summary>
        /// Gets posting list for a single term.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="termId">Term ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records.</returns>
        Task<List<DocumentTermRecord>> GetPostingsByTermAsync(string tenantId, string indexId, string termId, CancellationToken token = default);

        /// <summary>
        /// Searches for documents containing specified terms.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="termIds">Term IDs to search for.</param>
        /// <param name="useAndLogic">True for AND logic, false for OR logic.</param>
        /// <param name="limit">Maximum number of results.</param>
        /// <param name="labels">Optional label filters.</param>
        /// <param name="tags">Optional tag filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of search matches.</returns>
        Task<List<SearchMatch>> SearchAsync(
            string tenantId,
            string indexId,
            IEnumerable<string> termIds,
            bool useAndLogic = false,
            int limit = 100,
            IEnumerable<string>? labels = null,
            IDictionary<string, string>? tags = null,
            CancellationToken token = default);

        /// <summary>
        /// Deletes all term mappings for a document.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of mappings deleted.</returns>
        Task<long> DeleteByDocumentAsync(string tenantId, string indexId, string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets document-term mappings for specific documents and terms.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="documentIds">Document IDs.</param>
        /// <param name="termIds">Term IDs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records.</returns>
        Task<List<DocumentTermRecord>> GetByDocumentsAndTermsAsync(
            string tenantId,
            string indexId,
            IEnumerable<string> documentIds,
            IEnumerable<string> termIds,
            CancellationToken token = default);

        /// <summary>
        /// Deletes all document-term mappings in an index.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of mappings deleted.</returns>
        Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default);
    }
}
