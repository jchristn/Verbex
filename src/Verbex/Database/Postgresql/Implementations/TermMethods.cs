namespace Verbex.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
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
            TermRecord? existing = await GetAsync(tenantId, indexId, term, token).ConfigureAwait(false);
            if (existing != null)
            {
                return existing.Id;
            }

            DateTime now = DateTime.UtcNow;
            string query = $@"
INSERT INTO terms (id, tenant_id, index_id, term, document_frequency, total_frequency, last_update_utc, created_utc)
VALUES ('{Sanitizer.Sanitize(id)}', '{Sanitizer.Sanitize(tenantId)}', '{Sanitizer.Sanitize(indexId)}', '{Sanitizer.Sanitize(term)}', 0, 0, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return id;
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
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kvp in terms)
            {
                string id = await AddOrGetAsync(tenantId, indexId, kvp.Key, kvp.Value, token).ConfigureAwait(false);
                result[kvp.Value] = id;
            }
            return result;
        }

        public async Task IncrementFrequenciesBatchAsync(string tenantId, string indexId, Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> updates, CancellationToken token = default)
        {
            foreach (KeyValuePair<string, (int, int)> kvp in updates)
            {
                await IncrementFrequenciesAsync(tenantId, indexId, kvp.Key, kvp.Value.Item1, kvp.Value.Item2, token).ConfigureAwait(false);
            }
        }

        public async Task DecrementFrequenciesBatchAsync(string tenantId, string indexId, Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> updates, CancellationToken token = default)
        {
            foreach (KeyValuePair<string, (int, int)> kvp in updates)
            {
                await IncrementFrequenciesAsync(tenantId, indexId, kvp.Key, -kvp.Value.Item1, -kvp.Value.Item2, token).ConfigureAwait(false);
            }
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
