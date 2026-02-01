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
    using Verbex.DTO;
    using Verbex.Models;
    using Verbex.Utilities;

    using Sanitizer = Verbex.Database.SqlServer.Sanitizer;

    /// <summary>
    /// SQL Server implementation of term methods.
    /// </summary>
    internal class TermMethods : ITermMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        public TermMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task<string> AddOrGetAsync(string tenantId, string indexId, string id, string term, CancellationToken token = default)
        {
            // Use MERGE to handle concurrent inserts atomically.
            // This avoids TOCTOU race conditions where two concurrent calls both check
            // that a term doesn't exist, then both try to insert it.
            DateTime now = DateTime.UtcNow;
            string mergeQuery = $@"
MERGE INTO terms WITH (HOLDLOCK) AS target
USING (SELECT N'{Sanitizer.Sanitize(indexId)}' AS index_id, N'{Sanitizer.Sanitize(term)}' AS term) AS source
ON target.index_id = source.index_id AND target.term = source.term
WHEN NOT MATCHED THEN
    INSERT (id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc)
    VALUES (N'{Sanitizer.Sanitize(id)}', N'{Sanitizer.Sanitize(tenantId)}', N'{Sanitizer.Sanitize(indexId)}', N'{Sanitizer.Sanitize(term)}', 0, 0, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
            await _Driver.ExecuteQueryAsync(mergeQuery, true, token).ConfigureAwait(false);

            // Always fetch the actual record to get the correct ID (ours if we inserted, existing if another request won)
            TermRecord? record = await GetAsync(tenantId, indexId, term, token).ConfigureAwait(false);
            return record?.Id ?? id;
        }

        public async Task<TermRecord?> GetAsync(string tenantId, string indexId, string term, CancellationToken token = default)
        {
            string query = $@"SELECT id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND term = N'{Sanitizer.Sanitize(term)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : MapRowToTerm(result.Rows[0]);
        }

        public async Task<TermRecord?> GetByIdAsync(string tenantId, string indexId, string id, CancellationToken token = default)
        {
            string query = $@"SELECT id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND id = N'{Sanitizer.Sanitize(id)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : MapRowToTerm(result.Rows[0]);
        }

        public async Task<Dictionary<string, TermRecord>> GetMultipleAsync(string tenantId, string indexId, IEnumerable<string> terms, CancellationToken token = default)
        {
            List<string> termList = new List<string>(terms);
            Dictionary<string, TermRecord> result = new Dictionary<string, TermRecord>();
            if (termList.Count == 0) return result;

            string inClause = string.Join(",", termList.ConvertAll(t => $"N'{Sanitizer.Sanitize(t)}'"));
            string query = $@"SELECT id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND term IN ({inClause});";
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
            string query = $@"SELECT TOP {limit} id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND term LIKE N'{Sanitizer.EscapeLikePattern(prefix)}%';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<TermRecord> list = new List<TermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToTerm(row));
            return list;
        }

        public async Task<List<TermRecord>> GetTopAsync(string tenantId, string indexId, int limit = 100, CancellationToken token = default)
        {
            string query = $@"SELECT TOP {limit} id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc
FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' ORDER BY document_frequency DESC;";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<TermRecord> list = new List<TermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToTerm(row));
            return list;
        }

        public async Task<long> GetCountAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string query = $"SELECT COUNT(*) FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0][0]) : 0;
        }

        public async Task<bool> ExistsAsync(string tenantId, string indexId, string term, CancellationToken token = default)
        {
            string query = $"SELECT TOP 1 1 FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND term = N'{Sanitizer.Sanitize(term)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0;
        }

        public async Task UpdateFrequenciesAsync(string tenantId, string indexId, string termId, int documentFrequency, int totalFrequency, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string query = $"UPDATE terms SET document_frequency = {documentFrequency}, total_frequency = {totalFrequency}, last_update_utc = '{Sanitizer.FormatDateTime(now)}' WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND id = N'{Sanitizer.Sanitize(termId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task IncrementFrequenciesAsync(string tenantId, string indexId, string termId, int documentFrequencyDelta, int totalFrequencyDelta, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string query = $"UPDATE terms SET document_frequency = document_frequency + {documentFrequencyDelta}, total_frequency = total_frequency + {totalFrequencyDelta}, last_update_utc = '{Sanitizer.FormatDateTime(now)}' WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND id = N'{Sanitizer.Sanitize(termId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, string>> AddOrGetBatchAsync(string tenantId, string indexId, Dictionary<string, string> terms, CancellationToken token = default)
        {
            if (terms == null || terms.Count == 0) return new Dictionary<string, string>();

            DateTime now = DateTime.UtcNow;
            string nowFormatted = Sanitizer.FormatDateTime(now);
            List<KeyValuePair<string, string>> termsList = terms.ToList();

            // Single MERGE with OUTPUT clause to return all IDs in one round trip.
            // Using WHEN MATCHED with a no-op update ensures OUTPUT returns both
            // newly inserted and existing rows.
            StringBuilder sb = new StringBuilder();
            sb.Append("MERGE INTO terms WITH (HOLDLOCK) AS target USING (VALUES ");

            List<string> valuesClauses = new List<string>();
            foreach (KeyValuePair<string, string> kvp in termsList)
            {
                valuesClauses.Add($"(N'{Sanitizer.Sanitize(kvp.Key)}', N'{Sanitizer.Sanitize(tenantId)}', N'{Sanitizer.Sanitize(indexId)}', N'{Sanitizer.Sanitize(kvp.Value)}', 0, 0, '{nowFormatted}', '{nowFormatted}')");
            }

            sb.Append(string.Join(", ", valuesClauses));
            sb.Append(") AS source (id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc) ");
            sb.Append("ON target.index_id = source.index_id AND target.term = source.term ");
            sb.Append("WHEN NOT MATCHED THEN INSERT (id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc) ");
            sb.Append("VALUES (source.id, source.tenant_id, source.index_id, source.term, source.document_frequency, source.total_frequency, source.last_update_utc, source.created_utc) ");
            sb.Append("WHEN MATCHED THEN UPDATE SET term = target.term ");  // No-op update to trigger OUTPUT for matched rows
            sb.Append("OUTPUT inserted.id, inserted.term;");

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
            string inClause = string.Join(",", termIds.ConvertAll(id => $"N'{Sanitizer.Sanitize(id)}'"));

            StringBuilder docFreqCase = new StringBuilder("CASE id ");
            StringBuilder totalFreqCase = new StringBuilder("CASE id ");
            foreach (KeyValuePair<string, FrequencyDelta> kvp in updates)
            {
                docFreqCase.Append($"WHEN N'{Sanitizer.Sanitize(kvp.Key)}' THEN {kvp.Value.DocFreqDelta} ");
                totalFreqCase.Append($"WHEN N'{Sanitizer.Sanitize(kvp.Key)}' THEN {kvp.Value.TotalFreqDelta} ");
            }
            docFreqCase.Append("ELSE 0 END");
            totalFreqCase.Append("ELSE 0 END");

            string query = $@"UPDATE terms SET
document_frequency = document_frequency + ({docFreqCase}),
total_frequency = total_frequency + ({totalFreqCase}),
last_update_utc = '{Sanitizer.FormatDateTime(now)}'
WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND id IN ({inClause});";
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
            string countQuery = $"SELECT COUNT(*) FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND document_frequency = 0;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}' AND document_frequency = 0;";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task<Dictionary<string, string>> GetAllTermIdsAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string query = $"SELECT term, id FROM terms WHERE tenant_id = N'{Sanitizer.Sanitize(tenantId)}' AND index_id = N'{Sanitizer.Sanitize(indexId)}';";
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
