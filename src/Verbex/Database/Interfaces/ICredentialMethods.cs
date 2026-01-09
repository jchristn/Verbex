namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for credential-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides CRUD operations for credentials (bearer tokens) associated with users.
    /// Credentials are scoped to a tenant and user, but bearer tokens are globally unique.
    /// </remarks>
    public interface ICredentialMethods
    {
        /// <summary>
        /// Creates a new credential.
        /// </summary>
        /// <param name="credential">The credential to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created credential with populated identifier and timestamps.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when credential is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the bearer token already exists.</exception>
        Task<Credential> CreateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Gets a credential by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credential, or null if not found.</returns>
        Task<Credential?> ReadByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Gets a credential by its bearer token (global lookup).
        /// </summary>
        /// <param name="bearerToken">The bearer token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credential, or null if not found.</returns>
        /// <remarks>
        /// This method searches across all tenants since bearer tokens are globally unique.
        /// This is the primary method used for authentication.
        /// </remarks>
        Task<Credential?> ReadByBearerTokenAsync(string bearerToken, CancellationToken token = default);

        /// <summary>
        /// Gets all credentials for a specific user.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of credentials belonging to the user.</returns>
        Task<List<Credential>> ReadByUserAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Gets all credentials within a tenant with optional pagination.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="limit">Maximum number of credentials to return. Default is 100.</param>
        /// <param name="offset">Number of credentials to skip. Default is 0.</param>
        /// <param name="activeOnly">If true, only return active credentials. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of credentials ordered by creation date descending.</returns>
        Task<List<Credential>> ReadManyAsync(string tenantId, int limit = 100, int offset = 0, bool activeOnly = false, CancellationToken token = default);

        /// <summary>
        /// Streams credentials within a tenant as an async enumerable.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="activeOnly">If true, only return active credentials. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An async enumerable of credentials.</returns>
        IAsyncEnumerable<Credential> ReadAllAsync(string tenantId, bool activeOnly = false, CancellationToken token = default);

        /// <summary>
        /// Updates an existing credential.
        /// </summary>
        /// <param name="credential">The credential with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated credential.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when credential is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the credential does not exist.</exception>
        Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Deletes a credential by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the credential was deleted, false if not found.</returns>
        Task<bool> DeleteByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Deletes all credentials for a specific user.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of credentials deleted.</returns>
        Task<long> DeleteByUserAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Deletes all credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of credentials deleted.</returns>
        Task<long> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Checks if a credential exists by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the credential exists.</returns>
        Task<bool> ExistsByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Checks if a bearer token exists (global check).
        /// </summary>
        /// <param name="bearerToken">The bearer token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the bearer token exists.</returns>
        Task<bool> ExistsByBearerTokenAsync(string bearerToken, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="activeOnly">If true, only count active credentials. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The total credential count for the tenant.</returns>
        Task<long> GetRecordCountAsync(string tenantId, bool activeOnly = false, CancellationToken token = default);
    }
}
