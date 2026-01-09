namespace Verbex.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.Models;
    using Verbex.Utilities;

    /// <summary>
    /// PostgreSQL implementation of user methods.
    /// </summary>
    internal class UserMethods : IUserMethods
    {
        private readonly PostgresqlDatabaseDriver _Driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMethods"/> class.
        /// </summary>
        /// <param name="driver">The database driver.</param>
        public UserMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (string.IsNullOrEmpty(user.Identifier))
            {
                user.Identifier = IdGenerator.GenerateUserId();
            }

            user.CreatedUtc = DateTime.UtcNow;
            user.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
INSERT INTO users (identifier, tenant_id, email, password_sha256, first_name, last_name, is_admin, active, created_utc, last_update_utc)
VALUES (
    '{Sanitizer.Sanitize(user.Identifier)}',
    '{Sanitizer.Sanitize(user.TenantId)}',
    '{Sanitizer.Sanitize(user.Email)}',
    '{Sanitizer.Sanitize(user.PasswordSha256)}',
    {Sanitizer.FormatNullableString(user.FirstName)},
    {Sanitizer.FormatNullableString(user.LastName)},
    {Sanitizer.FormatBoolean(user.IsAdmin)},
    {Sanitizer.FormatBoolean(user.Active)},
    '{Sanitizer.FormatDateTime(user.CreatedUtc)}',
    '{Sanitizer.FormatDateTime(user.LastUpdateUtc)}'
);";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return user;
        }

        /// <inheritdoc />
        public async Task<UserMaster?> ReadByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            string query = $@"
SELECT identifier, tenant_id, email, password_sha256, first_name, last_name, is_admin, active, created_utc, last_update_utc
FROM users
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND identifier = '{Sanitizer.Sanitize(identifier)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToUser(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<UserMaster?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(email))
            {
                return null;
            }

            string query = $@"
SELECT identifier, tenant_id, email, password_sha256, first_name, last_name, is_admin, active, created_utc, last_update_utc
FROM users
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND email = '{Sanitizer.Sanitize(email)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToUser(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<UserMaster>> ReadManyAsync(string tenantId, int limit = 100, int offset = 0, bool activeOnly = false, CancellationToken token = default)
        {
            string whereClause = $"WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}'";
            if (activeOnly)
            {
                whereClause += " AND active = TRUE";
            }

            string query = $@"
SELECT identifier, tenant_id, email, password_sha256, first_name, last_name, is_admin, active, created_utc, last_update_utc
FROM users
{whereClause}
ORDER BY created_utc DESC
LIMIT {limit} OFFSET {offset};";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<UserMaster> users = new List<UserMaster>();
            foreach (DataRow row in result.Rows)
            {
                users.Add(MapRowToUser(row));
            }

            return users;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<UserMaster> ReadAllAsync(string tenantId, bool activeOnly = false, [EnumeratorCancellation] CancellationToken token = default)
        {
            string whereClause = $"WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}'";
            if (activeOnly)
            {
                whereClause += " AND active = TRUE";
            }

            string query = $@"
SELECT identifier, tenant_id, email, password_sha256, first_name, last_name, is_admin, active, created_utc, last_update_utc
FROM users
{whereClause}
ORDER BY created_utc DESC;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            foreach (DataRow row in result.Rows)
            {
                token.ThrowIfCancellationRequested();
                yield return MapRowToUser(row);
            }
        }

        /// <inheritdoc />
        public async Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
UPDATE users SET
    email = '{Sanitizer.Sanitize(user.Email)}',
    password_sha256 = '{Sanitizer.Sanitize(user.PasswordSha256)}',
    first_name = {Sanitizer.FormatNullableString(user.FirstName)},
    last_name = {Sanitizer.FormatNullableString(user.LastName)},
    is_admin = {Sanitizer.FormatBoolean(user.IsAdmin)},
    active = {Sanitizer.FormatBoolean(user.Active)},
    last_update_utc = '{Sanitizer.FormatDateTime(user.LastUpdateUtc)}'
WHERE tenant_id = '{Sanitizer.Sanitize(user.TenantId)}' AND identifier = '{Sanitizer.Sanitize(user.Identifier)}';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return user;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            string countQuery = $"SELECT COUNT(*) FROM users WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND identifier = '{Sanitizer.Sanitize(identifier)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            bool exists = countResult.Rows.Count > 0 && Convert.ToInt64(countResult.Rows[0][0]) > 0;

            if (!exists)
            {
                return false;
            }

            string query = $"DELETE FROM users WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND identifier = '{Sanitizer.Sanitize(identifier)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc />
        public async Task<long> DeleteByTenantAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return 0;
            }

            string countQuery = $"SELECT COUNT(*) FROM users WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM users WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            string query = $"SELECT 1 FROM users WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND identifier = '{Sanitizer.Sanitize(identifier)}' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(email))
            {
                return false;
            }

            string query = $"SELECT 1 FROM users WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND email = '{Sanitizer.Sanitize(email)}' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<long> GetRecordCountAsync(string tenantId, bool activeOnly = false, CancellationToken token = default)
        {
            string whereClause = $"WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}'";
            if (activeOnly)
            {
                whereClause += " AND active = TRUE";
            }

            string query = $"SELECT COUNT(*) FROM users {whereClause};";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count > 0)
            {
                object? value = result.Rows[0][0];
                if (value != null && value != DBNull.Value)
                {
                    return Convert.ToInt64(value);
                }
            }

            return 0;
        }

        private static UserMaster MapRowToUser(DataRow row)
        {
            UserMaster user = new UserMaster
            {
                Identifier = row["identifier"]?.ToString() ?? string.Empty,
                TenantId = row["tenant_id"]?.ToString() ?? string.Empty,
                Email = row["email"]?.ToString() ?? string.Empty,
                PasswordSha256 = row["password_sha256"]?.ToString() ?? string.Empty,
                FirstName = row["first_name"]?.ToString() ?? string.Empty,
                LastName = row["last_name"]?.ToString() ?? string.Empty,
                IsAdmin = Convert.ToBoolean(row["is_admin"]),
                Active = Convert.ToBoolean(row["active"]),
                CreatedUtc = DateTime.Parse(row["created_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o")),
                LastUpdateUtc = DateTime.Parse(row["last_update_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o"))
            };
            return user;
        }
    }
}
