namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for index-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides CRUD operations for indexes within tenants.
    /// Indexes contain documents and are the primary unit of search organization.
    /// </remarks>
    public interface IIndexMethods
    {
        /// <summary>
        /// Creates a new index within a tenant.
        /// </summary>
        /// <param name="index">The index to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created index with populated identifier and timestamps.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when index is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when an index with the same name already exists in the tenant.</exception>
        Task<IndexMetadata> CreateAsync(IndexMetadata index, CancellationToken token = default);

        /// <summary>
        /// Gets an index by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The index, or null if not found.</returns>
        Task<IndexMetadata?> ReadByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Gets an index by tenant and name.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="name">The index name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The index, or null if not found.</returns>
        Task<IndexMetadata?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Gets all indexes within a tenant with optional pagination.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="limit">Maximum number of indexes to return. Default is 100.</param>
        /// <param name="offset">Number of indexes to skip. Default is 0.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of indexes ordered by creation date descending.</returns>
        Task<List<IndexMetadata>> ReadManyAsync(string tenantId, int limit = 100, int offset = 0, CancellationToken token = default);

        /// <summary>
        /// Streams indexes within a tenant as an async enumerable.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An async enumerable of indexes.</returns>
        IAsyncEnumerable<IndexMetadata> ReadAllAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Updates an existing index.
        /// </summary>
        /// <param name="index">The index with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated index.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when index is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the index does not exist.</exception>
        Task<IndexMetadata> UpdateAsync(IndexMetadata index, CancellationToken token = default);

        /// <summary>
        /// Deletes an index by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the index was deleted, false if not found.</returns>
        /// <remarks>
        /// This operation cascades to delete all documents, terms, labels, and tags
        /// associated with the index.
        /// </remarks>
        Task<bool> DeleteByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Deletes all indexes within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of indexes deleted.</returns>
        Task<long> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Checks if an index exists by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the index exists.</returns>
        Task<bool> ExistsByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Checks if an index exists by tenant and name.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="name">The index name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the index exists.</returns>
        Task<bool> ExistsByNameAsync(string tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of indexes within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The total index count for the tenant.</returns>
        Task<long> GetRecordCountAsync(string tenantId, CancellationToken token = default);
    }
}
