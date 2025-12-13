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
    /// SQLite implementation of label operations.
    /// </summary>
    public class SqliteLabelMethods : ILabelMethods
    {
        private readonly SqliteIndexRepository _Repository;

        /// <summary>
        /// Creates a new instance of SqliteLabelMethods.
        /// </summary>
        /// <param name="repository">Parent repository instance.</param>
        public SqliteLabelMethods(SqliteIndexRepository repository)
        {
            _Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <inheritdoc/>
        public async Task AddAsync(string? documentId, string label, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(label, nameof(label));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = LabelQueries.InsertIfNotExists();
                cmd.Parameters.AddWithValue("@id", IdGenerator.GenerateLabelId());
                cmd.Parameters.AddWithValue("@documentId", (object?)documentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@label", label);
                cmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                cmd.Parameters.AddWithValue("@createdUtc", now);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetByDocumentAsync(string documentId, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = LabelQueries.SelectByDocumentId();
                cmd.Parameters.AddWithValue("@documentId", documentId);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<string> results = new List<string>();
                foreach (DataRow row in table.Rows)
                {
                    string? label = Converters.GetString(row, "label");
                    if (label != null)
                    {
                        results.Add(label);
                    }
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetIndexLabelsAsync(CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = LabelQueries.SelectIndexLabels();

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<string> results = new List<string>();
                foreach (DataRow row in table.Rows)
                {
                    string? label = Converters.GetString(row, "label");
                    if (label != null)
                    {
                        results.Add(label);
                    }
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetAllDistinctAsync(CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = LabelQueries.SelectDistinct();

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<string> results = new List<string>();
                foreach (DataRow row in table.Rows)
                {
                    string? label = Converters.GetString(row, "label");
                    if (label != null)
                    {
                        results.Add(label);
                    }
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentMetadata>> GetDocumentsByLabelAsync(string label, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(label, nameof(label));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = LabelQueries.SelectDocumentsByLabel();
                cmd.Parameters.AddWithValue("@label", label);

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
        public async Task<bool> ExistsAsync(string? documentId, string label, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(label, nameof(label));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = LabelQueries.Exists();
                cmd.Parameters.AddWithValue("@documentId", (object?)documentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@label", label);

                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result != null;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveAsync(string? documentId, string label, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(label, nameof(label));

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = LabelQueries.DeleteByDocumentAndLabel();
                cmd.Parameters.AddWithValue("@documentId", (object?)documentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@label", label);

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
                cmd.CommandText = LabelQueries.DeleteByDocumentId();
                cmd.Parameters.AddWithValue("@documentId", documentId);

                int affected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return (long)affected;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task AddBatchAsync(string documentId, List<string> labels, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));
            ArgumentNullException.ThrowIfNull(labels, nameof(labels));

            if (labels.Count == 0)
            {
                return;
            }

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = LabelQueries.InsertBatch(labels.Count);
                cmd.Parameters.AddWithValue("@documentId", documentId);
                cmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                cmd.Parameters.AddWithValue("@createdUtc", now);

                for (int i = 0; i < labels.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@id{i}", IdGenerator.GenerateLabelId());
                    cmd.Parameters.AddWithValue($"@label{i}", labels[i]);
                }

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ReplaceAsync(string documentId, List<string> labels, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));
            ArgumentNullException.ThrowIfNull(labels, nameof(labels));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                // Delete existing labels
                using (SqliteCommand deleteCmd = connection.CreateCommand())
                {
                    deleteCmd.CommandText = LabelQueries.DeleteByDocumentId();
                    deleteCmd.Parameters.AddWithValue("@documentId", documentId);
                    await deleteCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                // Insert new labels if any
                if (labels.Count > 0)
                {
                    string now = Converters.FormatTimestamp(DateTime.UtcNow);

                    using SqliteCommand insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = LabelQueries.InsertBatch(labels.Count);
                    insertCmd.Parameters.AddWithValue("@documentId", documentId);
                    insertCmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                    insertCmd.Parameters.AddWithValue("@createdUtc", now);

                    for (int i = 0; i < labels.Count; i++)
                    {
                        insertCmd.Parameters.AddWithValue($"@id{i}", IdGenerator.GenerateLabelId());
                        insertCmd.Parameters.AddWithValue($"@label{i}", labels[i]);
                    }

                    await insertCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }, token).ConfigureAwait(false);
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
