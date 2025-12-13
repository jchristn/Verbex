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
    /// SQLite implementation of term operations.
    /// </summary>
    public class SqliteTermMethods : ITermMethods
    {
        private readonly SqliteIndexRepository _Repository;

        /// <summary>
        /// Creates a new instance of SqliteTermMethods.
        /// </summary>
        /// <param name="repository">Parent repository instance.</param>
        public SqliteTermMethods(SqliteIndexRepository repository)
        {
            _Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <inheritdoc/>
        public async Task<string> AddOrGetAsync(string term, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(term, nameof(term));

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                // First check if term already exists
                using (SqliteCommand selectCmd = connection.CreateCommand())
                {
                    selectCmd.CommandText = TermQueries.SelectByTerm();
                    selectCmd.Parameters.AddWithValue("@term", term);

                    using SqliteDataReader reader = await selectCmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                    DataTable table = new DataTable();
                    table.Load(reader);

                    if (table.Rows.Count > 0)
                    {
                        return Converters.GetString(table.Rows[0], "id") ?? throw new InvalidOperationException($"Term has null ID: {term}");
                    }
                }

                // Term doesn't exist, insert with new ID
                string now = Converters.FormatTimestamp(DateTime.UtcNow);
                string newId = IdGenerator.GenerateTermId();

                using (SqliteCommand insertCmd = connection.CreateCommand())
                {
                    insertCmd.CommandText = TermQueries.Insert();
                    insertCmd.Parameters.AddWithValue("@id", newId);
                    insertCmd.Parameters.AddWithValue("@term", term);
                    insertCmd.Parameters.AddWithValue("@lastUpdatedUtc", now);
                    insertCmd.Parameters.AddWithValue("@createdUtc", now);
                    await insertCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                return newId;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<TermRecord?> GetAsync(string term, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(term, nameof(term));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.SelectByTerm();
                cmd.Parameters.AddWithValue("@term", term);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return null;
                }

                return Converters.TermFromRow(table.Rows[0]);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<TermRecord?> GetByIdAsync(string id, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.SelectById();
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return null;
                }

                return Converters.TermFromRow(table.Rows[0]);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, TermRecord>> GetMultipleAsync(IEnumerable<string> terms, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(terms, nameof(terms));

            List<string> termList = new List<string>(terms);
            if (termList.Count == 0)
            {
                return new Dictionary<string, TermRecord>();
            }

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.SelectByTerms(termList.Count);

                for (int i = 0; i < termList.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@term{i}", termList[i]);
                }

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                Dictionary<string, TermRecord> results = new Dictionary<string, TermRecord>();
                foreach (DataRow row in table.Rows)
                {
                    TermRecord record = Converters.TermFromRow(row);
                    results[record.Term] = record;
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<TermRecord>> GetByPrefixAsync(string prefix, int limit = 100, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(prefix, nameof(prefix));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.SelectByPrefix();
                cmd.Parameters.AddWithValue("@pattern", Sanitizer.CreatePrefixPattern(prefix));
                cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit));

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<TermRecord> results = new List<TermRecord>();
                foreach (DataRow row in table.Rows)
                {
                    results.Add(Converters.TermFromRow(row));
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
                cmd.CommandText = TermQueries.Count();

                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return Convert.ToInt64(result);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(string term, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(term, nameof(term));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.Exists();
                cmd.Parameters.AddWithValue("@term", term);

                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result != null;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task UpdateFrequenciesAsync(string termId, int documentFrequency, int totalFrequency, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(termId, nameof(termId));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.UpdateFrequencies();
                cmd.Parameters.AddWithValue("@id", termId);
                cmd.Parameters.AddWithValue("@documentFrequency", documentFrequency);
                cmd.Parameters.AddWithValue("@totalFrequency", totalFrequency);
                cmd.Parameters.AddWithValue("@lastUpdatedUtc", now);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task IncrementFrequenciesAsync(string termId, int documentFrequencyDelta, int totalFrequencyDelta, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(termId, nameof(termId));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.IncrementFrequencies();
                cmd.Parameters.AddWithValue("@id", termId);
                cmd.Parameters.AddWithValue("@documentFrequencyDelta", documentFrequencyDelta);
                cmd.Parameters.AddWithValue("@totalFrequencyDelta", totalFrequencyDelta);
                cmd.Parameters.AddWithValue("@lastUpdatedUtc", now);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> AddOrGetBatchAsync(List<string> terms, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(terms, nameof(terms));

            if (terms.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                // Step 1: First fetch existing terms to see which ones already exist
                Dictionary<string, string> result = new Dictionary<string, string>();

                using (SqliteCommand selectCmd = connection.CreateCommand())
                {
                    selectCmd.CommandText = TermQueries.SelectByTerms(terms.Count);
                    for (int i = 0; i < terms.Count; i++)
                    {
                        selectCmd.Parameters.AddWithValue($"@term{i}", terms[i]);
                    }

                    using SqliteDataReader reader = await selectCmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                    DataTable table = new DataTable();
                    table.Load(reader);

                    foreach (DataRow row in table.Rows)
                    {
                        TermRecord termRecord = Converters.TermFromRow(row);
                        result[termRecord.Term] = termRecord.Id;
                    }
                }

                // Step 2: Insert terms that don't exist
                List<string> newTerms = new List<string>();
                foreach (string term in terms)
                {
                    if (!result.ContainsKey(term))
                    {
                        newTerms.Add(term);
                    }
                }

                if (newTerms.Count > 0)
                {
                    using SqliteCommand insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = TermQueries.InsertBatchIgnore(newTerms.Count);
                    insertCmd.Parameters.AddWithValue("@lastUpdatedUtc", now);
                    insertCmd.Parameters.AddWithValue("@createdUtc", now);

                    for (int i = 0; i < newTerms.Count; i++)
                    {
                        string newId = IdGenerator.GenerateTermId();
                        insertCmd.Parameters.AddWithValue($"@id{i}", newId);
                        insertCmd.Parameters.AddWithValue($"@term{i}", newTerms[i]);
                        result[newTerms[i]] = newId;
                    }

                    await insertCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                return result;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task IncrementFrequenciesBatchAsync(Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> updates, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(updates, nameof(updates));

            if (updates.Count == 0)
            {
                return;
            }

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.IncrementFrequenciesBatch(updates.Count);

                int i = 0;
                foreach (KeyValuePair<string, (int DocFreqDelta, int TotalFreqDelta)> kvp in updates)
                {
                    cmd.Parameters.AddWithValue($"@id{i}", kvp.Key);
                    cmd.Parameters.AddWithValue($"@docFreqDelta{i}", kvp.Value.DocFreqDelta);
                    cmd.Parameters.AddWithValue($"@totalFreqDelta{i}", kvp.Value.TotalFreqDelta);
                    i++;
                }
                cmd.Parameters.AddWithValue("@lastUpdatedUtc", now);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DecrementFrequenciesBatchAsync(Dictionary<string, (int DocFreqDelta, int TotalFreqDelta)> updates, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(updates, nameof(updates));

            if (updates.Count == 0)
            {
                return;
            }

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.DecrementFrequenciesBatch(updates.Count);

                int i = 0;
                foreach (KeyValuePair<string, (int DocFreqDelta, int TotalFreqDelta)> kvp in updates)
                {
                    cmd.Parameters.AddWithValue($"@id{i}", kvp.Key);
                    cmd.Parameters.AddWithValue($"@docFreqDelta{i}", kvp.Value.DocFreqDelta);
                    cmd.Parameters.AddWithValue($"@totalFreqDelta{i}", kvp.Value.TotalFreqDelta);
                    i++;
                }
                cmd.Parameters.AddWithValue("@lastUpdatedUtc", now);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<TermRecord>> GetTopAsync(int limit = 100, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.GetTopTerms();
                cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit));

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<TermRecord> results = new List<TermRecord>();
                foreach (DataRow row in table.Rows)
                {
                    results.Add(Converters.TermFromRow(row));
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<long> DeleteOrphanedAsync(CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = TermQueries.DeleteOrphaned();

                int affected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return (long)affected;
            }, token).ConfigureAwait(false);
        }
    }
}
