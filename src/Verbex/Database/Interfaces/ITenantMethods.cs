namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for tenant-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides CRUD operations for tenants in the multi-tenant system.
    /// Tenants are the top-level organizational unit for data isolation.
    /// </remarks>
    public interface ITenantMethods
    {
        /// <summary>
        /// Creates a new tenant.
        /// </summary>
        /// <param name="tenant">The tenant to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created tenant with populated identifier and timestamps.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when tenant is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when a tenant with the same name already exists.</exception>
        Task<TenantMetadata> CreateAsync(TenantMetadata tenant, CancellationToken token = default);

        /// <summary>
        /// Gets a tenant by its unique identifier.
        /// </summary>
        /// <param name="identifier">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tenant, or null if not found.</returns>
        Task<TenantMetadata?> ReadByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Gets a tenant by its name.
        /// </summary>
        /// <param name="name">The tenant name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tenant, or null if not found.</returns>
        Task<TenantMetadata?> ReadByNameAsync(string name, CancellationToken token = default);

        /// <summary>
        /// Gets all tenants with optional pagination.
        /// </summary>
        /// <param name="limit">Maximum number of tenants to return. Default is 100.</param>
        /// <param name="offset">Number of tenants to skip. Default is 0.</param>
        /// <param name="activeOnly">If true, only return active tenants. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of tenants ordered by creation date descending.</returns>
        Task<List<TenantMetadata>> ReadManyAsync(int limit = 100, int offset = 0, bool activeOnly = false, CancellationToken token = default);

        /// <summary>
        /// Streams tenants as an async enumerable.
        /// </summary>
        /// <param name="activeOnly">If true, only return active tenants. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An async enumerable of tenants.</returns>
        IAsyncEnumerable<TenantMetadata> ReadAllAsync(bool activeOnly = false, CancellationToken token = default);

        /// <summary>
        /// Updates an existing tenant.
        /// </summary>
        /// <param name="tenant">The tenant with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated tenant.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when tenant is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the tenant does not exist.</exception>
        Task<TenantMetadata> UpdateAsync(TenantMetadata tenant, CancellationToken token = default);

        /// <summary>
        /// Deletes a tenant by its identifier.
        /// </summary>
        /// <param name="identifier">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tenant was deleted, false if not found.</returns>
        /// <remarks>
        /// This operation cascades to delete all users, credentials, indexes, documents,
        /// and all associated data within the tenant.
        /// </remarks>
        Task<bool> DeleteByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Checks if a tenant exists by its identifier.
        /// </summary>
        /// <param name="identifier">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tenant exists.</returns>
        Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Checks if a tenant exists by its name.
        /// </summary>
        /// <param name="name">The tenant name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tenant exists.</returns>
        Task<bool> ExistsByNameAsync(string name, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of tenants.
        /// </summary>
        /// <param name="activeOnly">If true, only count active tenants. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The total tenant count.</returns>
        Task<long> GetRecordCountAsync(bool activeOnly = false, CancellationToken token = default);
    }
}
