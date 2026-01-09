namespace Verbex.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;

    /// <summary>
    /// Interface for administrator-related database operations.
    /// </summary>
    /// <remarks>
    /// Provides CRUD operations for global administrators.
    /// Administrators have system-wide privileges and can manage all tenants.
    /// </remarks>
    public interface IAdministratorMethods
    {
        /// <summary>
        /// Creates a new administrator.
        /// </summary>
        /// <param name="administrator">The administrator to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created administrator with populated identifier and timestamps.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when administrator is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when an administrator with the same email already exists.</exception>
        Task<Administrator> CreateAsync(Administrator administrator, CancellationToken token = default);

        /// <summary>
        /// Gets an administrator by its unique identifier.
        /// </summary>
        /// <param name="identifier">The administrator identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The administrator, or null if not found.</returns>
        Task<Administrator?> ReadByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Gets an administrator by its email address.
        /// </summary>
        /// <param name="email">The administrator's email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The administrator, or null if not found.</returns>
        Task<Administrator?> ReadByEmailAsync(string email, CancellationToken token = default);

        /// <summary>
        /// Gets all administrators with optional pagination.
        /// </summary>
        /// <param name="limit">Maximum number of administrators to return. Default is 100.</param>
        /// <param name="offset">Number of administrators to skip. Default is 0.</param>
        /// <param name="activeOnly">If true, only return active administrators. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of administrators ordered by creation date descending.</returns>
        Task<List<Administrator>> ReadManyAsync(int limit = 100, int offset = 0, bool activeOnly = false, CancellationToken token = default);

        /// <summary>
        /// Streams administrators as an async enumerable.
        /// </summary>
        /// <param name="activeOnly">If true, only return active administrators. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An async enumerable of administrators.</returns>
        IAsyncEnumerable<Administrator> ReadAllAsync(bool activeOnly = false, CancellationToken token = default);

        /// <summary>
        /// Updates an existing administrator.
        /// </summary>
        /// <param name="administrator">The administrator with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated administrator.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when administrator is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the administrator does not exist.</exception>
        Task<Administrator> UpdateAsync(Administrator administrator, CancellationToken token = default);

        /// <summary>
        /// Deletes an administrator by its identifier.
        /// </summary>
        /// <param name="identifier">The administrator identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the administrator was deleted, false if not found.</returns>
        Task<bool> DeleteByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Checks if an administrator exists by its identifier.
        /// </summary>
        /// <param name="identifier">The administrator identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the administrator exists.</returns>
        Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Checks if an administrator exists by its email address.
        /// </summary>
        /// <param name="email">The administrator's email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the administrator exists.</returns>
        Task<bool> ExistsByEmailAsync(string email, CancellationToken token = default);

        /// <summary>
        /// Gets the total number of administrators.
        /// </summary>
        /// <param name="activeOnly">If true, only count active administrators. Default is false.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The total administrator count.</returns>
        Task<long> GetRecordCountAsync(bool activeOnly = false, CancellationToken token = default);
    }
}
