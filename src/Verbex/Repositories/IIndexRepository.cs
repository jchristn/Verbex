namespace Verbex.Repositories
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Repositories.Interfaces;

    /// <summary>
    /// Interface for inverted index persistence operations.
    /// Implementations provide storage for documents, terms, and their relationships.
    /// This interface exposes segregated method interfaces for better organization and flexibility.
    /// </summary>
    public interface IIndexRepository : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets whether the repository is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        string IndexName { get; }   

        /// <summary>
        /// Gets the document operations interface.
        /// </summary>
        IDocumentMethods Document { get; }

        /// <summary>
        /// Gets the term operations interface.
        /// </summary>
        ITermMethods Term { get; }

        /// <summary>
        /// Gets the document-term operations interface.
        /// </summary>
        IDocumentTermMethods DocumentTerm { get; }

        /// <summary>
        /// Gets the label operations interface.
        /// </summary>
        ILabelMethods Label { get; }

        /// <summary>
        /// Gets the tag operations interface.
        /// </summary>
        ITagMethods Tag { get; }

        /// <summary>
        /// Gets the statistics operations interface.
        /// </summary>
        IStatisticsMethods Statistics { get; }

        /// <summary>
        /// Opens the repository connection and initializes the schema if needed.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task OpenAsync(string indexName, CancellationToken token = default);

        /// <summary>
        /// Closes the repository connection.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CloseAsync(CancellationToken token = default);

        /// <summary>
        /// Flushes any pending changes to persistent storage.
        /// For in-memory databases, this saves to the specified file.
        /// </summary>
        /// <param name="targetPath">Optional path to save to (for in-memory mode).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task FlushAsync(string? targetPath = null, CancellationToken token = default);
    }
}
