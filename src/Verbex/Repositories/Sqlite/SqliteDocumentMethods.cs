namespace Verbex.Repositories.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Verbex.Repositories.Interfaces;
    using Verbex.Repositories.Queries;
    using Verbex.Utilities;

    /// <summary>
    /// SQLite implementation of document operations.
    /// </summary>
    public class SqliteDocumentMethods : IDocumentMethods
    {
        private readonly SqliteIndexRepository _Repository;

        /// <summary>
        /// Creates a new instance of SqliteDocumentMethods.
        /// </summary>
        /// <param name="repository">Parent repository instance.</param>
        public SqliteDocumentMethods(SqliteIndexRepository repository)
        {
            _Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <inheritdoc/>
        public async Task AddAsync(string id, string name, string? contentSha256, int documentLength, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            ArgumentNullException.ThrowIfNull(name, nameof(name));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.Insert();
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@contentSha256", (object?)contentSha256 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@documentLength", documentLength);
                cmd.Parameters.AddWithValue("@termCount", 0);
                cmd.Parameters.AddWithValue("@indexedUtc", now);
                cmd.Parameters.AddWithValue("@lastModifiedUtc", DBNull.Value);
                cmd.Parameters.AddWithValue("@createdUtc", now);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                await _Repository.UpdateLastModifiedAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<DocumentMetadata?> GetAsync(string id, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.SelectById();
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return null;
                }

                DocumentRecord record = Converters.DocumentFromRow(table.Rows[0]);
                return RecordToMetadata(record);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<DocumentMetadata?> GetByNameAsync(string name, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(name, nameof(name));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.SelectByName();
                cmd.Parameters.AddWithValue("@name", name);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return null;
                }

                DocumentRecord record = Converters.DocumentFromRow(table.Rows[0]);
                return RecordToMetadata(record);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<DocumentMetadata?> GetWithMetadataAsync(string id, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.SelectByIdWithMetadata();
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return null;
                }

                DataRow row = table.Rows[0];
                DocumentRecord record = Converters.DocumentFromRow(row);
                DocumentMetadata metadata = RecordToMetadata(record);

                // Parse labels from CSV
                string? labelsCsv = Converters.GetString(row, "labels_csv");
                if (!string.IsNullOrEmpty(labelsCsv))
                {
                    foreach (string label in labelsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        metadata.AddLabel(label.Trim());
                    }
                }

                // Parse tags from CSV (key=value format)
                string? tagsCsv = Converters.GetString(row, "tags_csv");
                if (!string.IsNullOrEmpty(tagsCsv))
                {
                    foreach (string tagPair in tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        int eqIndex = tagPair.IndexOf('=');
                        if (eqIndex > 0)
                        {
                            string key = tagPair.Substring(0, eqIndex).Trim();
                            string value = eqIndex < tagPair.Length - 1 ? tagPair.Substring(eqIndex + 1).Trim() : "";
                            metadata.SetTag(key, value);
                        }
                    }
                }

                // Parse terms from CSV
                string? termsCsv = Converters.GetString(row, "terms_csv");
                if (!string.IsNullOrEmpty(termsCsv))
                {
                    foreach (string term in termsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        metadata.AddTerm(term.Trim());
                    }
                }

                return metadata;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentMetadata>> GetByContentSha256Async(string contentSha256, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(contentSha256, nameof(contentSha256));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.SelectByContentSha256();
                cmd.Parameters.AddWithValue("@contentSha256", contentSha256);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<DocumentMetadata> results = new List<DocumentMetadata>();
                foreach (DataRow row in table.Rows)
                {
                    DocumentRecord record = Converters.DocumentFromRow(row);
                    results.Add(RecordToMetadata(record));
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentMetadata>> GetAllAsync(int limit = 100, int offset = 0, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            (int validLimit, int validOffset) = Sanitizer.ValidatePagination(limit, offset);

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.SelectAll();
                cmd.Parameters.AddWithValue("@limit", validLimit);
                cmd.Parameters.AddWithValue("@offset", validOffset);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<DocumentMetadata> results = new List<DocumentMetadata>();
                foreach (DataRow row in table.Rows)
                {
                    DocumentRecord record = Converters.DocumentFromRow(row);
                    results.Add(RecordToMetadata(record));
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentMetadata>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(ids, nameof(ids));

            List<string> idList = new List<string>(ids);
            if (idList.Count == 0)
            {
                return new List<DocumentMetadata>();
            }

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.SelectByIds(idList.Count);

                for (int i = 0; i < idList.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@id{i}", idList[i]);
                }

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<DocumentMetadata> results = new List<DocumentMetadata>();
                foreach (DataRow row in table.Rows)
                {
                    DocumentRecord record = Converters.DocumentFromRow(row);
                    results.Add(RecordToMetadata(record));
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<long> GetCountAsync(CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.Count();

                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return Convert.ToInt64(result);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.ExistsById();
                cmd.Parameters.AddWithValue("@id", id);

                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result != null;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsByNameAsync(string name, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(name, nameof(name));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.ExistsByName();
                cmd.Parameters.AddWithValue("@name", name);

                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result != null;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(string id, string name, string? contentSha256, int documentLength, int termCount, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            ArgumentNullException.ThrowIfNull(name, nameof(name));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.Update();
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@contentSha256", (object?)contentSha256 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@documentLength", documentLength);
                cmd.Parameters.AddWithValue("@termCount", termCount);
                cmd.Parameters.AddWithValue("@lastModifiedUtc", now);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                await _Repository.UpdateLastModifiedAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.DeleteById();
                cmd.Parameters.AddWithValue("@id", id);

                int affected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                if (affected > 0)
                {
                    await _Repository.UpdateLastModifiedAsync(token).ConfigureAwait(false);
                }

                return affected > 0;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<long> DeleteAllAsync(CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                long count = await GetCountInternalAsync(connection, token).ConfigureAwait(false);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentQueries.DeleteAll();
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                await _Repository.UpdateLastModifiedAsync(token).ConfigureAwait(false);
                return count;
            }, token).ConfigureAwait(false);
        }

        private static async Task<long> GetCountInternalAsync(SqliteConnection connection, CancellationToken token)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = DocumentQueries.Count();
            object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
            return Convert.ToInt64(result);
        }

        private static DocumentMetadata RecordToMetadata(DocumentRecord record)
        {
            return new DocumentMetadata(record.Id, record.Name)
            {
                ContentSha256 = record.ContentSha256 ?? string.Empty,
                DocumentLength = record.DocumentLength,
                IndexedDate = record.IndexedUtc,
                LastModified = record.LastModifiedUtc ?? record.IndexedUtc
            };
        }
    }
}
