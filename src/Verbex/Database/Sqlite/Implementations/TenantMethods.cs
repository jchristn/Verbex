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
    /// SQLite implementation of tenant methods.
    /// </summary>
    internal class TenantMethods : ITenantMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantMethods"/> class.
        /// </summary>
        /// <param name="driver">The database driver.</param>
        public TenantMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<TenantMetadata> CreateAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null)
            {
                throw new ArgumentNullException(nameof(tenant));
            }

            if (string.IsNullOrEmpty(tenant.Identifier))
            {
                tenant.Identifier = IdGenerator.GenerateTenantId();
            }

            tenant.CreatedUtc = DateTime.UtcNow;
            tenant.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
INSERT INTO tenants (identifier, name, description, active, created_utc, last_update_utc)
VALUES (
    '{Sanitizer.Sanitize(tenant.Identifier)}',
    '{Sanitizer.Sanitize(tenant.Name)}',
    {Sanitizer.FormatNullableString(tenant.Description)},
    {Sanitizer.FormatBoolean(tenant.Active)},
    '{Sanitizer.FormatDateTime(tenant.CreatedUtc)}',
    '{Sanitizer.FormatDateTime(tenant.LastUpdateUtc)}'
);";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return tenant;
        }

        /// <inheritdoc />
        public async Task<TenantMetadata?> ReadByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            string query = $@"
SELECT identifier, name, description, active, created_utc, last_update_utc
FROM tenants
WHERE identifier = '{Sanitizer.Sanitize(identifier)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToTenant(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<TenantMetadata?> ReadByNameAsync(string name, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            string query = $@"
SELECT identifier, name, description, active, created_utc, last_update_utc
FROM tenants
WHERE name = '{Sanitizer.Sanitize(name)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToTenant(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<TenantMetadata>> ReadManyAsync(int limit = 100, int offset = 0, bool activeOnly = false, CancellationToken token = default)
        {
            string whereClause = activeOnly ? "WHERE active = 1" : "";

            string query = $@"
SELECT identifier, name, description, active, created_utc, last_update_utc
FROM tenants
{whereClause}
ORDER BY created_utc DESC
LIMIT {limit} OFFSET {offset};";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<TenantMetadata> tenants = new List<TenantMetadata>();
            foreach (DataRow row in result.Rows)
            {
                tenants.Add(MapRowToTenant(row));
            }

            return tenants;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<TenantMetadata> ReadAllAsync(bool activeOnly = false, [EnumeratorCancellation] CancellationToken token = default)
        {
            string whereClause = activeOnly ? "WHERE active = 1" : "";

            string query = $@"
SELECT identifier, name, description, active, created_utc, last_update_utc
FROM tenants
{whereClause}
ORDER BY created_utc DESC;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            foreach (DataRow row in result.Rows)
            {
                token.ThrowIfCancellationRequested();
                yield return MapRowToTenant(row);
            }
        }

        /// <inheritdoc />
        public async Task<TenantMetadata> UpdateAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null)
            {
                throw new ArgumentNullException(nameof(tenant));
            }

            tenant.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
UPDATE tenants SET
    name = '{Sanitizer.Sanitize(tenant.Name)}',
    description = {Sanitizer.FormatNullableString(tenant.Description)},
    active = {Sanitizer.FormatBoolean(tenant.Active)},
    last_update_utc = '{Sanitizer.FormatDateTime(tenant.LastUpdateUtc)}'
WHERE identifier = '{Sanitizer.Sanitize(tenant.Identifier)}';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return tenant;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            string query = $@"
DELETE FROM tenants WHERE identifier = '{Sanitizer.Sanitize(identifier)}';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            // Must use write connection (isTransaction = true) because changes()
            // returns the row count from the most recent write on the SAME connection
            string checkQuery = $@"
SELECT changes();";

            DataTable result = await _Driver.ExecuteQueryAsync(checkQuery, true, token).ConfigureAwait(false);

            if (result.Rows.Count > 0)
            {
                object? value = result.Rows[0][0];
                if (value != null && value != DBNull.Value)
                {
                    return Convert.ToInt64(value) > 0;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            string query = $@"
SELECT 1 FROM tenants WHERE identifier = '{Sanitizer.Sanitize(identifier)}' LIMIT 1;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByNameAsync(string name, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string query = $@"
SELECT 1 FROM tenants WHERE name = '{Sanitizer.Sanitize(name)}' LIMIT 1;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<long> GetRecordCountAsync(bool activeOnly = false, CancellationToken token = default)
        {
            string whereClause = activeOnly ? "WHERE active = 1" : "";

            string query = $@"
SELECT COUNT(*) FROM tenants {whereClause};";

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

        /// <summary>
        /// Maps a DataRow to a TenantMetadata object.
        /// </summary>
        private static TenantMetadata MapRowToTenant(DataRow row)
        {
            return new TenantMetadata
            {
                Identifier = row["identifier"]?.ToString() ?? string.Empty,
                Name = row["name"]?.ToString() ?? string.Empty,
                Description = row["description"]?.ToString() ?? string.Empty,
                Active = Convert.ToInt32(row["active"]) == 1,
                CreatedUtc = DateTime.Parse(row["created_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o")),
                LastUpdateUtc = DateTime.Parse(row["last_update_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o"))
            };
        }
    }
}
