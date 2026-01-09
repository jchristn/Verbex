namespace Verbex.Database.Sqlite.Implementations
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
    /// SQLite implementation of administrator methods.
    /// </summary>
    internal class AdministratorMethods : IAdministratorMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdministratorMethods"/> class.
        /// </summary>
        /// <param name="driver">The database driver.</param>
        public AdministratorMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<Administrator> CreateAsync(Administrator administrator, CancellationToken token = default)
        {
            if (administrator == null)
            {
                throw new ArgumentNullException(nameof(administrator));
            }

            if (string.IsNullOrEmpty(administrator.Identifier))
            {
                administrator.Identifier = IdGenerator.GenerateAdministratorId();
            }

            administrator.CreatedUtc = DateTime.UtcNow;
            administrator.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
INSERT INTO administrators (identifier, email, password_sha256, first_name, last_name, active, created_utc, last_update_utc)
VALUES (
    '{Sanitizer.Sanitize(administrator.Identifier)}',
    '{Sanitizer.Sanitize(administrator.Email)}',
    '{Sanitizer.Sanitize(administrator.PasswordSha256)}',
    {Sanitizer.FormatNullableString(administrator.FirstName)},
    {Sanitizer.FormatNullableString(administrator.LastName)},
    {Sanitizer.FormatBoolean(administrator.Active)},
    '{Sanitizer.FormatDateTime(administrator.CreatedUtc)}',
    '{Sanitizer.FormatDateTime(administrator.LastUpdateUtc)}'
);";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return administrator;
        }

        /// <inheritdoc />
        public async Task<Administrator?> ReadByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            string query = $@"
SELECT identifier, email, password_sha256, first_name, last_name, active, created_utc, last_update_utc
FROM administrators
WHERE identifier = '{Sanitizer.Sanitize(identifier)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToAdministrator(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<Administrator?> ReadByEmailAsync(string email, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(email))
            {
                return null;
            }

            string query = $@"
SELECT identifier, email, password_sha256, first_name, last_name, active, created_utc, last_update_utc
FROM administrators
WHERE email = '{Sanitizer.Sanitize(email)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToAdministrator(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Administrator>> ReadManyAsync(int limit = 100, int offset = 0, bool activeOnly = false, CancellationToken token = default)
        {
            string whereClause = activeOnly ? "WHERE active = 1" : "";

            string query = $@"
SELECT identifier, email, password_sha256, first_name, last_name, active, created_utc, last_update_utc
FROM administrators
{whereClause}
ORDER BY created_utc DESC
LIMIT {limit} OFFSET {offset};";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<Administrator> administrators = new List<Administrator>();
            foreach (DataRow row in result.Rows)
            {
                administrators.Add(MapRowToAdministrator(row));
            }

            return administrators;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Administrator> ReadAllAsync(bool activeOnly = false, [EnumeratorCancellation] CancellationToken token = default)
        {
            string whereClause = activeOnly ? "WHERE active = 1" : "";

            string query = $@"
SELECT identifier, email, password_sha256, first_name, last_name, active, created_utc, last_update_utc
FROM administrators
{whereClause}
ORDER BY created_utc DESC;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            foreach (DataRow row in result.Rows)
            {
                token.ThrowIfCancellationRequested();
                yield return MapRowToAdministrator(row);
            }
        }

        /// <inheritdoc />
        public async Task<Administrator> UpdateAsync(Administrator administrator, CancellationToken token = default)
        {
            if (administrator == null)
            {
                throw new ArgumentNullException(nameof(administrator));
            }

            administrator.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
UPDATE administrators SET
    email = '{Sanitizer.Sanitize(administrator.Email)}',
    password_sha256 = '{Sanitizer.Sanitize(administrator.PasswordSha256)}',
    first_name = {Sanitizer.FormatNullableString(administrator.FirstName)},
    last_name = {Sanitizer.FormatNullableString(administrator.LastName)},
    active = {Sanitizer.FormatBoolean(administrator.Active)},
    last_update_utc = '{Sanitizer.FormatDateTime(administrator.LastUpdateUtc)}'
WHERE identifier = '{Sanitizer.Sanitize(administrator.Identifier)}';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return administrator;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            string countQuery = $"SELECT COUNT(*) FROM administrators WHERE identifier = '{Sanitizer.Sanitize(identifier)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            bool exists = countResult.Rows.Count > 0 && Convert.ToInt64(countResult.Rows[0][0]) > 0;

            if (!exists)
            {
                return false;
            }

            string query = $"DELETE FROM administrators WHERE identifier = '{Sanitizer.Sanitize(identifier)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            string query = $"SELECT 1 FROM administrators WHERE identifier = '{Sanitizer.Sanitize(identifier)}' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByEmailAsync(string email, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(email))
            {
                return false;
            }

            string query = $"SELECT 1 FROM administrators WHERE email = '{Sanitizer.Sanitize(email)}' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<long> GetRecordCountAsync(bool activeOnly = false, CancellationToken token = default)
        {
            string whereClause = activeOnly ? "WHERE active = 1" : "";
            string query = $"SELECT COUNT(*) FROM administrators {whereClause};";
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

        private static Administrator MapRowToAdministrator(DataRow row)
        {
            Administrator admin = new Administrator
            {
                Identifier = row["identifier"]?.ToString() ?? string.Empty,
                Email = row["email"]?.ToString() ?? string.Empty,
                PasswordSha256 = row["password_sha256"]?.ToString() ?? string.Empty,
                FirstName = row["first_name"]?.ToString() ?? string.Empty,
                LastName = row["last_name"]?.ToString() ?? string.Empty,
                Active = Convert.ToInt32(row["active"]) == 1,
                CreatedUtc = DateTime.Parse(row["created_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o")),
                LastUpdateUtc = DateTime.Parse(row["last_update_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o"))
            };
            return admin;
        }
    }
}
