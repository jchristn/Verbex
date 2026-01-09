namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for user-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides CRUD operations for users within tenants.
    /// Users are scoped to a specific tenant and can have multiple credentials.
    /// </remarks>
    public interface IUserMethods
    {
        /// <summary>
        /// Creates a new user within a tenant.
        /// </summary>
        /// <param name="user">The user to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created user with populated identifier and timestamps.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when user is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when a user with the same email already exists in the tenant.</exception>
        Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>
        /// Gets a user by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user, or null if not found.</returns>
        Task<UserMaster?> ReadByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Gets a user by tenant and email.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="email">The user's email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user, or null if not found.</returns>
        Task<UserMaster?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>
        /// Gets all users within a tenant with optional pagination.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="limit">Maximum number of users to return. Default is 100.</param>
        /// <param name="offset">Number of users to skip. Default is 0.</param>
        /// <param name="activeOnly">If true, only return active users. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of users ordered by creation date descending.</returns>
        Task<List<UserMaster>> ReadManyAsync(string tenantId, int limit = 100, int offset = 0, bool activeOnly = false, CancellationToken token = default);

        /// <summary>
        /// Streams users within a tenant as an async enumerable.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="activeOnly">If true, only return active users. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An async enumerable of users.</returns>
        IAsyncEnumerable<UserMaster> ReadAllAsync(string tenantId, bool activeOnly = false, CancellationToken token = default);

        /// <summary>
        /// Updates an existing user.
        /// </summary>
        /// <param name="user">The user with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated user.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when user is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the user does not exist.</exception>
        Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>
        /// Deletes a user by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the user was deleted, false if not found.</returns>
        /// <remarks>
        /// This operation cascades to delete all credentials belonging to the user.
        /// </remarks>
        Task<bool> DeleteByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Deletes all users within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of users deleted.</returns>
        /// <remarks>
        /// This operation cascades to delete all credentials belonging to the deleted users.
        /// </remarks>
        Task<long> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Checks if a user exists by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="identifier">The user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the user exists.</returns>
        Task<bool> ExistsByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default);

        /// <summary>
        /// Checks if a user exists by tenant and email.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="email">The user's email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the user exists.</returns>
        Task<bool> ExistsByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of users within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="activeOnly">If true, only count active users. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The total user count for the tenant.</returns>
        Task<long> GetRecordCountAsync(string tenantId, bool activeOnly = false, CancellationToken token = default);
    }
}
