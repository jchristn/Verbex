namespace Verbex.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.Models;

    using Sanitizer = Verbex.Database.Postgresql.Sanitizer;

    /// <summary>
    /// PostgreSQL implementation of document-term methods.
    /// </summary>
    internal class DocumentTermMethods : IDocumentTermMethods
    {
        private readonly PostgresqlDatabaseDriver _Driver;

        public DocumentTermMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task AddAsync(string tenantId, string indexId, string id, string documentId, string termId, int termFrequency, List<int> characterPositions, List<int> termPositions, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string? charPosJson = characterPositions.Count > 0 ? JsonSerializer.Serialize(characterPositions) : null;
            string? termPosJson = termPositions.Count > 0 ? JsonSerializer.Serialize(termPositions) : null;
            string query = $@"
INSERT INTO document_terms (id, document_id, term_id, term_frequency, character_positions, term_positions, last_update_utc, created_utc)
VALUES ('{Sanitizer.Sanitize(id)}', '{Sanitizer.Sanitize(documentId)}', '{Sanitizer.Sanitize(termId)}', {termFrequency}, {Sanitizer.FormatNullableString(charPosJson)}, {Sanitizer.FormatNullableString(termPosJson)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task AddBatchAsync(string tenantId, string indexId, IEnumerable<DocumentTermRecord> records, CancellationToken token = default)
        {
            foreach (DocumentTermRecord record in records)
            {
                await AddAsync(tenantId, indexId, record.Id, record.DocumentId, record.TermId, record.TermFrequency, record.CharacterPositions, record.TermPositions, token).ConfigureAwait(false);
            }
        }

        public async Task<List<DocumentTermRecord>> GetByDocumentAsync(string tenantId, string indexId, string documentId, CancellationToken token = default)
        {
            string query = $@"
SELECT dt.id, dt.document_id, dt.term_id, dt.term_frequency, dt.character_positions, dt.term_positions, dt.last_update_utc, dt.created_utc, t.term
FROM document_terms dt
JOIN terms t ON dt.term_id = t.id
WHERE dt.document_id = '{Sanitizer.Sanitize(documentId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentTermRecord> list = new List<DocumentTermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToDocumentTerm(row));
            return list;
        }

        public async Task<List<DocumentTermRecord>> GetPostingsAsync(string tenantId, string indexId, IEnumerable<string> termIds, CancellationToken token = default)
        {
            List<string> termIdList = termIds.ToList();
            if (termIdList.Count == 0) return new List<DocumentTermRecord>();

            string inClause = string.Join(",", termIdList.Select(id => $"'{Sanitizer.Sanitize(id)}'"));
            string query = $@"
SELECT dt.id, dt.document_id, dt.term_id, dt.term_frequency, dt.character_positions, dt.term_positions, dt.last_update_utc, dt.created_utc, t.term
FROM document_terms dt
JOIN terms t ON dt.term_id = t.id
WHERE dt.term_id IN ({inClause});";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentTermRecord> list = new List<DocumentTermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToDocumentTerm(row));
            return list;
        }

        public async Task<List<DocumentTermRecord>> GetPostingsByTermAsync(string tenantId, string indexId, string termId, CancellationToken token = default)
        {
            return await GetPostingsAsync(tenantId, indexId, new[] { termId }, token).ConfigureAwait(false);
        }

        public async Task<List<SearchMatch>> SearchAsync(string tenantId, string indexId, IEnumerable<string> termIds, bool useAndLogic = false, int limit = 100, IEnumerable<string>? labels = null, IDictionary<string, string>? tags = null, CancellationToken token = default)
        {
            List<string> termIdList = termIds.ToList();
            if (termIdList.Count == 0) return new List<SearchMatch>();

            string inClause = string.Join(",", termIdList.Select(id => $"'{Sanitizer.Sanitize(id)}'"));

            string query;
            if (useAndLogic)
            {
                query = $@"
SELECT dt.document_id, SUM(dt.term_frequency) as total_frequency, COUNT(DISTINCT dt.term_id) as term_count
FROM document_terms dt
JOIN documents d ON dt.document_id = d.id
WHERE dt.term_id IN ({inClause}) AND d.tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND d.index_id = '{Sanitizer.Sanitize(indexId)}'
GROUP BY dt.document_id
HAVING COUNT(DISTINCT dt.term_id) = {termIdList.Count}
ORDER BY total_frequency DESC
LIMIT {limit};";
            }
            else
            {
                query = $@"
SELECT dt.document_id, SUM(dt.term_frequency) as total_frequency, COUNT(DISTINCT dt.term_id) as term_count
FROM document_terms dt
JOIN documents d ON dt.document_id = d.id
WHERE dt.term_id IN ({inClause}) AND d.tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND d.index_id = '{Sanitizer.Sanitize(indexId)}'
GROUP BY dt.document_id
ORDER BY total_frequency DESC
LIMIT {limit};";
            }

            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<SearchMatch> results = new List<SearchMatch>();
            foreach (DataRow row in dt.Rows)
            {
                results.Add(new SearchMatch
                {
                    DocumentId = row["document_id"]?.ToString() ?? string.Empty,
                    MatchedTermCount = Convert.ToInt32(row["term_count"]),
                    TotalFrequency = Convert.ToInt32(row["total_frequency"])
                });
            }
            return results;
        }

        public async Task<long> DeleteByDocumentAsync(string tenantId, string indexId, string documentId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM document_terms WHERE document_id = '{Sanitizer.Sanitize(documentId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM document_terms WHERE document_id = '{Sanitizer.Sanitize(documentId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task<List<DocumentTermRecord>> GetByDocumentsAndTermsAsync(string tenantId, string indexId, IEnumerable<string> documentIds, IEnumerable<string> termIds, CancellationToken token = default)
        {
            List<string> docIdList = documentIds.ToList();
            List<string> termIdList = termIds.ToList();
            if (docIdList.Count == 0 || termIdList.Count == 0) return new List<DocumentTermRecord>();

            string docInClause = string.Join(",", docIdList.Select(id => $"'{Sanitizer.Sanitize(id)}'"));
            string termInClause = string.Join(",", termIdList.Select(id => $"'{Sanitizer.Sanitize(id)}'"));

            string query = $@"
SELECT dt.id, dt.document_id, dt.term_id, dt.term_frequency, dt.character_positions, dt.term_positions, dt.last_update_utc, dt.created_utc, t.term
FROM document_terms dt
JOIN terms t ON dt.term_id = t.id
WHERE dt.document_id IN ({docInClause}) AND dt.term_id IN ({termInClause});";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentTermRecord> list = new List<DocumentTermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToDocumentTerm(row));
            return list;
        }

        public async Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string countQuery = $@"
SELECT COUNT(*) FROM document_terms dt
JOIN documents d ON dt.document_id = d.id
WHERE d.tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND d.index_id = '{Sanitizer.Sanitize(indexId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $@"
DELETE FROM document_terms WHERE document_id IN (
    SELECT id FROM documents WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}'
);";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        private static DocumentTermRecord MapRowToDocumentTerm(DataRow row)
        {
            List<int> charPositions = new List<int>();
            List<int> termPositions = new List<int>();

            string? charPosJson = row["character_positions"]?.ToString();
            if (!string.IsNullOrEmpty(charPosJson))
            {
                charPositions = JsonSerializer.Deserialize<List<int>>(charPosJson) ?? new List<int>();
            }

            string? termPosJson = row["term_positions"]?.ToString();
            if (!string.IsNullOrEmpty(termPosJson))
            {
                termPositions = JsonSerializer.Deserialize<List<int>>(termPosJson) ?? new List<int>();
            }

            return new DocumentTermRecord
            {
                Id = row["id"]?.ToString() ?? string.Empty,
                DocumentId = row["document_id"]?.ToString() ?? string.Empty,
                TermId = row["term_id"]?.ToString() ?? string.Empty,
                Term = row.Table.Columns.Contains("term") ? row["term"]?.ToString() ?? string.Empty : string.Empty,
                TermFrequency = Convert.ToInt32(row["term_frequency"]),
                CharacterPositions = charPositions,
                TermPositions = termPositions,
                LastUpdateUtc = row["last_update_utc"] != DBNull.Value ? DateTime.Parse(row["last_update_utc"].ToString()!) : DateTime.UtcNow,
                CreatedUtc = DateTime.Parse(row["created_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o"))
            };
        }
    }
}
