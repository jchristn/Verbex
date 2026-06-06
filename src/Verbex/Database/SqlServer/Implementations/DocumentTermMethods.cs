namespace Verbex.Database.SqlServer.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.Models;

    using Sanitizer = Verbex.Database.SqlServer.Sanitizer;

    /// <summary>
    /// SQL Server implementation of document-term methods using prefixed tables.
    /// </summary>
    internal class DocumentTermMethods : IDocumentTermMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        public DocumentTermMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task AddAsync(string tablePrefix, string id, string documentId, string termId, int termFrequency, List<int> characterPositions, List<int> termPositions, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            DateTime now = DateTime.UtcNow;
            string? charPosJson = characterPositions.Count > 0 ? JsonSerializer.Serialize(characterPositions) : null;
            string? termPosJson = termPositions.Count > 0 ? JsonSerializer.Serialize(termPositions) : null;
            string query = $@"
INSERT INTO {prefix}_document_terms (id, document_id, term_id, term_frequency, character_positions, term_positions, last_update_utc, created_utc)
VALUES (N'{Sanitizer.Sanitize(id)}', N'{Sanitizer.Sanitize(documentId)}', N'{Sanitizer.Sanitize(termId)}', {termFrequency}, {Sanitizer.FormatNullableString(charPosJson)}, {Sanitizer.FormatNullableString(termPosJson)}, '{Sanitizer.FormatDateTime(now)}', '{Sanitizer.FormatDateTime(now)}');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        public async Task AddBatchAsync(string tablePrefix, IEnumerable<DocumentTermRecord> records, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<DocumentTermRecord> recordList = records.ToList();
            if (recordList.Count == 0) return;

            const int ChunkSize = 100;
            DateTime now = DateTime.UtcNow;
            string nowFormatted = Sanitizer.FormatDateTime(now);

            for (int i = 0; i < recordList.Count; i += ChunkSize)
            {
                List<DocumentTermRecord> chunk = recordList.Skip(i).Take(ChunkSize).ToList();
                StringBuilder sb = new StringBuilder();
                sb.Append($"INSERT INTO {prefix}_document_terms (id, document_id, term_id, term_frequency, character_positions, term_positions, last_update_utc, created_utc) VALUES ");

                List<string> valuesClauses = new List<string>();
                foreach (DocumentTermRecord record in chunk)
                {
                    string? charPosJson = record.CharacterPositions.Count > 0 ? JsonSerializer.Serialize(record.CharacterPositions) : null;
                    string? termPosJson = record.TermPositions.Count > 0 ? JsonSerializer.Serialize(record.TermPositions) : null;
                    valuesClauses.Add($"(N'{Sanitizer.Sanitize(record.Id)}', N'{Sanitizer.Sanitize(record.DocumentId)}', N'{Sanitizer.Sanitize(record.TermId)}', {record.TermFrequency}, {Sanitizer.FormatNullableString(charPosJson)}, {Sanitizer.FormatNullableString(termPosJson)}, '{nowFormatted}', '{nowFormatted}')");
                }

                sb.Append(string.Join(", ", valuesClauses));
                sb.Append(';');

                await _Driver.ExecuteQueryAsync(sb.ToString(), true, token).ConfigureAwait(false);
            }
        }

        public async Task<List<DocumentTermRecord>> GetByDocumentAsync(string tablePrefix, string documentId, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $@"
SELECT dt.id, dt.document_id, dt.term_id, dt.term_frequency, dt.character_positions, dt.term_positions, dt.last_update_utc, dt.created_utc, t.term
FROM {prefix}_document_terms dt
JOIN {prefix}_terms t ON dt.term_id = t.id
WHERE dt.document_id = N'{Sanitizer.Sanitize(documentId)}';";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentTermRecord> list = new List<DocumentTermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToDocumentTerm(row));
            return list;
        }

        public async Task<List<DocumentTermRecord>> GetPostingsAsync(string tablePrefix, IEnumerable<string> termIds, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> termIdList = termIds.ToList();
            if (termIdList.Count == 0) return new List<DocumentTermRecord>();

            string inClause = string.Join(",", termIdList.Select(id => $"N'{Sanitizer.Sanitize(id)}'"));
            string query = $@"
SELECT dt.id, dt.document_id, dt.term_id, dt.term_frequency, dt.character_positions, dt.term_positions, dt.last_update_utc, dt.created_utc, t.term
FROM {prefix}_document_terms dt
JOIN {prefix}_terms t ON dt.term_id = t.id
WHERE dt.term_id IN ({inClause});";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentTermRecord> list = new List<DocumentTermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToDocumentTerm(row));
            return list;
        }

        public async Task<List<DocumentTermRecord>> GetPostingsByTermAsync(string tablePrefix, string termId, CancellationToken token = default)
        {
            return await GetPostingsAsync(tablePrefix, new[] { termId }, token).ConfigureAwait(false);
        }

        public async Task<List<SearchMatch>> SearchAsync(string tablePrefix, IEnumerable<string> termIds, bool useAndLogic = false, int limit = 100, IEnumerable<string>? labels = null, IDictionary<string, string>? tags = null, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> termIdList = termIds.ToList();
            if (termIdList.Count == 0) return new List<SearchMatch>();

            string inClause = string.Join(",", termIdList.Select(id => $"N'{Sanitizer.Sanitize(id)}'"));

            // Check if we have any document filters (labels or tags)
            List<string>? labelList = labels?.ToList();
            bool hasLabelFilter = labelList != null && labelList.Count > 0;
            bool hasTagFilter = tags != null && tags.Count > 0;
            bool hasDocumentFilter = hasLabelFilter || hasTagFilter;

            string query;

            if (hasDocumentFilter)
            {
                // When filtering by labels/tags, use INNER JOIN with derived table and FORCE ORDER
                // to make SQL Server filter documents FIRST before scanning document_terms.

                // Build filtered documents subquery - start with first filter
                string baseFilter;
                int filterIndex = 0;

                if (hasTagFilter)
                {
                    KeyValuePair<string, string> firstTag = tags!.First();
                    baseFilter = $"SELECT document_id FROM {prefix}_tags WHERE [key] = N'{Sanitizer.Sanitize(firstTag.Key)}' AND value = N'{Sanitizer.Sanitize(firstTag.Value)}'";
                    filterIndex = 1;
                }
                else
                {
                    baseFilter = $"SELECT document_id FROM {prefix}_labels WHERE label = N'{Sanitizer.Sanitize(labelList![0])}' COLLATE Latin1_General_CI_AS";
                    filterIndex = 1;
                }

                // Build additional filters as nested subqueries for AND logic
                StringBuilder filteredDocsQuery = new StringBuilder(baseFilter);

                // Add remaining tag filters
                if (hasTagFilter)
                {
                    foreach (KeyValuePair<string, string> tag in tags!.Skip(1))
                    {
                        string currentQuery = filteredDocsQuery.ToString();
                        filteredDocsQuery.Clear();
                        filteredDocsQuery.Append($"SELECT document_id FROM ({currentQuery}) AS tf{filterIndex} WHERE document_id IN (SELECT document_id FROM {prefix}_tags WHERE [key] = N'{Sanitizer.Sanitize(tag.Key)}' AND value = N'{Sanitizer.Sanitize(tag.Value)}')");
                        filterIndex++;
                    }
                }

                // Add label filters
                if (hasLabelFilter)
                {
                    int startIdx = hasTagFilter ? 0 : 1;
                    foreach (string label in labelList!.Skip(startIdx))
                    {
                        string currentQuery = filteredDocsQuery.ToString();
                        filteredDocsQuery.Clear();
                        filteredDocsQuery.Append($"SELECT document_id FROM ({currentQuery}) AS lf{filterIndex} WHERE document_id IN (SELECT document_id FROM {prefix}_labels WHERE label = N'{Sanitizer.Sanitize(label)}' COLLATE Latin1_General_CI_AS)");
                        filterIndex++;
                    }
                }

                // Build main query with JOIN and OPTION (FORCE ORDER) to force filter-first execution
                string havingClause = useAndLogic ? $"HAVING COUNT(DISTINCT dt.term_id) = {termIdList.Count}" : "";

                query = $@"
SELECT TOP {limit} dt.document_id, SUM(dt.term_frequency) as total_frequency, COUNT(DISTINCT dt.term_id) as term_count
FROM ({filteredDocsQuery}) AS filtered
INNER JOIN {prefix}_document_terms dt WITH (INDEX(idx_{prefix}_docterms_search)) ON dt.document_id = filtered.document_id
WHERE dt.term_id IN ({inClause})
GROUP BY dt.document_id
{havingClause}
ORDER BY term_count DESC, total_frequency DESC
OPTION (FORCE ORDER);";
            }
            else
            {
                // No document filters - simple query on document_terms
                string havingClause = useAndLogic ? $"HAVING COUNT(DISTINCT dt.term_id) = {termIdList.Count}" : "";

                query = $@"
SELECT TOP {limit} dt.document_id, SUM(dt.term_frequency) as total_frequency, COUNT(DISTINCT dt.term_id) as term_count
FROM {prefix}_document_terms dt WITH (INDEX(idx_{prefix}_docterms_search))
WHERE dt.term_id IN ({inClause})
GROUP BY dt.document_id
{havingClause}
ORDER BY term_count DESC, total_frequency DESC;";
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

        public async Task<long> DeleteByDocumentAsync(string tablePrefix, string documentId, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string countQuery = $"SELECT COUNT(*) FROM {prefix}_document_terms WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM {prefix}_document_terms WHERE document_id = N'{Sanitizer.Sanitize(documentId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task<List<DocumentTermRecord>> GetByDocumentsAndTermsAsync(string tablePrefix, IEnumerable<string> documentIds, IEnumerable<string> termIds, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> docIdList = documentIds.ToList();
            List<string> termIdList = termIds.ToList();
            if (docIdList.Count == 0 || termIdList.Count == 0) return new List<DocumentTermRecord>();

            string docInClause = string.Join(",", docIdList.Select(id => $"N'{Sanitizer.Sanitize(id)}'"));
            string termInClause = string.Join(",", termIdList.Select(id => $"N'{Sanitizer.Sanitize(id)}'"));

            // Select only the columns needed for scoring (skip position data and terms JOIN).
            // Use WITH (INDEX) to ensure SQL Server uses the composite index for this lookup.
            string query = $@"
SELECT dt.document_id, dt.term_id, dt.term_frequency
FROM {prefix}_document_terms dt WITH (INDEX(idx_{prefix}_docterms_doc_term))
WHERE dt.document_id IN ({docInClause}) AND dt.term_id IN ({termInClause});";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentTermRecord> list = new List<DocumentTermRecord>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new DocumentTermRecord
                {
                    DocumentId = row["document_id"]?.ToString() ?? string.Empty,
                    TermId = row["term_id"]?.ToString() ?? string.Empty,
                    TermFrequency = Convert.ToInt32(row["term_frequency"])
                });
            }
            return list;
        }

        public async Task<long> DeleteAllAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string countQuery = $"SELECT COUNT(*) FROM {prefix}_document_terms;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM {prefix}_document_terms;";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        public async Task<List<DocumentTermRecord>> GetByDocumentsAsync(string tablePrefix, IEnumerable<string> documentIds, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> docIdList = documentIds.ToList();
            if (docIdList.Count == 0) return new List<DocumentTermRecord>();

            string inClause = string.Join(",", docIdList.Select(id => $"N'{Sanitizer.Sanitize(id)}'"));
            string query = $@"
SELECT dt.id, dt.document_id, dt.term_id, dt.term_frequency, dt.character_positions, dt.term_positions, dt.last_update_utc, dt.created_utc, t.term
FROM {prefix}_document_terms dt
JOIN {prefix}_terms t ON dt.term_id = t.id
WHERE dt.document_id IN ({inClause});";
            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentTermRecord> list = new List<DocumentTermRecord>();
            foreach (DataRow row in dt.Rows) list.Add(MapRowToDocumentTerm(row));
            return list;
        }

        public async Task<Dictionary<string, DocumentTermStats>> GetStatsByDocumentsAsync(string tablePrefix, IEnumerable<string> documentIds, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> docIdList = documentIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (docIdList.Count == 0) return new Dictionary<string, DocumentTermStats>();

            string inClause = string.Join(",", docIdList.Select(id => $"N'{Sanitizer.Sanitize(id)}'"));
            string query = $@"
SELECT dt.document_id, COUNT(*) AS unique_term_count, COALESCE(SUM(dt.term_frequency), 0) AS total_term_occurrences
FROM {prefix}_document_terms dt WITH (INDEX(idx_{prefix}_docterms_doc))
WHERE dt.document_id IN ({inClause})
GROUP BY dt.document_id;";

            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            Dictionary<string, DocumentTermStats> stats = new Dictionary<string, DocumentTermStats>();
            foreach (DataRow row in dt.Rows)
            {
                string documentId = row["document_id"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(documentId)) continue;

                stats[documentId] = new DocumentTermStats
                {
                    DocumentId = documentId,
                    UniqueTermCount = Convert.ToInt64(row["unique_term_count"]),
                    TotalTermOccurrences = Convert.ToInt64(row["total_term_occurrences"])
                };
            }

            return stats;
        }

        public async Task<long> DeleteByDocumentsAsync(string tablePrefix, IEnumerable<string> documentIds, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> docIdList = documentIds.ToList();
            if (docIdList.Count == 0) return 0;

            string inClause = string.Join(",", docIdList.Select(id => $"N'{Sanitizer.Sanitize(id)}'"));

            string countQuery = $"SELECT COUNT(*) FROM {prefix}_document_terms WHERE document_id IN ({inClause});";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM {prefix}_document_terms WHERE document_id IN ({inClause});";
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
