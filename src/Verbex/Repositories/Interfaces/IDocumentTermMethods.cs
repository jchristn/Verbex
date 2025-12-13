namespace Verbex.Repositories.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for document-term relationship operations.
    /// Provides operations for managing the inverted index mappings between documents and terms.
    /// </summary>
    public interface IDocumentTermMethods
    {
        /// <summary>
        /// Adds a document-term mapping.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="termId">Term ID.</param>
        /// <param name="termFrequency">Term frequency in document.</param>
        /// <param name="characterPositions">Character positions (absolute offsets) where the term appears.</param>
        /// <param name="termPositions">Term positions (word indices) where the term appears.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddAsync(string documentId, string termId, int termFrequency, List<int> characterPositions, List<int> termPositions, CancellationToken token = default);

        /// <summary>
        /// Batch adds document-term mappings.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="termMappings">List of (termId, termFrequency, characterPositions, termPositions) tuples.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AddBatchAsync(string documentId, List<(string TermId, int TermFrequency, List<int> CharacterPositions, List<int> TermPositions)> termMappings, CancellationToken token = default);

        /// <summary>
        /// Gets all terms for a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records.</returns>
        Task<List<DocumentTermRecord>> GetByDocumentAsync(string documentId, CancellationToken token = default);

        /// <summary>
        /// Gets all documents containing a term.
        /// </summary>
        /// <param name="termId">Term ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records.</returns>
        Task<List<DocumentTermRecord>> GetPostingsAsync(string termId, CancellationToken token = default);

        /// <summary>
        /// Gets all documents containing a term by term text.
        /// </summary>
        /// <param name="term">Term text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document-term records.</returns>
        Task<List<DocumentTermRecord>> GetPostingsByTermAsync(string term, CancellationToken token = default);

        /// <summary>
        /// Searches for documents containing specified terms.
        /// </summary>
        /// <param name="termIds">Term IDs to search for.</param>
        /// <param name="requireAll">If true, documents must contain all terms (AND). If false, any term (OR).</param>
        /// <param name="limit">Maximum results.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs with match information.</returns>
        Task<List<SearchMatch>> SearchAsync(IEnumerable<string> termIds, bool requireAll, int limit = 100, CancellationToken token = default);

        /// <summary>
        /// Searches for documents containing specified terms with optional label and tag filtering.
        /// </summary>
        /// <param name="termIds">Term IDs to search for.</param>
        /// <param name="requireAll">If true, documents must contain all terms (AND). If false, any term (OR).</param>
        /// <param name="labels">Optional list of labels to filter by (documents must have ALL labels).</param>
        /// <param name="tags">Optional dictionary of tags to filter by (documents must have ALL tags).</param>
        /// <param name="limit">Maximum results.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document IDs with match information.</returns>
        Task<List<SearchMatch>> SearchAsync(IEnumerable<string> termIds, bool requireAll, List<string>? labels, Dictionary<string, string>? tags, int limit = 100, CancellationToken token = default);

        /// <summary>
        /// Deletes all document-term mappings for a document.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of mappings deleted.</returns>
        Task<long> DeleteByDocumentAsync(string documentId, CancellationToken token = default);
    }
}
