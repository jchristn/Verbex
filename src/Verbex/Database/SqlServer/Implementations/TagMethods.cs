namespace Verbex.Database.SqlServer.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.Models;
    using Verbex.Utilities;

    using Sanitizer = Verbex.Database.SqlServer.Sanitizer;

    /// <summary>
    /// SQL Server implementation of tag methods.
    /// </summary>
    internal class TagMethods : ITagMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        public TagMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task SetAsync(string tablePrefix, string id, string? documentId, string key, string? value, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            DateTime now = DateTime.UtcNow;

            string existsQuery;
            if (documentId != null)
            {
                existsQuery = $"SELECT id FROM {prefix}_tags WHERE document_id = N'{Sanitizer.Sanitize(documentId)}' AND [key] = N'{Sanitizer.Sanitize(key)}';";
            }
            else
            {
                existsQuery = $"SELECT id FROM {prefix}_tags WHERE document_id IS NULL AND [key] = N'{Sanitizer.Sanitize(key)}';";
            }

            DataTable existsResult = await _Driver.ExecuteQueryAsync(existsQuery, false, token).ConfigureAwait(false);

            string query;
            if (existsResult.Rows.Count > 0)
            {
                string existingId = existsResult.Rows[0]["id"]?.ToString() ?? string.Empty;
                query = $"UPDATE {prefix}_tags SET value = {Sanitizer.FormatNullableString(value)}, last_update_utc = '{Sanitizer.FormatDateTime(now)}' WHERE id = N'{Sanitizer.Sanitize(existingId)}';";
            }
            else
            {
                query = $@"
INSERT INTO {prefix}_tags (id, document_id, [key], value, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', {Sanitizer.FormatNullableString(documentId)}, N'{Sanitizer.Sanitize(key)}', {Sanitizer.FormatNullableString(value)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
            }

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task AddBatchAsync(string tablePrefix, IEnumerable<TagRecord> records, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<TagRecord> recordList = records.ToList();
            if (recordList.Count == 0) return;

            const int ChunkSize = 200;
            DateTime now = DateTime.UtcNow;
            string nowFormatted = Sanitizer.FormatDateTime(now);

            for (int i = 0; i < recordList.Count; i += ChunkSize)
            {
                List<TagRecord> chunk = recordList.Skip(i).Take(ChunkSize).ToList();
                StringBuilder sb = new StringBuilder();
                sb.Append($"MERGE INTO {prefix}_tags WITH (HOLDLOCK) AS target USING (VALUES ");

                List<string> valuesClauses = new List<string>();
                foreach (TagRecord record in chunk)
                {
                    valuesClauses.Add($"(N'{Sanitizer.Sanitize(record.Id)}', {Sanitizer.FormatNullableString(record.DocumentId)}, N'{Sanitizer.Sanitize(record.Key)}', {Sanitizer.FormatNullableString(record.Value)}, '{nowFormatted}', '{nowFormatted}')");
                }

                sb.Append(string.Join(", ", valuesClauses));
                sb.Append(") AS source (id, document_id, [key], value, last_update_utc, created_utc) ");
                sb.Append("ON target.document_id = source.document_id AND target.[key] = source.[key] ");
                sb.Append("WHEN MATCHED THEN UPDATE SET value = source.value, last_update_utc = source.last_update_utc ");
                sb.Append("WHEN NOT MATCHED THEN INSERT (id, document_id, [key], value, last_update_utc, created_utc) VALUES (source.id, source.document_id, source.[key], source.value, source.last_update_utc, source.created_utc);");

                await _Driver.ExecuteQueryAsync(sb.ToString(), true, token).ConfigureAwait(false);
            }
        }

        public async Task<string?> GetAsync(string tablePrefix, string documentId, string key, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT value FROM {prefix}_tags WHERE document_id = N'{Sanitizer.Sanitize(documentId)}' AND [key] = N'{Sanitizer.Sanitize(key)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0 ? dt.Rows[0]["value"]?.ToString() : null;
        }

        public async Task<Dictionary<string, string>> GetByDocumentAsync(string tablePrefix, string documentId, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT [key], value FROM {prefix}_tags WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (DataRow row in dt.Rows)
            {
                string k = row["key"]?.ToString() ?? string.Empty;
                string v = row["value"]?.ToString() ?? string.Empty;
                result[k] = v;
            }
            return result;
        }

        public async Task<Dictionary<string, string>> GetIndexTagsAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT [key], value FROM {prefix}_tags WHERE document_id IS NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (DataRow row in dt.Rows)
            {
                string k = row["key"]?.ToString() ?? string.Empty;
                string v = row["value"]?.ToString() ?? string.Empty;
                result[k] = v;
            }
            return result;
        }

        public async Task<List<string>> GetAllDistinctKeysAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT DISTINCT [key] FROM {prefix}_tags;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["key"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetDocumentsByKeyAsync(string tablePrefix, string key, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT DISTINCT document_id FROM {prefix}_tags WHERE [key] = N'{Sanitizer.Sanitize(key)}' AND document_id IS NOT NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["document_id"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetDocumentsByTagAsync(string tablePrefix, string key, string value, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT DISTINCT document_id FROM {prefix}_tags WHERE [key] = N'{Sanitizer.Sanitize(key)}' AND value = N'{Sanitizer.Sanitize(value)}' AND document_id IS NOT NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["document_id"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<bool> ExistsAsync(string tablePrefix, string documentId, string key, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT TOP 1 1 FROM {prefix}_tags WHERE document_id = N'{Sanitizer.Sanitize(documentId)}' AND [key] = N'{Sanitizer.Sanitize(key)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0;
        }

        public async Task<bool> RemoveAsync(string tablePrefix, string documentId, string key, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            bool exists = await ExistsAsync(tablePrefix, documentId, key, token).ConfigureAwait(false);
            if (!exists) return false;

            string query = $"DELETE FROM {prefix}_tags WHERE document_id = N'{Sanitizer.Sanitize(documentId)}' AND [key] = N'{Sanitizer.Sanitize(key)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> RemoveIndexTagAsync(string tablePrefix, string key, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string checkQuery = $"SELECT TOP 1 1 FROM {prefix}_tags WHERE document_id IS NULL AND [key] = N'{Sanitizer.Sanitize(key)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(checkQuery, false, token).ConfigureAwait(false);
            if (dt.Rows.Count == 0) return false;

            string query = $"DELETE FROM {prefix}_tags WHERE document_id IS NULL AND [key] = N'{Sanitizer.Sanitize(key)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        public async Task<long> RemoveAllAsync(string tablePrefix, string documentId, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string countQuery = $"SELECT COUNT(*) FROM {prefix}_tags WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM {prefix}_tags WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task ReplaceAsync(string tablePrefix, string documentId, IDictionary<string, string> tags, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            await RemoveAllAsync(tablePrefix, documentId, token).ConfigureAwait(false);
            foreach (KeyValuePair<string, string> kvp in tags)
            {
                string id = IdGenerator.GenerateTagId();
                await SetAsync(tablePrefix, id, documentId, kvp.Key, kvp.Value, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string countQuery = $"SELECT COUNT(*) FROM {prefix}_tags;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM {prefix}_tags;";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #region Tenant Tags

        public async Task<Dictionary<string, string>> GetTenantTagsAsync(string tenantId, CancellationToken token = default)
        {
            string query = $"SELECT [key], value FROM tags WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (DataRow row in dt.Rows)
            {
                string k = row["key"]?.ToString() ?? string.Empty;
                string v = row["value"]?.ToString() ?? string.Empty;
                result[k] = v;
            }
            return result;
        }

        public async Task ReplaceTenantTagsAsync(string tenantId, IDictionary<string, string> tags, CancellationToken token = default)
        {
            await DeleteAllTenantTagsAsync(tenantId, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            foreach (KeyValuePair<string, string> kvp in tags)
            {
                string id = IdGenerator.GenerateTagId();
                string query = $@"
INSERT INTO tags (id, tenant_id, [key], value, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', N'{Sanitizer.Sanitize(tenantId)}', N'{Sanitizer.Sanitize(kvp.Key)}', {Sanitizer.FormatNullableString(kvp.Value)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllTenantTagsAsync(string tenantId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM tags WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM tags WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion

        #region User Tags

        public async Task<Dictionary<string, string>> GetUserTagsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string query = $"SELECT [key], value FROM tags WHERE user_id = N'{Sanitizer.Sanitize(userId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (DataRow row in dt.Rows)
            {
                string k = row["key"]?.ToString() ?? string.Empty;
                string v = row["value"]?.ToString() ?? string.Empty;
                result[k] = v;
            }
            return result;
        }

        public async Task ReplaceUserTagsAsync(string tenantId, string userId, IDictionary<string, string> tags, CancellationToken token = default)
        {
            await DeleteAllUserTagsAsync(tenantId, userId, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            foreach (KeyValuePair<string, string> kvp in tags)
            {
                string id = IdGenerator.GenerateTagId();
                string query = $@"
INSERT INTO tags (id, user_id, [key], value, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', N'{Sanitizer.Sanitize(userId)}', N'{Sanitizer.Sanitize(kvp.Key)}', {Sanitizer.FormatNullableString(kvp.Value)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllUserTagsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM tags WHERE user_id = N'{Sanitizer.Sanitize(userId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM tags WHERE user_id = N'{Sanitizer.Sanitize(userId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion

        #region Credential Tags

        public async Task<Dictionary<string, string>> GetCredentialTagsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            string query = $"SELECT [key], value FROM tags WHERE credential_id = N'{Sanitizer.Sanitize(credentialId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (DataRow row in dt.Rows)
            {
                string k = row["key"]?.ToString() ?? string.Empty;
                string v = row["value"]?.ToString() ?? string.Empty;
                result[k] = v;
            }
            return result;
        }

        public async Task ReplaceCredentialTagsAsync(string tenantId, string credentialId, IDictionary<string, string> tags, CancellationToken token = default)
        {
            await DeleteAllCredentialTagsAsync(tenantId, credentialId, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            foreach (KeyValuePair<string, string> kvp in tags)
            {
                string id = IdGenerator.GenerateTagId();
                string query = $@"
INSERT INTO tags (id, credential_id, [key], value, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', N'{Sanitizer.Sanitize(credentialId)}', N'{Sanitizer.Sanitize(kvp.Key)}', {Sanitizer.FormatNullableString(kvp.Value)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllCredentialTagsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM tags WHERE credential_id = N'{Sanitizer.Sanitize(credentialId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM tags WHERE credential_id = N'{Sanitizer.Sanitize(credentialId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion
    }
}
