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
    /// SQLite implementation of document-term operations.
    /// </summary>
    public class SqliteDocumentTermMethods : IDocumentTermMethods
    {
        private readonly SqliteIndexRepository _Repository;

        /// <summary>
        /// Creates a new instance of SqliteDocumentTermMethods.
        /// </summary>
        /// <param name="repository">Parent repository instance.</param>
        public SqliteDocumentTermMethods(SqliteIndexRepository repository)
        {
            _Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <inheritdoc/>
        public async Task AddAsync(string documentId, string termId, int termFrequency, List<int> characterPositions, List<int> termPositions, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));
            ArgumentNullException.ThrowIfNull(termId, nameof(termId));
            ArgumentNullException.ThrowIfNull(characterPositions, nameof(characterPositions));
            ArgumentNullException.ThrowIfNull(termPositions, nameof(termPositions));

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);
                string characterPositionsJson = Converters.SerializeIntList(characterPositions);
                string termPositionsJson = Converters.SerializeIntList(termPositions);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentTermQueries.InsertOrReplace();
                cmd.Parameters.AddWithValue("@id", IdGenerator.GenerateDocumentTermId());
                cmd.Parameters.AddWithValue("@documentId", documentId);
                cmd.Parameters.AddWithValue("@termId", termId);
                cmd.Parameters.AddWithValue("@termFrequency", termFrequency);
                cmd.Parameters.AddWithValue("@characterPositions", characterPositionsJson);
                cmd.Parameters.AddWithValue("@termPositions", termPositionsJson);
                cmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                cmd.Parameters.AddWithValue("@createdUtc", now);

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task AddBatchAsync(string documentId, List<(string TermId, int TermFrequency, List<int> CharacterPositions, List<int> TermPositions)> termMappings, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));
            ArgumentNullException.ThrowIfNull(termMappings, nameof(termMappings));

            if (termMappings.Count == 0)
            {
                return;
            }

            await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                string now = Converters.FormatTimestamp(DateTime.UtcNow);

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentTermQueries.InsertBatch(termMappings.Count);
                cmd.Parameters.AddWithValue("@documentId", documentId);
                cmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                cmd.Parameters.AddWithValue("@createdUtc", now);

                for (int i = 0; i < termMappings.Count; i++)
                {
                    (string termId, int termFrequency, List<int> characterPositions, List<int> termPositions) = termMappings[i];
                    cmd.Parameters.AddWithValue($"@id{i}", IdGenerator.GenerateDocumentTermId());
                    cmd.Parameters.AddWithValue($"@termId{i}", termId);
                    cmd.Parameters.AddWithValue($"@termFrequency{i}", termFrequency);
                    cmd.Parameters.AddWithValue($"@characterPositions{i}", Converters.SerializeIntList(characterPositions));
                    cmd.Parameters.AddWithValue($"@termPositions{i}", Converters.SerializeIntList(termPositions));
                }

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentTermRecord>> GetByDocumentAsync(string documentId, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentTermQueries.SelectByDocumentId();
                cmd.Parameters.AddWithValue("@documentId", documentId);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<DocumentTermRecord> results = new List<DocumentTermRecord>();
                foreach (DataRow row in table.Rows)
                {
                    results.Add(Converters.DocumentTermFromRow(row));
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentTermRecord>> GetPostingsAsync(string termId, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(termId, nameof(termId));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentTermQueries.SelectByTermId();
                cmd.Parameters.AddWithValue("@termId", termId);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<DocumentTermRecord> results = new List<DocumentTermRecord>();
                foreach (DataRow row in table.Rows)
                {
                    results.Add(Converters.DocumentTermFromRow(row));
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentTermRecord>> GetPostingsByTermAsync(string term, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(term, nameof(term));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentTermQueries.SelectByTerm();
                cmd.Parameters.AddWithValue("@term", term);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<DocumentTermRecord> results = new List<DocumentTermRecord>();
                foreach (DataRow row in table.Rows)
                {
                    results.Add(Converters.DocumentTermFromRow(row));
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<SearchMatch>> SearchAsync(IEnumerable<string> termIds, bool requireAll, int limit = 100, CancellationToken token = default)
        {
            return await SearchAsync(termIds, requireAll, null, null, limit, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<SearchMatch>> SearchAsync(
            IEnumerable<string> termIds,
            bool requireAll,
            List<string>? labels,
            Dictionary<string, string>? tags,
            int limit = 100,
            CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(termIds, nameof(termIds));

            List<string> termIdList = new List<string>(termIds);
            if (termIdList.Count == 0)
            {
                return new List<SearchMatch>();
            }

            int labelCount = labels?.Count ?? 0;
            int tagCount = tags?.Count ?? 0;

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();

                if (labelCount == 0 && tagCount == 0)
                {
                    if (requireAll)
                    {
                        cmd.CommandText = DocumentTermQueries.SelectDocumentsWithAllTerms(termIdList.Count);
                    }
                    else
                    {
                        cmd.CommandText = DocumentTermQueries.SelectDocumentsWithAnyTerms(termIdList.Count);
                    }
                }
                else
                {
                    if (requireAll)
                    {
                        cmd.CommandText = DocumentTermQueries.SelectDocumentsWithAllTermsFiltered(termIdList.Count, labelCount, tagCount);
                    }
                    else
                    {
                        cmd.CommandText = DocumentTermQueries.SelectDocumentsWithAnyTermsFiltered(termIdList.Count, labelCount, tagCount);
                    }
                }

                for (int i = 0; i < termIdList.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@termId{i}", termIdList[i]);
                }

                if (labels != null)
                {
                    for (int i = 0; i < labels.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@label{i}", labels[i].ToLowerInvariant());
                    }
                }

                if (tags != null)
                {
                    int i = 0;
                    foreach (KeyValuePair<string, string> tag in tags)
                    {
                        cmd.Parameters.AddWithValue($"@tagKey{i}", tag.Key);
                        cmd.Parameters.AddWithValue($"@tagValue{i}", tag.Value);
                        i++;
                    }
                }

                cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit));

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                List<SearchMatch> results = new List<SearchMatch>();
                foreach (DataRow row in table.Rows)
                {
                    results.Add(new SearchMatch
                    {
                        DocumentId = Converters.GetString(row, "document_id") ?? string.Empty,
                        TotalFrequency = Converters.GetInt(row, "total_frequency"),
                        MatchedTermCount = Converters.GetInt(row, "matched_terms")
                    });
                }
                return results;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<long> DeleteByDocumentAsync(string documentId, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentId, nameof(documentId));

            return await _Repository.ExecuteWriteAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentTermQueries.DeleteByDocumentId();
                cmd.Parameters.AddWithValue("@documentId", documentId);

                int affected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return (long)affected;
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentTermRecord>> GetByDocumentsAndTermsAsync(IEnumerable<string> documentIds, IEnumerable<string> termIds, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(documentIds, nameof(documentIds));
            ArgumentNullException.ThrowIfNull(termIds, nameof(termIds));

            List<string> docIdList = new List<string>(documentIds);
            List<string> termIdList = new List<string>(termIds);

            if (docIdList.Count == 0 || termIdList.Count == 0)
            {
                return new List<DocumentTermRecord>();
            }

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = DocumentTermQueries.SelectByDocumentsAndTerms(docIdList.Count, termIdList.Count);

                for (int i = 0; i < docIdList.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@docId{i}", docIdList[i]);
                }

                for (int i = 0; i < termIdList.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@termId{i}", termIdList[i]);
                }

                List<DocumentTermRecord> results = new List<DocumentTermRecord>();
                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    results.Add(new DocumentTermRecord
                    {
                        DocumentId = reader.GetString(0),
                        TermId = reader.GetString(1),
                        TermFrequency = reader.GetInt32(2)
                    });
                }
                return results;
            }, token).ConfigureAwait(false);
        }
    }
}
