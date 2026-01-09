namespace Verbex.Database.SqlServer.Implementations
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

    using Sanitizer = Verbex.Database.SqlServer.Sanitizer;

    /// <summary>
    /// SQL Server implementation of index methods.
    /// </summary>
    internal class IndexMethods : IIndexMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexMethods"/> class.
        /// </summary>
        /// <param name="driver">The database driver.</param>
        public IndexMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<IndexMetadata> CreateAsync(IndexMetadata index, CancellationToken token = default)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            if (string.IsNullOrEmpty(index.Identifier))
            {
                index.Identifier = IdGenerator.GenerateIndexMetadataId();
            }

            index.CreatedUtc = DateTime.UtcNow;
            index.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
INSERT INTO indexes (identifier, tenant_id, name, description, created_utc, last_update_utc)
VALUES (
    N'{Sanitizer.Sanitize(index.Identifier)}',
    N'{Sanitizer.Sanitize(index.TenantId)}',
    N'{Sanitizer.Sanitize(index.Name)}',
    {Sanitizer.FormatNullableString(index.Description)},
    '{Sanitizer.FormatDateTime(index.CreatedUtc)}',
    '{Sanitizer.FormatDateTime(index.LastUpdateUtc)}'
);";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return index;
        }

        /// <inheritdoc />
        public async Task<IndexMetadata?> ReadByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            string query = $@"
SELECT identifier, tenant_id, name, description, created_utc, last_update_utc
FROM indexes
WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND identifier = N'{Sanitizer.Sanitize(identifier)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToIndex(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<IndexMetadata?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(name))
            {
                return null;
            }

            string query = $@"
SELECT identifier, tenant_id, name, description, created_utc, last_update_utc
FROM indexes
WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND name = N'{Sanitizer.Sanitize(name)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToIndex(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<IndexMetadata>> ReadManyAsync(string tenantId, int limit = 100, int offset = 0, CancellationToken token = default)
        {
            string query = $@"
SELECT identifier, tenant_id, name, description, created_utc, last_update_utc
FROM indexes
WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}'
ORDER BY created_utc DESC
OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<IndexMetadata> indexes = new List<IndexMetadata>();
            foreach (DataRow row in result.Rows)
            {
                indexes.Add(MapRowToIndex(row));
            }

            return indexes;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IndexMetadata> ReadAllAsync(string tenantId, [EnumeratorCancellation] CancellationToken token = default)
        {
            string query = $@"
SELECT identifier, tenant_id, name, description, created_utc, last_update_utc
FROM indexes
WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}'
ORDER BY created_utc DESC;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            foreach (DataRow row in result.Rows)
            {
                token.ThrowIfCancellationRequested();
                yield return MapRowToIndex(row);
            }
        }

        /// <inheritdoc />
        public async Task<IndexMetadata> UpdateAsync(IndexMetadata index, CancellationToken token = default)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            index.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
UPDATE indexes SET
    name = N'{Sanitizer.Sanitize(index.Name)}',
    description = {Sanitizer.FormatNullableString(index.Description)},
    last_update_utc = '{Sanitizer.FormatDateTime(index.LastUpdateUtc)}'
WHERE tenant_id = N'{Sanitizer.Sanitize(index.TenantId)}' AND identifier = N'{Sanitizer.Sanitize(index.Identifier)}';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return index;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            string countQuery = $"SELECT COUNT(*) FROM indexes WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND identifier = N'{Sanitizer.Sanitize(identifier)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            bool exists = countResult.Rows.Count > 0 && Convert.ToInt64(countResult.Rows[0][0]) > 0;

            if (!exists)
            {
                return false;
            }

            string query = $"DELETE FROM indexes WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND identifier = N'{Sanitizer.Sanitize(identifier)}';";
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

            string countQuery = $"SELECT COUNT(*) FROM indexes WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM indexes WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}';";
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

            string query = $"SELECT TOP 1 1 FROM indexes WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND identifier = N'{Sanitizer.Sanitize(identifier)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(name))
            {
                return false;
            }

            string query = $"SELECT TOP 1 1 FROM indexes WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND name = N'{Sanitizer.Sanitize(name)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<long> GetRecordCountAsync(string tenantId, CancellationToken token = default)
        {
            string query = $"SELECT COUNT(*) FROM indexes WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}';";
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

        private static IndexMetadata MapRowToIndex(DataRow row)
        {
            return new IndexMetadata
            {
                Identifier = row["identifier"]?.ToString() ?? string.Empty,
                TenantId = row["tenant_id"]?.ToString() ?? string.Empty,
                Name = row["name"]?.ToString() ?? string.Empty,
                Description = row["description"]?.ToString() ?? string.Empty,
                CreatedUtc = DateTime.Parse(row["created_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o")),
                LastUpdateUtc = DateTime.Parse(row["last_update_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o"))
            };
        }
    }
}
