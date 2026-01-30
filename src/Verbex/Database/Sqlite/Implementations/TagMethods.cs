namespace Verbex.Database.Sqlite.Implementations
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

    using Sanitizer = Verbex.Database.Sqlite.Sanitizer;

    /// <summary>
    /// SQLite implementation of tag methods.
    /// </summary>
    internal class TagMethods : ITagMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        public TagMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task SetAsync(string tenantId, string indexId, string id, string? documentId, string key, string? value, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;

            string existsQuery;
            if (documentId != null)
            {
                existsQuery = $"SELECT id FROM tags WHERE document_id = '{Sanitizer.Sanitize(documentId)}' AND key = '{Sanitizer.Sanitize(key)}';";
            }
            else
            {
                existsQuery = $"SELECT id FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}' AND document_id IS NULL AND key = '{Sanitizer.Sanitize(key)}';";
            }

            DataTable existsResult = await _Driver.ExecuteQueryAsync(existsQuery, false, token).ConfigureAwait(false);

            string query;
            if (existsResult.Rows.Count > 0)
            {
                string existingId = existsResult.Rows[0]["id"]?.ToString() ?? string.Empty;
                query = $"UPDATE tags SET value = {Sanitizer.FormatNullableString(value)}, last_update_utc = '{Sanitizer.FormatDateTime(now)}' WHERE id = '{Sanitizer.Sanitize(existingId)}';";
            }
            else
            {
                query = $@"
INSERT INTO tags (id, document_id, index_id, key, value, last_update_utc, created_utc)
VALUES ('{Sanitizer.Sanitize(id)}', {Sanitizer.FormatNullableString(documentId)}, '{Sanitizer.Sanitize(indexId)}', '{Sanitizer.Sanitize(key)}', {Sanitizer.FormatNullableString(value)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
            }

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task AddBatchAsync(string tenantId, string indexId, IEnumerable<TagRecord> records, CancellationToken token = default)
        {
            List<TagRecord> recordList = records.ToList();
            if (recordList.Count == 0) return;

            const int ChunkSize = 200;
            DateTime now = DateTime.UtcNow;
            string nowFormatted = Sanitizer.FormatDateTime(now);

            for (int i = 0; i < recordList.Count; i += ChunkSize)
            {
                List<TagRecord> chunk = recordList.Skip(i).Take(ChunkSize).ToList();
                StringBuilder sb = new StringBuilder();
                sb.Append("INSERT OR REPLACE INTO tags (id, document_id, index_id, key, value, last_update_utc, created_utc) VALUES ");

                List<string> valuesClauses = new List<string>();
                foreach (TagRecord record in chunk)
                {
                    valuesClauses.Add($"('{Sanitizer.Sanitize(record.Id)}', {Sanitizer.FormatNullableString(record.DocumentId)}, '{Sanitizer.Sanitize(indexId)}', '{Sanitizer.Sanitize(record.Key)}', {Sanitizer.FormatNullableString(record.Value)}, '{nowFormatted}', '{nowFormatted}')");
                }

                sb.Append(string.Join(", ", valuesClauses));
                sb.Append(';');

                await _Driver.ExecuteQueryAsync(sb.ToString(), true, token).ConfigureAwait(false);
            }
        }

        public async Task<string?> GetAsync(string tenantId, string indexId, string documentId, string key, CancellationToken token = default)
        {
            string query = $"SELECT value FROM tags WHERE document_id = '{Sanitizer.Sanitize(documentId)}' AND key = '{Sanitizer.Sanitize(key)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0 ? dt.Rows[0]["value"]?.ToString() : null;
        }

        public async Task<Dictionary<string, string>> GetByDocumentAsync(string tenantId, string indexId, string documentId, CancellationToken token = default)
        {
            string query = $"SELECT key, value FROM tags WHERE document_id = '{Sanitizer.Sanitize(documentId)}';";
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

        public async Task<Dictionary<string, string>> GetIndexTagsAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string query = $"SELECT key, value FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}' AND document_id IS NULL;";
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

        public async Task<List<string>> GetAllDistinctKeysAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT key FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["key"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetDocumentsByKeyAsync(string tenantId, string indexId, string key, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT document_id FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}' AND key = '{Sanitizer.Sanitize(key)}' AND document_id IS NOT NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["document_id"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetDocumentsByTagAsync(string tenantId, string indexId, string key, string value, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT document_id FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}' AND key = '{Sanitizer.Sanitize(key)}' AND value = '{Sanitizer.Sanitize(value)}' AND document_id IS NOT NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["document_id"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<bool> ExistsAsync(string tenantId, string indexId, string documentId, string key, CancellationToken token = default)
        {
            string query = $"SELECT 1 FROM tags WHERE document_id = '{Sanitizer.Sanitize(documentId)}' AND key = '{Sanitizer.Sanitize(key)}' LIMIT 1;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0;
        }

        public async Task<bool> RemoveAsync(string tenantId, string indexId, string documentId, string key, CancellationToken token = default)
        {
            bool exists = await ExistsAsync(tenantId, indexId, documentId, key, token).ConfigureAwait(false);
            if (!exists) return false;

            string query = $"DELETE FROM tags WHERE document_id = '{Sanitizer.Sanitize(documentId)}' AND key = '{Sanitizer.Sanitize(key)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> RemoveIndexTagAsync(string tenantId, string indexId, string key, CancellationToken token = default)
        {
            string checkQuery = $"SELECT 1 FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}' AND document_id IS NULL AND key = '{Sanitizer.Sanitize(key)}' LIMIT 1;";
            DataTable dt = await _Driver.ExecuteQueryAsync(checkQuery, false, token).ConfigureAwait(false);
            if (dt.Rows.Count == 0) return false;

            string query = $"DELETE FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}' AND document_id IS NULL AND key = '{Sanitizer.Sanitize(key)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        public async Task<long> RemoveAllAsync(string tenantId, string indexId, string documentId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM tags WHERE document_id = '{Sanitizer.Sanitize(documentId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM tags WHERE document_id = '{Sanitizer.Sanitize(documentId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task ReplaceAsync(string tenantId, string indexId, string documentId, IDictionary<string, string> tags, CancellationToken token = default)
        {
            await RemoveAllAsync(tenantId, indexId, documentId, token).ConfigureAwait(false);
            foreach (KeyValuePair<string, string> kvp in tags)
            {
                string id = IdGenerator.GenerateTagId();
                await SetAsync(tenantId, indexId, id, documentId, kvp.Key, kvp.Value, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM tags WHERE index_id = '{Sanitizer.Sanitize(indexId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #region Tenant Tags

        public async Task<Dictionary<string, string>> GetTenantTagsAsync(string tenantId, CancellationToken token = default)
        {
            string query = $"SELECT key, value FROM tags WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
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
INSERT INTO tags (id, tenant_id, key, value, last_update_utc, created_utc)
VALUES ('{Sanitizer.Sanitize(id)}', '{Sanitizer.Sanitize(tenantId)}', '{Sanitizer.Sanitize(kvp.Key)}', {Sanitizer.FormatNullableString(kvp.Value)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllTenantTagsAsync(string tenantId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM tags WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM tags WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion

        #region User Tags

        public async Task<Dictionary<string, string>> GetUserTagsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string query = $"SELECT key, value FROM tags WHERE user_id = '{Sanitizer.Sanitize(userId)}';";
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
INSERT INTO tags (id, user_id, key, value, last_update_utc, created_utc)
VALUES ('{Sanitizer.Sanitize(id)}', '{Sanitizer.Sanitize(userId)}', '{Sanitizer.Sanitize(kvp.Key)}', {Sanitizer.FormatNullableString(kvp.Value)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllUserTagsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM tags WHERE user_id = '{Sanitizer.Sanitize(userId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM tags WHERE user_id = '{Sanitizer.Sanitize(userId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion

        #region Credential Tags

        public async Task<Dictionary<string, string>> GetCredentialTagsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            string query = $"SELECT key, value FROM tags WHERE credential_id = '{Sanitizer.Sanitize(credentialId)}';";
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
INSERT INTO tags (id, credential_id, key, value, last_update_utc, created_utc)
VALUES ('{Sanitizer.Sanitize(id)}', '{Sanitizer.Sanitize(credentialId)}', '{Sanitizer.Sanitize(kvp.Key)}', {Sanitizer.FormatNullableString(kvp.Value)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllCredentialTagsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM tags WHERE credential_id = '{Sanitizer.Sanitize(credentialId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM tags WHERE credential_id = '{Sanitizer.Sanitize(credentialId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion
    }
}
