namespace Verbex.Repositories
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Verbex.Repositories.Interfaces;
    using Verbex.Repositories.Queries;
    using Verbex.Repositories.Sqlite;
    using Verbex.Utilities;

    /// <summary>
    /// SQLite-based implementation of the index repository.
    /// Provides the base implementation for both in-memory and on-disk modes.
    /// </summary>
    public class SqliteIndexRepository : IIndexRepository
    {
        private readonly ReaderWriterLockSlim _Lock = new ReaderWriterLockSlim();
        private readonly bool _InMemory;
        private readonly string? _DatabasePath;
        private SqliteConnection? _Connection;
        private string _ConnectionString = string.Empty;
        private string _IndexName = string.Empty;
        private bool _IsOpen;
        private bool _Disposed;

        private SqliteDocumentMethods? _DocumentMethods;
        private SqliteTermMethods? _TermMethods;
        private SqliteDocumentTermMethods? _DocumentTermMethods;
        private SqliteLabelMethods? _LabelMethods;
        private SqliteTagMethods? _TagMethods;
        private SqliteStatisticsMethods? _StatisticsMethods;

        /// <summary>
        /// Gets whether the repository is currently open.
        /// </summary>
        public bool IsOpen => _IsOpen;

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public string IndexName => _IndexName;

        /// <summary>
        /// Gets the document operations interface.
        /// </summary>
        public IDocumentMethods Document => _DocumentMethods ?? throw new InvalidOperationException("Repository not initialized.");

        /// <summary>
        /// Gets the term operations interface.
        /// </summary>
        public ITermMethods Term => _TermMethods ?? throw new InvalidOperationException("Repository not initialized.");

        /// <summary>
        /// Gets the document-term operations interface.
        /// </summary>
        public IDocumentTermMethods DocumentTerm => _DocumentTermMethods ?? throw new InvalidOperationException("Repository not initialized.");

        /// <summary>
        /// Gets the label operations interface.
        /// </summary>
        public ILabelMethods Label => _LabelMethods ?? throw new InvalidOperationException("Repository not initialized.");

        /// <summary>
        /// Gets the tag operations interface.
        /// </summary>
        public ITagMethods Tag => _TagMethods ?? throw new InvalidOperationException("Repository not initialized.");

        /// <summary>
        /// Gets the statistics operations interface.
        /// </summary>
        public IStatisticsMethods Statistics => _StatisticsMethods ?? throw new InvalidOperationException("Repository not initialized.");

        /// <summary>
        /// Creates a new SQLite index repository.
        /// </summary>
        /// <param name="databasePath">Path to the database file. If null, uses in-memory database.</param>
        public SqliteIndexRepository(string? databasePath = null)
        {
            _InMemory = string.IsNullOrEmpty(databasePath);
            _DatabasePath = databasePath;

            // Initialize method implementations
            _DocumentMethods = new SqliteDocumentMethods(this);
            _TermMethods = new SqliteTermMethods(this);
            _DocumentTermMethods = new SqliteDocumentTermMethods(this);
            _LabelMethods = new SqliteLabelMethods(this);
            _TagMethods = new SqliteTagMethods(this);
            _StatisticsMethods = new SqliteStatisticsMethods(this);
        }

        /// <summary>
        /// Opens the repository connection and initializes the schema if needed.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task OpenAsync(string indexName, CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (_IsOpen)
            {
                return;
            }

            _IndexName = indexName;

            if (_InMemory)
            {
                _ConnectionString = "Data Source=:memory:;Mode=Memory;Cache=Shared";
            }
            else
            {
                _ConnectionString = $"Data Source={_DatabasePath}";
            }

            _Connection = new SqliteConnection(_ConnectionString);
            await _Connection.OpenAsync(token).ConfigureAwait(false);

            await ApplyPragmaSettingsAsync(token).ConfigureAwait(false);
            await InitializeSchemaAsync(token).ConfigureAwait(false);

            _IsOpen = true;
        }

        /// <summary>
        /// Closes the repository connection.
        /// For on-disk databases, performs a WAL checkpoint before closing to ensure all data is persisted.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task CloseAsync(CancellationToken token = default)
        {
            if (!_IsOpen || _Connection == null)
            {
                return;
            }

            if (!_InMemory)
            {
                await CheckpointAsync(token).ConfigureAwait(false);
            }

            await _Connection.CloseAsync().ConfigureAwait(false);
            _Connection.Dispose();
            _Connection = null;
            _IsOpen = false;
        }

        /// <summary>
        /// Flushes any pending changes to persistent storage.
        /// For in-memory databases, this saves to the specified file.
        /// For on-disk databases, this performs a WAL checkpoint to move data from the WAL file to the main database.
        /// </summary>
        /// <param name="targetPath">Path to save to (required for in-memory mode, ignored for on-disk mode).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task FlushAsync(string? targetPath = null, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (_InMemory)
            {
                if (string.IsNullOrEmpty(targetPath))
                {
                    throw new ArgumentException("Target path is required for in-memory database flush.", nameof(targetPath));
                }

                _Lock.EnterWriteLock();
                try
                {
                    using SqliteConnection targetConnection = new SqliteConnection($"Data Source={targetPath}");
                    await targetConnection.OpenAsync(token).ConfigureAwait(false);

                    _Connection!.BackupDatabase(targetConnection);

                    await targetConnection.CloseAsync().ConfigureAwait(false);
                }
                finally
                {
                    _Lock.ExitWriteLock();
                }
            }
            else
            {
                await CheckpointAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Performs a WAL checkpoint to move data from the WAL file to the main database file.
        /// This is only applicable for on-disk databases using WAL mode.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task CheckpointAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (_InMemory)
            {
                return;
            }

            _Lock.EnterWriteLock();
            try
            {
                using SqliteCommand cmd = _Connection!.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        #region Internal Helper Methods for Method Implementations

        /// <summary>
        /// Throws if disposed. Internal method for use by method implementations.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if repository is disposed.</exception>
        internal void ThrowIfDisposed()
        {
            if (_Disposed)
            {
                throw new ObjectDisposedException(nameof(SqliteIndexRepository));
            }
        }

        /// <summary>
        /// Throws if not open. Internal method for use by method implementations.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if repository is not open.</exception>
        internal void ThrowIfNotOpen()
        {
            if (!_IsOpen || _Connection == null)
            {
                throw new InvalidOperationException("Repository is not open. Call OpenAsync first.");
            }
        }

        /// <summary>
        /// Executes a read operation with proper locking.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="operation">The read operation to execute.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result of the operation.</returns>
        internal async Task<T> ExecuteReadAsync<T>(Func<SqliteConnection, Task<T>> operation, CancellationToken token = default)
        {
            _Lock.EnterReadLock();
            try
            {
                return await operation(_Connection!).ConfigureAwait(false);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Executes a write operation with proper locking.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="operation">The write operation to execute.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result of the operation.</returns>
        internal async Task<T> ExecuteWriteAsync<T>(Func<SqliteConnection, Task<T>> operation, CancellationToken token = default)
        {
            _Lock.EnterWriteLock();
            try
            {
                return await operation(_Connection!).ConfigureAwait(false);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Executes a write operation with proper locking.
        /// </summary>
        /// <param name="operation">The write operation to execute.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task ExecuteWriteAsync(Func<SqliteConnection, Task> operation, CancellationToken token = default)
        {
            _Lock.EnterWriteLock();
            try
            {
                await operation(_Connection!).ConfigureAwait(false);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the last modified timestamp in the index metadata.
        /// Must be called within a write lock.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task UpdateLastModifiedAsync(CancellationToken token = default)
        {
            string now = Converters.FormatTimestamp(DateTime.UtcNow);
            using SqliteCommand cmd = _Connection!.CreateCommand();
            cmd.CommandText = StatisticsQueries.UpdateLastModified();
            cmd.Parameters.AddWithValue("@lastModifiedUtc", now);
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the repository.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the repository asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources.
        /// For on-disk databases, performs a WAL checkpoint before closing to ensure all data is persisted.
        /// </summary>
        /// <param name="disposing">True if called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                if (!_InMemory && _Connection != null && _IsOpen)
                {
                    try
                    {
                        using SqliteCommand cmd = _Connection.CreateCommand();
                        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        cmd.ExecuteNonQuery();
                    }
                    catch
                    {
                        // Best effort checkpoint on dispose
                    }
                }

                _Connection?.Close();
                _Connection?.Dispose();
                _Lock.Dispose();
            }

            _Disposed = true;
        }

        /// <summary>
        /// Disposes managed resources asynchronously.
        /// For on-disk databases, performs a WAL checkpoint before closing to ensure all data is persisted.
        /// </summary>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_Connection != null)
            {
                if (!_InMemory && _IsOpen)
                {
                    try
                    {
                        using SqliteCommand cmd = _Connection.CreateCommand();
                        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best effort checkpoint on dispose
                    }
                }

                await _Connection.CloseAsync().ConfigureAwait(false);
                await _Connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        #endregion

        #region Private Methods

        private async Task ApplyPragmaSettingsAsync(CancellationToken token)
        {
            using SqliteCommand cmd = _Connection!.CreateCommand();
            cmd.CommandText = SchemaQueries.ApplyPragmaSettings();
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        private async Task InitializeSchemaAsync(CancellationToken token)
        {
            using SqliteCommand checkCmd = _Connection!.CreateCommand();
            checkCmd.CommandText = SchemaQueries.CheckSchemaExists();
            object? result = await checkCmd.ExecuteScalarAsync(token).ConfigureAwait(false);
            bool schemaExists = Convert.ToInt32(result) > 0;

            if (!schemaExists)
            {
                using SqliteCommand createCmd = _Connection!.CreateCommand();
                createCmd.CommandText = SchemaQueries.CreateSchema();
                await createCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                string now = Converters.FormatTimestamp(DateTime.UtcNow);
                using SqliteCommand metaCmd = _Connection!.CreateCommand();
                metaCmd.CommandText = StatisticsQueries.UpsertIndexMetadata();
                metaCmd.Parameters.AddWithValue("@id", IdGenerator.GenerateIndexMetadataId());
                metaCmd.Parameters.AddWithValue("@name", _IndexName);
                metaCmd.Parameters.AddWithValue("@lastModifiedUtc", now);
                metaCmd.Parameters.AddWithValue("@createdUtc", now);
                await metaCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        #endregion
    }
}
