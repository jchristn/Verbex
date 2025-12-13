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
    /// SQLite implementation of tag operations.
    /// </summary>
    public class SqliteTagMethods : ITagMethods
    {
        private readonly SqliteIndexRepository _Repository;

        /// <summary>
        /// Creates a new instance of SqliteTagMethods.
        /// </summary>
        /// <param name="repository">Parent repository instance.</param>
        public SqliteTagMethods(SqliteIndexRepository repository)
        {
            _Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <inheritdoc/>
        public async Task SetAsync(string? documentId, string key, string? value, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                bool exists = await TagExistsInternalAsync(connection, documentId, key, token).ConfigureAwait(false);

                using SqliteCommand cmd = connection.CreateCommand();

                if (exists)
                {
                    cmd.CommandText = TagQueries.Update();
                    cmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                }
                else
                {
                    cmd.CommandText = TagQueries.Insert();
                    cmd.Parameters.AddWithValue("@id", IdGenerator.GenerateTagId());
                    cmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                    cmd.Parameters.AddWithValue("@createdUtc", now);
                }

                cmd.Parameters.AddWithValue("@documentId", (object?)documentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@value", (object?)value ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string?> GetAsync(string? documentId, string key, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.SelectByDocumentAndKey();
                cmd.Parameters.AddWithValue("@documentId", (object?)documentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@key", key);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return null;
                }

                return Converters.GetString(table.Rows[0], "value");
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string?>> GetByDocumentAsync(string documentId, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.SelectByDocumentId();
                cmd.Parameters.AddWithValue("@documentId", documentId);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                Dictionary<string, string?> results = new Dictionary<string, string?>();
                foreach (DataRow row in table.Rows)
                {
                    string? key = Converters.GetString(row, "key");
                    string? value = Converters.GetString(row, "value");
                    if (key != null)
                    {
                        results[key] = value;
                    }
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string?>> GetIndexTagsAsync(CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.SelectIndexTags();

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                Dictionary<string, string?> results = new Dictionary<string, string?>();
                foreach (DataRow row in table.Rows)
                {
                    string? key = Converters.GetString(row, "key");
                    string? value = Converters.GetString(row, "value");
                    if (key != null)
                    {
                        results[key] = value;
                    }
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetAllDistinctKeysAsync(CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.SelectDistinctKeys();

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<string> results = new List<string>();
                foreach (DataRow row in table.Rows)
                {
                    string? key = Converters.GetString(row, "key");
                    if (key != null)
                    {
                        results.Add(key);
                    }
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentMetadata>> GetDocumentsByKeyAsync(string key, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.SelectDocumentsByTagKey();
                cmd.Parameters.AddWithValue("@key", key);

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
        public async Task<List<DocumentMetadata>> GetDocumentsByTagAsync(string key, string value, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(value, nameof(value));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.SelectDocumentsByTagKeyValue();
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@value", value);

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
        public async Task<bool> ExistsAsync(string? documentId, string key, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                return await TagExistsInternalAsync(connection, documentId, key, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveAsync(string? documentId, string key, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.DeleteByDocumentAndKey();
                cmd.Parameters.AddWithValue("@documentId", (object?)documentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@key", key);

                int affected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return affected > 0;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<long> RemoveAllAsync(string documentId, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.DeleteByDocumentId();
                cmd.Parameters.AddWithValue("@documentId", documentId);

                int affected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return (long)affected;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task AddBatchAsync(string documentId, Dictionary<string, string> tags, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));
            ArgumentNullException.ThrowIfNull(tags, nameof(tags));

            if (tags.Count == 0)
            {
                return;
            }

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TagQueries.InsertOrReplaceBatch(tags.Count);
                cmd.Parameters.AddWithValue("@documentId", documentId);
                cmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                cmd.Parameters.AddWithValue("@createdUtc", now);

                int i = 0;
                foreach (KeyValuePair<string, string> kvp in tags)
                {
                    cmd.Parameters.AddWithValue($"@id{i}", IdGenerator.GenerateTagId());
                    cmd.Parameters.AddWithValue($"@key{i}", kvp.Key);
                    cmd.Parameters.AddWithValue($"@value{i}", kvp.Value ?? (object)DBNull.Value);
                    i++;
                }

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ReplaceAsync(string documentId, Dictionary<string, string> tags, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));
            ArgumentNullException.ThrowIfNull(tags, nameof(tags));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                // Delete existing tags
                using (SqliteCommand deleteCmd = connection.CreateCommand())
                {
                    deleteCmd.CommandText = TagQueries.DeleteByDocumentId();
                    deleteCmd.Parameters.AddWithValue("@documentId", documentId);
                    await deleteCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                // Insert new tags if any
                if (tags.Count > 0)
                {
                    string now = Converters.FormatTimestamp(DateTime.UtcNow);

                    using SqliteCommand insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = TagQueries.InsertOrReplaceBatch(tags.Count);
                    insertCmd.Parameters.AddWithValue("@documentId", documentId);
                    insertCmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                    insertCmd.Parameters.AddWithValue("@createdUtc", now);

                    int i = 0;
                    foreach (KeyValuePair<string, string> kvp in tags)
                    {
                        insertCmd.Parameters.AddWithValue($"@id{i}", IdGenerator.GenerateTagId());
                        insertCmd.Parameters.AddWithValue($"@key{i}", kvp.Key);
                        insertCmd.Parameters.AddWithValue($"@value{i}", kvp.Value ?? (object)DBNull.Value);
                        i++;
                    }

                    await insertCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }, token).ConfigureAwait(false);
        }

        private static async Task<bool> TagExistsInternalAsync(SqliteConnection connection, string? documentId, string key, CancellationToken token)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = TagQueries.Exists();
            cmd.Parameters.AddWithValue("@documentId", (object?)documentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@key", key);

            object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
            return result != null;
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
