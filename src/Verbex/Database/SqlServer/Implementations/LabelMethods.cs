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
    /// SQL Server implementation of label methods.
    /// </summary>
    internal class LabelMethods : ILabelMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        public LabelMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task AddAsync(string tenantId, string indexId, string id, string? documentId, string label, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string query = $@"
INSERT INTO labels (id, document_id, index_id, label, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', {Sanitizer.FormatNullableString(documentId)}, N'{Sanitizer.Sanitize(indexId)}', N'{Sanitizer.Sanitize(label)}', '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task AddBatchAsync(string tenantId, string indexId, IEnumerable<LabelRecord> records, CancellationToken token = default)
        {
            List<LabelRecord> recordList = records.ToList();
            if (recordList.Count == 0) return;

            const int ChunkSize = 200;
            DateTime now = DateTime.UtcNow;
            string nowFormatted = Sanitizer.FormatDateTime(now);

            for (int i = 0; i < recordList.Count; i += ChunkSize)
            {
                List<LabelRecord> chunk = recordList.Skip(i).Take(ChunkSize).ToList();
                StringBuilder sb = new StringBuilder();
                sb.Append("INSERT INTO labels (id, document_id, index_id, label, last_update_utc, created_utc) VALUES ");

                List<string> valuesClauses = new List<string>();
                foreach (LabelRecord record in chunk)
                {
                    valuesClauses.Add($"(N'{Sanitizer.Sanitize(record.Id)}', {Sanitizer.FormatNullableString(record.DocumentId)}, N'{Sanitizer.Sanitize(indexId)}', N'{Sanitizer.Sanitize(record.Label)}', '{nowFormatted}', '{nowFormatted}')");
                }

                sb.Append(string.Join(", ", valuesClauses));
                sb.Append(';');

                await _Driver.ExecuteQueryAsync(sb.ToString(), true, token).ConfigureAwait(false);
            }
        }

        public async Task<List<string>> GetByDocumentAsync(string tenantId, string indexId, string documentId, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT label FROM labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetIndexLabelsAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT label FROM labels WHERE index_id = N'{Sanitizer.Sanitize(indexId)}' AND document_id IS NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetAllDistinctAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT label FROM labels WHERE index_id = N'{Sanitizer.Sanitize(indexId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetDocumentsByLabelAsync(string tenantId, string indexId, string label, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT document_id FROM labels WHERE index_id = N'{Sanitizer.Sanitize(indexId)}' AND label = N'{Sanitizer.Sanitize(label)}' AND document_id IS NOT NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["document_id"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<bool> ExistsAsync(string tenantId, string indexId, string documentId, string label, CancellationToken token = default)
        {
            string query = $"SELECT TOP 1 1 FROM labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}' AND label = N'{Sanitizer.Sanitize(label)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0;
        }

        public async Task<bool> RemoveAsync(string tenantId, string indexId, string documentId, string label, CancellationToken token = default)
        {
            bool exists = await ExistsAsync(tenantId, indexId, documentId, label, token).ConfigureAwait(false);
            if (!exists) return false;

            string query = $"DELETE FROM labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}' AND label = N'{Sanitizer.Sanitize(label)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> RemoveIndexLabelAsync(string tenantId, string indexId, string label, CancellationToken token = default)
        {
            string checkQuery = $"SELECT TOP 1 1 FROM labels WHERE index_id = N'{Sanitizer.Sanitize(indexId)}' AND document_id IS NULL AND label = N'{Sanitizer.Sanitize(label)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(checkQuery, false, token).ConfigureAwait(false);
            if (dt.Rows.Count == 0) return false;

            string query = $"DELETE FROM labels WHERE index_id = N'{Sanitizer.Sanitize(indexId)}' AND document_id IS NULL AND label = N'{Sanitizer.Sanitize(label)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        public async Task<long> RemoveAllAsync(string tenantId, string indexId, string documentId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task ReplaceAsync(string tenantId, string indexId, string documentId, IEnumerable<string> labels, CancellationToken token = default)
        {
            await RemoveAllAsync(tenantId, indexId, documentId, token).ConfigureAwait(false);
            foreach (string label in labels)
            {
                string id = IdGenerator.GenerateLabelId();
                await AddAsync(tenantId, indexId, id, documentId, label, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM labels WHERE index_id = N'{Sanitizer.Sanitize(indexId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM labels WHERE index_id = N'{Sanitizer.Sanitize(indexId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #region Tenant Labels

        public async Task<List<string>> GetTenantLabelsAsync(string tenantId, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT label FROM labels WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task ReplaceTenantLabelsAsync(string tenantId, IEnumerable<string> labels, CancellationToken token = default)
        {
            await DeleteAllTenantLabelsAsync(tenantId, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            foreach (string label in labels)
            {
                string id = IdGenerator.GenerateLabelId();
                string query = $@"
INSERT INTO labels (id, tenant_id, label, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', N'{Sanitizer.Sanitize(tenantId)}', N'{Sanitizer.Sanitize(label)}', '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllTenantLabelsAsync(string tenantId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM labels WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM labels WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND user_id IS NULL AND credential_id IS NULL AND document_id IS NULL AND index_id IS NULL;";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion

        #region User Labels

        public async Task<List<string>> GetUserLabelsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT label FROM labels WHERE user_id = N'{Sanitizer.Sanitize(userId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task ReplaceUserLabelsAsync(string tenantId, string userId, IEnumerable<string> labels, CancellationToken token = default)
        {
            await DeleteAllUserLabelsAsync(tenantId, userId, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            foreach (string label in labels)
            {
                string id = IdGenerator.GenerateLabelId();
                string query = $@"
INSERT INTO labels (id, user_id, label, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', N'{Sanitizer.Sanitize(userId)}', N'{Sanitizer.Sanitize(label)}', '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllUserLabelsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM labels WHERE user_id = N'{Sanitizer.Sanitize(userId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM labels WHERE user_id = N'{Sanitizer.Sanitize(userId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion

        #region Credential Labels

        public async Task<List<string>> GetCredentialLabelsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            string query = $"SELECT DISTINCT label FROM labels WHERE credential_id = N'{Sanitizer.Sanitize(credentialId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task ReplaceCredentialLabelsAsync(string tenantId, string credentialId, IEnumerable<string> labels, CancellationToken token = default)
        {
            await DeleteAllCredentialLabelsAsync(tenantId, credentialId, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            foreach (string label in labels)
            {
                string id = IdGenerator.GenerateLabelId();
                string query = $@"
INSERT INTO labels (id, credential_id, label, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', N'{Sanitizer.Sanitize(credentialId)}', N'{Sanitizer.Sanitize(label)}', '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
                await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllCredentialLabelsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM labels WHERE credential_id = N'{Sanitizer.Sanitize(credentialId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM labels WHERE credential_id = N'{Sanitizer.Sanitize(credentialId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        #endregion
    }
}
