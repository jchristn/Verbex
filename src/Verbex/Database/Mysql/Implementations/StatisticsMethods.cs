namespace Verbex.Database.Mysql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.Models;

    using Sanitizer = Verbex.Database.Mysql.Sanitizer;

    /// <summary>
    /// MySQL implementation of statistics methods.
    /// </summary>
    internal class StatisticsMethods : IStatisticsMethods
    {
        private readonly MysqlDatabaseDriver _Driver;

        public StatisticsMethods(MysqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task<IndexStatistics> GetIndexStatisticsAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            IndexStatistics stats = new IndexStatistics();

            string docCountQuery = $"SELECT COUNT(*) FROM {prefix}_documents;";
            DataTable docResult = await _Driver.ExecuteQueryAsync(docCountQuery, false, token).ConfigureAwait(false);
            stats.DocumentCount = docResult.Rows.Count > 0 ? Convert.ToInt64(docResult.Rows[0][0]) : 0;

            string termCountQuery = $"SELECT COUNT(*) FROM {prefix}_terms;";
            DataTable termResult = await _Driver.ExecuteQueryAsync(termCountQuery, false, token).ConfigureAwait(false);
            stats.TermCount = termResult.Rows.Count > 0 ? Convert.ToInt64(termResult.Rows[0][0]) : 0;

            string totalTermsQuery = $"SELECT COALESCE(SUM(total_frequency), 0) FROM {prefix}_terms;";
            DataTable totalResult = await _Driver.ExecuteQueryAsync(totalTermsQuery, false, token).ConfigureAwait(false);
            stats.TotalTermOccurrences = totalResult.Rows.Count > 0 ? Convert.ToInt64(totalResult.Rows[0][0]) : 0;

            if (stats.DocumentCount > 0)
            {
                stats.AverageDocumentLength = (double)stats.TotalTermOccurrences / stats.DocumentCount;
            }

            string docLengthQuery = $"SELECT COALESCE(SUM(document_length), 0) FROM {prefix}_documents;";
            DataTable lengthResult = await _Driver.ExecuteQueryAsync(docLengthQuery, false, token).ConfigureAwait(false);
            stats.TotalDocumentSize = lengthResult.Rows.Count > 0 ? Convert.ToInt64(lengthResult.Rows[0][0]) : 0;

            return stats;
        }

        public async Task<TermStatisticsResult?> GetTermStatisticsAsync(string tablePrefix, string term, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);

            string query = $@"
SELECT t.term, t.document_frequency, t.total_frequency
FROM {prefix}_terms t
WHERE t.term = '{Sanitizer.Sanitize(term)}';";

            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (dt.Rows.Count == 0)
            {
                return null;
            }

            DataRow row = dt.Rows[0];
            int docFrequency = Convert.ToInt32(row["document_frequency"]);
            int totalFrequency = Convert.ToInt32(row["total_frequency"]);

            return new TermStatisticsResult
            {
                Term = row["term"]?.ToString() ?? string.Empty,
                DocumentFrequency = docFrequency,
                TotalFrequency = totalFrequency,
                AverageFrequencyPerDocument = docFrequency > 0 ? (double)totalFrequency / docFrequency : 0
            };
        }

        public async Task<TenantStatistics> GetTenantStatisticsAsync(string tenantId, CancellationToken token = default)
        {
            TenantStatistics stats = new TenantStatistics
            {
                TenantId = tenantId
            };

            string indexQuery = $"SELECT identifier FROM indexes WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}';";
            DataTable indexResult = await _Driver.ExecuteQueryAsync(indexQuery, false, token).ConfigureAwait(false);
            List<string> indexIds = new List<string>();
            foreach (DataRow row in indexResult.Rows)
            {
                string indexId = row["identifier"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(indexId))
                {
                    indexIds.Add(indexId);
                }
            }

            stats.IndexCount = indexIds.Count;

            foreach (string indexId in indexIds)
            {
                string prefix = TablePrefixValidator.FromIndexId(indexId);
                stats.TotalDocumentCount += await GetTableCountAsync($"{prefix}_documents", token).ConfigureAwait(false);
                stats.TotalTermCount += await GetTableCountAsync($"{prefix}_terms", token).ConfigureAwait(false);
            }

            string userCountQuery = $"SELECT COUNT(*) FROM users WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}';";
            DataTable userResult = await _Driver.ExecuteQueryAsync(userCountQuery, false, token).ConfigureAwait(false);
            stats.UserCount = userResult.Rows.Count > 0 ? Convert.ToInt64(userResult.Rows[0][0]) : 0;

            string credCountQuery = $"SELECT COUNT(*) FROM credentials WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}';";
            DataTable credResult = await _Driver.ExecuteQueryAsync(credCountQuery, false, token).ConfigureAwait(false);
            stats.CredentialCount = credResult.Rows.Count > 0 ? Convert.ToInt64(credResult.Rows[0][0]) : 0;

            return stats;
        }

        private async Task<long> GetTableCountAsync(string tableName, CancellationToken token)
        {
            DataTable result = await _Driver.ExecuteQueryAsync($"SELECT COUNT(*) FROM {tableName};", false, token).ConfigureAwait(false);
            return result.Rows.Count > 0 ? Convert.ToInt64(result.Rows[0][0]) : 0;
        }
    }
}
