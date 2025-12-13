namespace Verbex.Repositories.Sqlite
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Verbex.Repositories.Interfaces;
    using Verbex.Repositories.Queries;

    /// <summary>
    /// SQLite implementation of statistics operations.
    /// </summary>
    public class SqliteStatisticsMethods : IStatisticsMethods
    {
        private readonly SqliteIndexRepository _Repository;

        /// <summary>
        /// Creates a new instance of SqliteStatisticsMethods.
        /// </summary>
        /// <param name="repository">Parent repository instance.</param>
        public SqliteStatisticsMethods(SqliteIndexRepository repository)
        {
            _Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <inheritdoc/>
        public async Task<IndexStatistics> GetIndexStatisticsAsync(CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = StatisticsQueries.GetIndexStatistics();

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return new IndexStatistics();
                }

                DataRow row = table.Rows[0];
                return new IndexStatistics
                {
                    DocumentCount = Converters.GetLong(row, "document_count"),
                    TermCount = Converters.GetLong(row, "term_count"),
                    TotalTermOccurrences = Converters.GetLong(row, "total_term_occurrences"),
                    AverageTermsPerDocument = Converters.GetDouble(row, "avg_terms_per_document"),
                    AverageDocumentFrequency = Converters.GetDouble(row, "avg_document_frequency"),
                    MaxDocumentFrequency = Converters.GetLong(row, "max_document_frequency"),
                    MinDocumentLength = Converters.GetLong(row, "min_document_length"),
                    MaxDocumentLength = Converters.GetLong(row, "max_document_length"),
                    AverageDocumentLength = Converters.GetDouble(row, "avg_document_length")
                };
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<TermStatisticsResult?> GetTermStatisticsAsync(string term, CancellationToken token = default)
        {
            _Repository.ThrowIfDisposed();
            _Repository.ThrowIfNotOpen();
            ArgumentNullException.ThrowIfNull(term, nameof(term));

            return await _Repository.ExecuteReadAsync(async (connection) =>
            {
                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = StatisticsQueries.GetTermStatistics();
                cmd.Parameters.AddWithValue("@term", term);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return null;
                }

                DataRow row = table.Rows[0];
                long totalDocuments = Converters.GetLong(row, "total_documents");
                int documentFrequency = Converters.GetInt(row, "document_frequency");

                return new TermStatisticsResult
                {
                    Term = Converters.GetRequiredString(row, "term"),
                    DocumentFrequency = documentFrequency,
                    TotalFrequency = Converters.GetInt(row, "total_frequency"),
                    InverseDocumentFrequency = totalDocuments > 0 && documentFrequency > 0
                        ? Math.Log((double)totalDocuments / documentFrequency)
                        : 0.0,
                    AverageFrequencyPerDocument = Converters.GetDouble(row, "avg_frequency_per_document"),
                    MaxFrequencyInDocument = Converters.GetInt(row, "max_frequency_in_document"),
                    MinFrequencyInDocument = Converters.GetInt(row, "min_frequency_in_document")
                };
            }, token).ConfigureAwait(false);
        }
    }
}
