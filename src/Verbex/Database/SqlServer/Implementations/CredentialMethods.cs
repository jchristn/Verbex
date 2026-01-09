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
    /// SQL Server implementation of credential methods.
    /// </summary>
    internal class CredentialMethods : ICredentialMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="CredentialMethods"/> class.
        /// </summary>
        /// <param name="driver">The database driver.</param>
        public CredentialMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<Credential> CreateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            if (string.IsNullOrEmpty(credential.Identifier))
            {
                credential.Identifier = IdGenerator.GenerateCredentialId();
            }

            credential.CreatedUtc = DateTime.UtcNow;
            credential.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
INSERT INTO credentials (identifier, tenant_id, user_id, bearer_token, name, active, created_utc, last_update_utc)
VALUES (
    N'{Sanitizer.Sanitize(credential.Identifier)}',
    N'{Sanitizer.Sanitize(credential.TenantId)}',
    N'{Sanitizer.Sanitize(credential.UserId)}',
    N'{Sanitizer.Sanitize(credential.BearerToken)}',
    {Sanitizer.FormatNullableString(credential.Name)},
    {Sanitizer.FormatBoolean(credential.Active)},
    '{Sanitizer.FormatDateTime(credential.CreatedUtc)}',
    '{Sanitizer.FormatDateTime(credential.LastUpdateUtc)}'
);";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return credential;
        }

        /// <inheritdoc />
        public async Task<Credential?> ReadByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            string query = $@"
SELECT identifier, tenant_id, user_id, bearer_token, name, active, created_utc, last_update_utc
FROM credentials
WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND identifier = N'{Sanitizer.Sanitize(identifier)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToCredential(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<Credential?> ReadByBearerTokenAsync(string bearerToken, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(bearerToken))
            {
                return null;
            }

            string query = $@"
SELECT identifier, tenant_id, user_id, bearer_token, name, active, created_utc, last_update_utc
FROM credentials
WHERE bearer_token = N'{Sanitizer.Sanitize(bearerToken)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToCredential(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Credential>> ReadByUserAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
            {
                return new List<Credential>();
            }

            string query = $@"
SELECT identifier, tenant_id, user_id, bearer_token, name, active, created_utc, last_update_utc
FROM credentials
WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id = N'{Sanitizer.Sanitize(userId)}'
ORDER BY created_utc DESC;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<Credential> credentials = new List<Credential>();
            foreach (DataRow row in result.Rows)
            {
                credentials.Add(MapRowToCredential(row));
            }

            return credentials;
        }

        /// <inheritdoc />
        public async Task<List<Credential>> ReadManyAsync(string tenantId, int limit = 100, int offset = 0, bool activeOnly = false, CancellationToken token = default)
        {
            string whereClause = $"WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}'";
            if (activeOnly)
            {
                whereClause += " AND active = 1";
            }

            string query = $@"
SELECT identifier, tenant_id, user_id, bearer_token, name, active, created_utc, last_update_utc
FROM credentials
{whereClause}
ORDER BY created_utc DESC
OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<Credential> credentials = new List<Credential>();
            foreach (DataRow row in result.Rows)
            {
                credentials.Add(MapRowToCredential(row));
            }

            return credentials;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Credential> ReadAllAsync(string tenantId, bool activeOnly = false, [EnumeratorCancellation] CancellationToken token = default)
        {
            string whereClause = $"WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}'";
            if (activeOnly)
            {
                whereClause += " AND active = 1";
            }

            string query = $@"
SELECT identifier, tenant_id, user_id, bearer_token, name, active, created_utc, last_update_utc
FROM credentials
{whereClause}
ORDER BY created_utc DESC;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            foreach (DataRow row in result.Rows)
            {
                token.ThrowIfCancellationRequested();
                yield return MapRowToCredential(row);
            }
        }

        /// <inheritdoc />
        public async Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            credential.LastUpdateUtc = DateTime.UtcNow;

            string query = $@"
UPDATE credentials SET
    bearer_token = N'{Sanitizer.Sanitize(credential.BearerToken)}',
    name = {Sanitizer.FormatNullableString(credential.Name)},
    active = {Sanitizer.FormatBoolean(credential.Active)},
    last_update_utc = '{Sanitizer.FormatDateTime(credential.LastUpdateUtc)}'
WHERE tenant_id = N'{Sanitizer.Sanitize(credential.TenantId)}' AND identifier = N'{Sanitizer.Sanitize(credential.Identifier)}';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return credential;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdentifierAsync(string tenantId, string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            string countQuery = $"SELECT COUNT(*) FROM credentials WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND identifier = N'{Sanitizer.Sanitize(identifier)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            bool exists = countResult.Rows.Count > 0 && Convert.ToInt64(countResult.Rows[0][0]) > 0;

            if (!exists)
            {
                return false;
            }

            string query = $"DELETE FROM credentials WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND identifier = N'{Sanitizer.Sanitize(identifier)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc />
        public async Task<long> DeleteByUserAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
            {
                return 0;
            }

            string countQuery = $"SELECT COUNT(*) FROM credentials WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id = N'{Sanitizer.Sanitize(userId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM credentials WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id = N'{Sanitizer.Sanitize(userId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        /// <inheritdoc />
        public async Task<long> DeleteByTenantAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return 0;
            }

            string countQuery = $"SELECT COUNT(*) FROM credentials WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM credentials WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}';";
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

            string query = $"SELECT TOP 1 1 FROM credentials WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND identifier = N'{Sanitizer.Sanitize(identifier)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByBearerTokenAsync(string bearerToken, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(bearerToken))
            {
                return false;
            }

            string query = $"SELECT TOP 1 1 FROM credentials WHERE bearer_token = N'{Sanitizer.Sanitize(bearerToken)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<long> GetRecordCountAsync(string tenantId, bool activeOnly = false, CancellationToken token = default)
        {
            string whereClause = $"WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}'";
            if (activeOnly)
            {
                whereClause += " AND active = 1";
            }

            string query = $"SELECT COUNT(*) FROM credentials {whereClause};";
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

        private static Credential MapRowToCredential(DataRow row)
        {
            Credential credential = new Credential
            {
                Identifier = row["identifier"]?.ToString() ?? string.Empty,
                TenantId = row["tenant_id"]?.ToString() ?? string.Empty,
                UserId = row["user_id"]?.ToString() ?? string.Empty,
                BearerToken = row["bearer_token"]?.ToString() ?? string.Empty,
                Name = row["name"]?.ToString() ?? string.Empty,
                Active = Convert.ToBoolean(row["active"]),
                CreatedUtc = DateTime.Parse(row["created_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o")),
                LastUpdateUtc = DateTime.Parse(row["last_update_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o"))
            };
            return credential;
        }
    }
}
