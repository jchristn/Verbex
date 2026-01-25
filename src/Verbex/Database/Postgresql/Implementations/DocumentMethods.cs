namespace Verbex.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.Utilities;

    /// <summary>
    /// PostgreSQL implementation of document methods.
    /// </summary>
    internal class DocumentMethods : IDocumentMethods
    {
        private readonly PostgresqlDatabaseDriver _Driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentMethods"/> class.
        /// </summary>
        /// <param name="driver">The database driver.</param>
        public DocumentMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task AddAsync(string tenantId, string indexId, string id, string name, string? contentSha256, int documentLength, object? customMetadata = null, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string? customMetadataJson = customMetadata != null
                ? Sanitizer.Sanitize(JsonSerializer.Serialize(customMetadata))
                : null;

            string query = $@"
INSERT INTO documents (id, tenant_id, index_id, name, content_sha256, document_length, term_count, custom_metadata, indexed_utc, last_update_utc, created_utc)
VALUES (
    '{Sanitizer.Sanitize(id)}',
    '{Sanitizer.Sanitize(tenantId)}',
    '{Sanitizer.Sanitize(indexId)}',
    '{Sanitizer.Sanitize(name)}',
    {Sanitizer.FormatNullableString(contentSha256)},
    {documentLength},
    0,
    {Sanitizer.FormatNullableString(customMetadataJson)},
    '{Sanitizer.FormatDateTime(now)}',
    '{Sanitizer.FormatDateTime(now)}',
    '{Sanitizer.FormatDateTime(now)}'
);";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<DocumentMetadata?> GetAsync(string tenantId, string indexId, string id, CancellationToken token = default)
        {
            string query = $@"
SELECT id, tenant_id, index_id, name, content_sha256, document_length, term_count, custom_metadata, indexed_utc, last_update_utc, created_utc
FROM documents
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(id)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToDocument(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<DocumentMetadata?> GetByNameAsync(string tenantId, string indexId, string name, CancellationToken token = default)
        {
            string query = $@"
SELECT id, tenant_id, index_id, name, content_sha256, document_length, term_count, custom_metadata, indexed_utc, last_update_utc, created_utc
FROM documents
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND name = '{Sanitizer.Sanitize(name)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result.Rows.Count == 0)
            {
                return null;
            }

            return MapRowToDocument(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<DocumentMetadata?> GetWithMetadataAsync(string tenantId, string indexId, string id, CancellationToken token = default)
        {
            DocumentMetadata? doc = await GetAsync(tenantId, indexId, id, token).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            string labelsQuery = $"SELECT label FROM labels WHERE document_id = '{Sanitizer.Sanitize(id)}';";
            DataTable labelsResult = await _Driver.ExecuteQueryAsync(labelsQuery, false, token).ConfigureAwait(false);
            foreach (DataRow row in labelsResult.Rows)
            {
                doc.AddLabel(row["label"]?.ToString() ?? string.Empty);
            }

            string tagsQuery = $"SELECT key, value FROM tags WHERE document_id = '{Sanitizer.Sanitize(id)}';";
            DataTable tagsResult = await _Driver.ExecuteQueryAsync(tagsQuery, false, token).ConfigureAwait(false);
            foreach (DataRow row in tagsResult.Rows)
            {
                doc.SetTag(row["key"]?.ToString() ?? string.Empty, row["value"]?.ToString() ?? string.Empty);
            }

            return doc;
        }

        /// <inheritdoc />
        public async Task<List<DocumentMetadata>> GetByContentSha256Async(string tenantId, string indexId, string contentSha256, CancellationToken token = default)
        {
            string query = $@"
SELECT id, tenant_id, index_id, name, content_sha256, document_length, term_count, custom_metadata, indexed_utc, last_update_utc, created_utc
FROM documents
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND content_sha256 = '{Sanitizer.Sanitize(contentSha256)}';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentMetadata> docs = new List<DocumentMetadata>();
            foreach (DataRow row in result.Rows)
            {
                docs.Add(MapRowToDocument(row));
            }
            return docs;
        }

        /// <inheritdoc />
        public async Task<List<DocumentMetadata>> GetAllAsync(string tenantId, string indexId, int limit = 100, int offset = 0, CancellationToken token = default)
        {
            string query = $@"
SELECT id, tenant_id, index_id, name, content_sha256, document_length, term_count, custom_metadata, indexed_utc, last_update_utc, created_utc
FROM documents
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}'
ORDER BY created_utc DESC
LIMIT {limit} OFFSET {offset};";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentMetadata> docs = new List<DocumentMetadata>();
            foreach (DataRow row in result.Rows)
            {
                docs.Add(MapRowToDocument(row));
            }
            return docs;
        }

        /// <inheritdoc />
        public async Task<List<DocumentMetadata>> GetByIdsAsync(string tenantId, string indexId, IEnumerable<string> ids, CancellationToken token = default)
        {
            List<string> idList = new List<string>(ids);
            if (idList.Count == 0)
            {
                return new List<DocumentMetadata>();
            }

            string inClause = string.Join(",", idList.ConvertAll(id => $"'{Sanitizer.Sanitize(id)}'"));
            string query = $@"
SELECT id, tenant_id, index_id, name, content_sha256, document_length, term_count, custom_metadata, indexed_utc, last_update_utc, created_utc
FROM documents
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id IN ({inClause});";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<DocumentMetadata> docs = new List<DocumentMetadata>();
            foreach (DataRow row in result.Rows)
            {
                docs.Add(MapRowToDocument(row));
            }
            return docs;
        }

        /// <inheritdoc />
        public async Task<long> GetCountAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string query = $"SELECT COUNT(*) FROM documents WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0 ? Convert.ToInt64(result.Rows[0][0]) : 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string indexId, string id, CancellationToken token = default)
        {
            string query = $"SELECT 1 FROM documents WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(id)}' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByNameAsync(string tenantId, string indexId, string name, CancellationToken token = default)
        {
            string query = $"SELECT 1 FROM documents WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND name = '{Sanitizer.Sanitize(name)}' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task UpdateAsync(string tenantId, string indexId, string id, string name, string? contentSha256, int documentLength, int termCount, object? customMetadata = null, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string? customMetadataJson = customMetadata != null
                ? Sanitizer.Sanitize(JsonSerializer.Serialize(customMetadata))
                : null;

            string query = $@"
UPDATE documents SET
    name = '{Sanitizer.Sanitize(name)}',
    content_sha256 = {Sanitizer.FormatNullableString(contentSha256)},
    document_length = {documentLength},
    term_count = {termCount},
    custom_metadata = {Sanitizer.FormatNullableString(customMetadataJson)},
    last_update_utc = '{Sanitizer.FormatDateTime(now)}'
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(id)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task UpdateCustomMetadataAsync(string tenantId, string indexId, string id, object? customMetadata, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string? customMetadataJson = customMetadata != null
                ? Sanitizer.Sanitize(JsonSerializer.Serialize(customMetadata))
                : null;

            string query = $@"
UPDATE documents SET
    custom_metadata = {Sanitizer.FormatNullableString(customMetadataJson)},
    last_update_utc = '{Sanitizer.FormatDateTime(now)}'
WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(id)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string indexId, string id, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM documents WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(id)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            bool exists = countResult.Rows.Count > 0 && Convert.ToInt64(countResult.Rows[0][0]) > 0;
            if (!exists) return false;

            string query = $"DELETE FROM documents WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}' AND id = '{Sanitizer.Sanitize(id)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc />
        public async Task<long> DeleteAllAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            string countQuery = $"SELECT COUNT(*) FROM documents WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0]) : 0;

            string query = $"DELETE FROM documents WHERE tenant_id = '{Sanitizer.Sanitize(tenantId)}' AND index_id = '{Sanitizer.Sanitize(indexId)}';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return count;
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
                LastModified = row["last_update_utc"] != DBNull.Value ? DateTime.Parse(row["last_update_utc"]?.ToString() ?? DateTime.UtcNow.ToString("o")) : DateTime.UtcNow
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
