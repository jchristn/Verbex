namespace Verbex.Database.SqlServer.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.Utilities;

    using Sanitizer = Verbex.Database.SqlServer.Sanitizer;

    /// <summary>
    /// SQL Server implementation of document methods using prefixed tables.
    /// </summary>
    internal class DocumentMethods : IDocumentMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentMethods"/> class.
        /// </summary>
        /// <param name="driver">The database driver.</param>
        public DocumentMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task AddAsync(string tablePrefix, string id, string name, string? contentSha256, int documentLength, object? customMetadata = null, decimal? indexingRuntimeMs = null, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            DateTime now = DateTime.UtcNow;
            string? customMetadataJson = customMetadata != null
                ? Sanitizer.Sanitize(JsonSerializer.Serialize(customMetadata))
                : null;

            string query = $@"
INSERT INTO {prefix}_documents (id, name, content_sha256, document_length, term_count, custom_metadata, indexing_runtime_ms, indexed_utc, last_update_utc, created_utc)
VALUES (
    N'{Sanitizer.Sanitize(id)}',
    N'{Sanitizer.Sanitize(name)}',
    {Sanitizer.FormatNullableString(contentSha256)},
    {documentLength},
    0,
    {Sanitizer.FormatNullableString(customMetadataJson)},
    {Sanitizer.FormatNullableDecimal(indexingRuntimeMs)},
    '{Sanitizer.FormatDateTime(now)}',
    '{Sanitizer.FormatDateTime(now)}',
    '{Sanitizer.FormatDateTime(now)}'
);";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<DocumentMetadata?> GetAsync(string tablePrefix, string id, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $@"
SELECT id, name, content_sha256, document_length, term_count, custom_metadata, indexing_runtime_ms, indexed_utc, last_update_utc, created_utc
FROM {prefix}_documents
WHERE id = N'{Sanitizer.Sanitize(id)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToDocument(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<DocumentMetadata?> GetByNameAsync(string tablePrefix, string name, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $@"
SELECT id, name, content_sha256, document_length, term_count, custom_metadata, indexing_runtime_ms, indexed_utc, last_update_utc, created_utc
FROM {prefix}_documents
WHERE name = N'{Sanitizer.Sanitize(name)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToDocument(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<DocumentMetadata?> GetWithMetadataAsync(string tablePrefix, string id, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            DocumentMetadata? doc = await GetAsync(tablePrefix, id, token).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            string labelsQuery = $"SELECT label FROM {prefix}_labels WHERE document_id = N'{Sanitizer.Sanitize(id)}';";
            DataTable labelsResult = await _Driver.ExecuteQueryAsync(labelsQuery, false, token).ConfigureAwait(false);
            foreach (DataRow row in labelsResult.Rows)
            {
                doc.AddLabel(row["label"]?.ToString() ?? string.Empty);
            }

            string tagsQuery = $"SELECT [key], value FROM {prefix}_tags WHERE document_id = N'{Sanitizer.Sanitize(id)}';";
            DataTable tagsResult = await _Driver.ExecuteQueryAsync(tagsQuery, false, token).ConfigureAwait(false);
            foreach (DataRow row in tagsResult.Rows)
            {
                doc.SetTag(row["key"]?.ToString() ?? string.Empty, row["value"]?.ToString() ?? string.Empty);
            }

            string termsQuery = $@"
SELECT t.term
FROM {prefix}_document_terms dt
INNER JOIN {prefix}_terms t ON dt.term_id = t.id
WHERE dt.document_id = N'{Sanitizer.Sanitize(id)}';";
            DataTable termsResult = await _Driver.ExecuteQueryAsync(termsQuery, false, token).ConfigureAwait(false);
            foreach (DataRow row in termsResult.Rows)
            {
                doc.AddTerm(row["term"]?.ToString() ?? string.Empty);
            }

            return doc;
        }

        /// <inheritdoc />
        public async Task<List<DocumentMetadata>> GetByContentSha256Async(string tablePrefix, string contentSha256, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $@"
SELECT id, name, content_sha256, document_length, term_count, custom_metadata, indexing_runtime_ms, indexed_utc, last_update_utc, created_utc
FROM {prefix}_documents
WHERE content_sha256 = N'{Sanitizer.Sanitize(contentSha256)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentMetadata> docs = new List<DocumentMetadata>();
            foreach (DataRow row in result.Rows)
            {
                docs.Add(MapRowToDocument(row));
            }
            return docs;
        }

        /// <inheritdoc />
        public async Task<List<DocumentMetadata>> GetAllAsync(string tablePrefix, int limit = 100, int offset = 0, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $@"
SELECT id, name, content_sha256, document_length, term_count, custom_metadata, indexing_runtime_ms, indexed_utc, last_update_utc, created_utc
FROM {prefix}_documents
ORDER BY created_utc DESC
OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentMetadata> docs = new List<DocumentMetadata>();
            foreach (DataRow row in result.Rows)
            {
                docs.Add(MapRowToDocument(row));
            }
            return docs;
        }

        /// <inheritdoc />
        public async Task<List<DocumentMetadata>> GetByIdsAsync(string tablePrefix, IEnumerable<string> ids, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> idList = new List<string>(ids);
            if (idList.Count == 0)
            {
                return new List<DocumentMetadata>();
            }

            string inClause = string.Join(",", idList.ConvertAll(id => $"N'{Sanitizer.Sanitize(id)}'"));
            string query = $@"
SELECT id, name, content_sha256, document_length, term_count, custom_metadata, indexing_runtime_ms, indexed_utc, last_update_utc, created_utc
FROM {prefix}_documents
WHERE id IN ({inClause});";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentMetadata> docs = new List<DocumentMetadata>();
            foreach (DataRow row in result.Rows)
            {
                docs.Add(MapRowToDocument(row));
            }
            return docs;
        }

        /// <inheritdoc />
        public async Task<long> GetCountAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT COUNT(*) FROM {prefix}_documents;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0 ? Convert.ToInt64(result.Rows[0][0]) : 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tablePrefix, string id, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT TOP 1 1 FROM {prefix}_documents WHERE id = N'{Sanitizer.Sanitize(id)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<List<string>> ExistsBatchAsync(string tablePrefix, IEnumerable<string> ids, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> idList = new List<string>(ids);
            if (idList.Count == 0)
            {
                return new List<string>();
            }

            string inClause = string.Join(",", idList.ConvertAll(id => $"N'{Sanitizer.Sanitize(id)}'"));
            string query = $"SELECT id FROM {prefix}_documents WHERE id IN ({inClause});";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<string> existingIds = new List<string>();
            foreach (DataRow row in result.Rows)
            {
                existingIds.Add(row["id"]?.ToString() ?? string.Empty);
            }
            return existingIds;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByNameAsync(string tablePrefix, string name, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string query = $"SELECT TOP 1 1 FROM {prefix}_documents WHERE name = N'{Sanitizer.Sanitize(name)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task UpdateAsync(string tablePrefix, string id, string name, string? contentSha256, int documentLength, int termCount, object? customMetadata = null, decimal? indexingRuntimeMs = null, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            DateTime now = DateTime.UtcNow;
            string? customMetadataJson = customMetadata != null
                ? Sanitizer.Sanitize(JsonSerializer.Serialize(customMetadata))
                : null;

            string query = $@"
UPDATE {prefix}_documents SET
    name = N'{Sanitizer.Sanitize(name)}',
    content_sha256 = {Sanitizer.FormatNullableString(contentSha256)},
    document_length = {documentLength},
    term_count = {termCount},
    custom_metadata = {Sanitizer.FormatNullableString(customMetadataJson)},
    indexing_runtime_ms = {Sanitizer.FormatNullableDecimal(indexingRuntimeMs)},
    last_update_utc = '{Sanitizer.FormatDateTime(now)}'
WHERE id = N'{Sanitizer.Sanitize(id)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task UpdateCustomMetadataAsync(string tablePrefix, string id, object? customMetadata, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            DateTime now = DateTime.UtcNow;
            string? customMetadataJson = customMetadata != null
                ? Sanitizer.Sanitize(JsonSerializer.Serialize(customMetadata))
                : null;

            string query = $@"
UPDATE {prefix}_documents SET
    custom_metadata = {Sanitizer.FormatNullableString(customMetadataJson)},
    last_update_utc = '{Sanitizer.FormatDateTime(now)}'
WHERE id = N'{Sanitizer.Sanitize(id)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tablePrefix, string id, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string countQuery = $"SELECT COUNT(*) FROM {prefix}_documents WHERE id = N'{Sanitizer.Sanitize(id)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            bool exists = countResult.Rows.Count > 0 && Convert.ToInt64(countResult.Rows[0][0]) > 0;
            if (!exists) return false;

            string query = $"DELETE FROM {prefix}_documents WHERE id = N'{Sanitizer.Sanitize(id)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc />
        public async Task<long> DeleteAllAsync(string tablePrefix, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            string countQuery = $"SELECT COUNT(*) FROM {prefix}_documents;";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM {prefix}_documents;";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
        }

        /// <inheritdoc />
        public async Task<List<string>> DeleteBatchAsync(string tablePrefix, IEnumerable<string> ids, CancellationToken token = default)
        {
            string prefix = TablePrefixValidator.Validate(tablePrefix);
            List<string> idList = new List<string>(ids);
            if (idList.Count == 0)
            {
                return new List<string>();
            }

            string inClause = string.Join(",", idList.ConvertAll(id => $"N'{Sanitizer.Sanitize(id)}'"));

            // First get the IDs that actually exist
            string selectQuery = $@"
SELECT id FROM {prefix}_documents
WHERE id IN ({inClause});";
            DataTable result = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);

            List<string> existingIds = new List<string>();
            foreach (DataRow row in result.Rows)
            {
                existingIds.Add(row["id"]?.ToString() ?? string.Empty);
            }

            if (existingIds.Count == 0)
            {
                return new List<string>();
            }

            // Delete all existing documents in a single statement
            string deleteInClause = string.Join(",", existingIds.ConvertAll(id => $"N'{Sanitizer.Sanitize(id)}'"));
            string deleteQuery = $@"
DELETE FROM {prefix}_documents
WHERE id IN ({deleteInClause});";
            await _Driver.ExecuteQueryAsync(deleteQuery, true, token).ConfigureAwait(false);

            return existingIds;
        }

        private static DocumentMetadata MapRowToDocument(DataRow row)
        {
            DocumentMetadata doc = new DocumentMetadata(
                row["id"]?.ToString() ?? string.Empty,
                row["name"]?.ToString() ?? string.Empty
            )
            {
                ContentSha256 = row["content_sha256"]?.ToString() ?? string.Empty,
                DocumentLength = Convert.ToInt32(row["document_length"] ?? 0),
                IndexedDate = row["indexed_utc"] != DBNull.Value ? DateTime.Parse(row["indexed_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o")) : DateTime.UtcNow,
                LastModified = row["last_update_utc"] != DBNull.Value ? DateTime.Parse(row["last_update_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o")) : DateTime.UtcNow,
                IndexingRuntimeMs = row["indexing_runtime_ms"] != DBNull.Value ? Convert.ToDecimal(row["indexing_runtime_ms"]) : null
            };

            string? customMetadataJson = row["custom_metadata"]?.ToString();
            if (!string.IsNullOrEmpty(customMetadataJson))
            {
                try
                {
                    doc.CustomMetadata = JsonSerializer.Deserialize<object>(customMetadataJson);
                }
                catch
                {
                    doc.CustomMetadata = null;
                }
            }

            return doc;
        }
    }
}
