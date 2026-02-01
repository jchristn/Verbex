namespace Verbex.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.DTO;
    using Verbex.Models;
    using Verbex.Utilities;

    using Sanitizer = Verbex.Database.Postgresql.Sanitizer;

    /// <summary>
    /// PostgreSQL implementation of term methods.
    /// </summary>
    internal class TermMethods : ITermMethods
    {
        private readonly PostgresqlDatabaseDriver _Driver;

        public TermMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task<string> AddOrGetAsync(string tenantId, string indexId, string id, string term, CancellationToken token = default)
        {
            // Use ON CONFLICT DO NOTHING to handle concurrent inserts atomically.
            // This avoids TOCTOU race conditions where two concurrent calls both check
            // that a term doesn't exist, then both try to insert it.
            DateTime now = DateTime.UtcNow;
            string insertQuery = $@"
INSERT INTO terms (id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc)
VALUES ('{Sanitizer.Sanitize(id)}', '{Sanitizer.Sanitize(tenantId)}', '{Sanitizer.Sanitize(indexId)}', '{Sanitizer.Sanitize(term)}', 0, 0, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}')
ON CONFLICT (index_id, term) DO NOTHING;";
            await _Driver.ExecuteQueryAsync(insertQuery, true, token).ConfigureAwait(false);

            // Always fetch the actual record to get the correct ID (ours if we inserted, existing if another request won)
            TermRecord? record = await GetAsync(tenantId, indexId, term, token).ConfigureAwait(false);
            return record?.Id ?? id;
        }

        public async Task<TermRecord?> GetAsync(string tenantId, string indexId, string term, CancellationToken token = default)
        {
            string query = $@"SELECT id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND term = '{Sanitizer.Sanitize(term)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : MapRowToTerm(result.Rows[0]);
        }

        public async Task<TermRecord?> GetByIdAsync(string tenantId, string indexId, string id, CancellationToken token = default)
        {
            string query = $@"SELECT id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(id)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : MapRowToTerm(result.Rows[0]);
        }

        public async Task<Dictionary<string, TermRecord>> GetMultipleAsync(string tenantId, string indexId, IEnumerable<string> terms, CancellationToken token = default)
        {
            List<string> termList = new List<string>(terms);
            Dictionary<string, TermRecord> result = new Dictionary<string, TermRecord>();
            if (termList.Count == 0) return result;

            string inClause = string.Join(",", termList.ConvertAll(t => $"'{Sanitizer.Sanitize(t)}'"));
            string query = $@"SELECT id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND term IN ({inClause});";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            foreach (DataRow row in dt.Rows)
            {
                TermRecord tr = MapRowToTerm(row);
                result[tr.Term] = tr;
            }
            return result;
        }

        public async Task<List<TermRecord>> GetByPrefixAsync(string tenantId, string indexId, string prefix, int limit = 100, CancellationToken token = default)
        {
            string query = $@"SELECT id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND term ILIKE '{Sanitizer.EscapeLikePattern(prefix)}%' ESCAPE '\' LIMIT {limit};";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<TermRecord> list = new List<TermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToTerm(row));
            return list;
        }

        public async Task<List<TermRecord>> GetTopAsync(string tenantId, string indexId, int limit = 100, CancellationToken token = default)
        {
            string query = $@"SELECT id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' ORDER BY document_frequency DESC LIMIT {limit};";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<TermRecord> list = new List<TermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToTerm(row));
            return list;
        }

        public async Task<long> GetCountAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string query = $"SELECT COUNT(*) FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0][0]) : 0;
        }

        public async Task<bool> ExistsAsync(string tenantId, string indexId, string term, CancellationToken token = default)
        {
            string query = $"SELECT 1 FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND term = '{Sanitizer.Sanitize(term)}' LIMIT 1;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0;
        }

        public async Task UpdateFrequenciesAsync(string tenantId, string indexId, string termId, int documentFrequency, int totalFrequency, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string query = $"UPDATE terms SET document_frequency = {documentFrequency}, total_frequency = {totalFrequency}, last_update_utc = '{Sanitizer.FormatDateTime(now)}' WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(termId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task IncrementFrequenciesAsync(string tenantId, string indexId, string termId, int documentFrequencyDelta, int totalFrequencyDelta, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string query = $"UPDATE terms SET document_frequency = document_frequency + {documentFrequencyDelta}, total_frequency = total_frequency + {totalFrequencyDelta}, last_update_utc = '{Sanitizer.FormatDateTime(now)}' WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(termId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, string>> AddOrGetBatchAsync(string tenantId, string indexId, Dictionary<string, string> terms, CancellationToken token = default)
        {
            if (terms == null || terms.Count == 0) return new Dictionary<string, string>();

            DateTime now = DateTime.UtcNow;
            string nowFormatted = Sanitizer.FormatDateTime(now);
            List<KeyValuePair<string, string>> termsList = terms.ToList();

            // Single INSERT with ON CONFLICT DO UPDATE and RETURNING
            // Using DO UPDATE SET term = EXCLUDED.term (a no-op update) causes PostgreSQL
            // to return ALL rows via RETURNING, including existing ones - not just new inserts.
            // This eliminates the need for a separate SELECT query.
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO terms (id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc) VALUES ");

            List<string> valuesClauses = new List<string>();
            foreach (KeyValuePair<string, string> kvp in termsList)
            {
                valuesClauses.Add($"('{Sanitizer.Sanitize(kvp.Key)}', '{Sanitizer.Sanitize(tenantId)}', '{Sanitizer.Sanitize(indexId)}', '{Sanitizer.Sanitize(kvp.Value)}', 0, 0, '{nowFormatted}', '{nowFormatted}')");
            }

            sb.Append(string.Join(", ", valuesClauses));
            sb.Append(" ON CONFLICT (index_id, term) DO UPDATE SET term = EXCLUDED.term RETURNING id, term;");

            DataTable dt = await _Driver.ExecuteQueryAsync(sb.ToString(), false, token).ConfigureAwait(false);

            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (DataRow row in dt.Rows)
            {
                string termValue = row["term"]?.ToString() ?? string.Empty;
                string termId = row["id"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(termValue) && !string.IsNullOrEmpty(termId))
                {
                    result[termValue] = termId;
                }
            }

            return result;
        }

        public async Task IncrementFrequenciesBatchAsync(string tenantId, string indexId, Dictionary<string, FrequencyDelta> updates, CancellationToken token = default)
        {
            if (updates == null || updates.Count == 0) return;

            DateTime now = DateTime.UtcNow;
            List<string> termIds = new List<string>(updates.Keys);
            string inClause = string.Join(",", termIds.ConvertAll(id => $"'{Sanitizer.Sanitize(id)}'"));

            StringBuilder docFreqCase = new StringBuilder("CASE id ");
            StringBuilder totalFreqCase = new StringBuilder("CASE id ");
            foreach (KeyValuePair<string, FrequencyDelta> kvp in updates)
            {
                docFreqCase.Append($"WHEN '{Sanitizer.Sanitize(kvp.Key)}' THEN {kvp.Value.DocFreqDelta} ");
                totalFreqCase.Append($"WHEN '{Sanitizer.Sanitize(kvp.Key)}' THEN {kvp.Value.TotalFreqDelta} ");
            }
            docFreqCase.Append("ELSE 0 END");
            totalFreqCase.Append("ELSE 0 END");

            string query = $@"UPDATE terms SET
document_frequency = document_frequency + ({docFreqCase}),
total_frequency = total_frequency + ({totalFreqCase}),
last_update_utc = '{Sanitizer.FormatDateTime(now)}'
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id IN ({inClause});";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task DecrementFrequenciesBatchAsync(string tenantId, string indexId, Dictionary<string, FrequencyDelta> updates, CancellationToken token = default)
        {
            if (updates == null || updates.Count == 0) return;

            // Convert to negative deltas and call increment
            Dictionary<string, FrequencyDelta> negatedUpdates = new Dictionary<string, FrequencyDelta>();
            foreach (KeyValuePair<string, FrequencyDelta> kvp in updates)
            {
                negatedUpdates[kvp.Key] = new FrequencyDelta(-kvp.Value.DocFreqDelta, -kvp.Value.TotalFreqDelta);
            }
            await IncrementFrequenciesBatchAsync(tenantId, indexId, negatedUpdates, token).ConfigureAwait(false);
        }

        public async Task<long> DeleteOrphanedAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND document_frequency = 0;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND document_frequency = 0;";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task<Dictionary<string, string>> GetAllTermIdsAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string query = $"SELECT term, id FROM terms WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            foreach (DataRow row in dt.Rows)
            {
                string term = row["term"]?.ToString() ?? string.Empty;
                string id = row["id"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(id))
                {
                    result[term] = id;
                }
            }
            return result;
        }

        private static TermRecord MapRowToTerm(DataRow row)
        {
            return new TermRecord
            {
                Id = row["id"]?.ToString() ?? string.Empty,
                Term = row["term"]?.ToString() ?? string.Empty,
                DocumentFrequency = Convert.ToInt32(row["document_frequency"]),
                TotalFrequency = Convert.ToInt32(row["total_frequency"]),
                LastUpdateUtc = row["last_update_utc"] != DBNull.Value ? DateTime.Parse(row["last_update_utc"].ToString()!) : DateTime.UtcNow,
                CreatedUtc = DateTime.Parse(row["created_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o"))
            };
        }
    }
}
