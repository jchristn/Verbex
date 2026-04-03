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

        public async Task AddAsync(string tablePrefix, string id, string? documentId, string label, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            DateTime now = DateTime.UtcNow;
            string query = $@"
INSERT INTO {prefix}_labels (id, document_id, label, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', {Sanitizer.FormatNullableString(documentId)}, N'{Sanitizer.Sanitize(label)}', '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task AddBatchAsync(string tablePrefix, IEnumerable<LabelRecord> records, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<LabelRecord> recordList = records.ToList();
            if (recordList.Count == 0) return;

            const int ChunkSize = 200;
            DateTime now = DateTime.UtcNow;
            string nowFormatted = Sanitizer.FormatDateTime(now);

            for (int i = 0; i < recordList.Count; i += ChunkSize)
            {
                List<LabelRecord> chunk = recordList.Skip(i).Take(ChunkSize).ToList();
                StringBuilder sb = new StringBuilder();
                sb.Append($"INSERT INTO {prefix}_labels (id, document_id, label, last_update_utc, created_utc) VALUES ");

                List<string> valuesClauses = new List<string>();
                foreach (LabelRecord record in chunk)
                {
                    valuesClauses.Add($"(N'{Sanitizer.Sanitize(record.Id)}', {Sanitizer.FormatNullableString(record.DocumentId)}, N'{Sanitizer.Sanitize(record.Label)}', '{nowFormatted}', '{nowFormatted}')");
                }

                sb.Append(string.Join(", ", valuesClauses));
                sb.Append(';');

                await _Driver.ExecuteQueryAsync(sb.ToString(), true, token).ConfigureAwait(false);
            }
        }

        public async Task<List<string>> GetByDocumentAsync(string tablePrefix, string documentId, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT DISTINCT label FROM {prefix}_labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetIndexLabelsAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT DISTINCT label FROM {prefix}_labels WHERE document_id IS NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetAllDistinctAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT DISTINCT label FROM {prefix}_labels;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["label"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<List<string>> GetDocumentsByLabelAsync(string tablePrefix, string label, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT DISTINCT document_id FROM {prefix}_labels WHERE label = N'{Sanitizer.Sanitize(label)}' AND document_id IS NOT NULL;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Cast<DataRow>().Select(r => r["document_id"]?.ToString() ?? string.Empty).ToList();
        }

        public async Task<bool> ExistsAsync(string tablePrefix, string documentId, string label, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT TOP 1 1 FROM {prefix}_labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}' AND label = N'{Sanitizer.Sanitize(label)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0;
        }

        public async Task<bool> RemoveAsync(string tablePrefix, string documentId, string label, CancellationToken token = default)
        {
            bool exists = await ExistsAsync(tablePrefix, documentId, label, token).ConfigureAwait(false);
            if (!exists) return false;

            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"DELETE FROM {prefix}_labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}' AND label = N'{Sanitizer.Sanitize(label)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> RemoveIndexLabelAsync(string tablePrefix, string label, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string checkQuery = $"SELECT TOP 1 1 FROM {prefix}_labels WHERE document_id IS NULL AND label = N'{Sanitizer.Sanitize(label)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(checkQuery, false, token).ConfigureAwait(false);
            if (dt.Rows.Count == 0) return false;

            string query = $"DELETE FROM {prefix}_labels WHERE document_id IS NULL AND label = N'{Sanitizer.Sanitize(label)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        public async Task<long> RemoveAllAsync(string tablePrefix, string documentId, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string countQuery = $"SELECT COUNT(*) FROM {prefix}_labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM {prefix}_labels WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task ReplaceAsync(string tablePrefix, string documentId, IEnumerable<string> labels, CancellationToken token = default)
        {
            await RemoveAllAsync(tablePrefix, documentId, token).ConfigureAwait(false);
            List<LabelRecord> records = labels
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => new LabelRecord
                {
                    Id = IdGenerator.GenerateLabelId(),
                    DocumentId = documentId,
                    Label = label
                })
                .ToList();

            if (records.Count > 0)
            {
                await AddBatchAsync(tablePrefix, records, token).ConfigureAwait(false);
            }
        }

        public async Task<long> DeleteAllAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string countQuery = $"SELECT COUNT(*) FROM {prefix}_labels;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM {prefix}_labels;";
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
            await InsertScopedLabelsAsync("tenant_id", tenantId, labels, token).ConfigureAwait(false);
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
            await InsertScopedLabelsAsync("user_id", userId, labels, token).ConfigureAwait(false);
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
            await InsertScopedLabelsAsync("credential_id", credentialId, labels, token).ConfigureAwait(false);
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

        private async Task InsertScopedLabelsAsync(string scopeColumn, string scopeValue, IEnumerable<string> labels, CancellationToken token)
        {
            List<string> labelList = labels
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();

            if (labelList.Count == 0)
            {
                return;
            }

            const int ChunkSize = 200;
            DateTime now = DateTime.UtcNow;
            string nowFormatted = Sanitizer.FormatDateTime(now);
            string sanitizedScopeValue = Sanitizer.Sanitize(scopeValue);

            for (int i = 0; i < labelList.Count; i += ChunkSize)
            {
                List<string> chunk = labelList.Skip(i).Take(ChunkSize).ToList();
                StringBuilder sb = new StringBuilder();
                sb.Append($"INSERT INTO labels (id, {scopeColumn}, label, last_update_utc, created_utc) VALUES ");

                List<string> valuesClauses = new List<string>();
                foreach (string label in chunk)
                {
                    valuesClauses.Add($"(N'{Sanitizer.Sanitize(IdGenerator.GenerateLabelId())}', N'{sanitizedScopeValue}', N'{Sanitizer.Sanitize(label)}', '{nowFormatted}', '{nowFormatted}')");
                }

                sb.Append(string.Join(", ", valuesClauses));
                sb.Append(';');

                await _Driver.ExecuteQueryAsync(sb.ToString(), true, token).ConfigureAwait(false);
            }
        }

        #endregion
    }
}
