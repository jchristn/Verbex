namespace Verbex.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Verbex.Database.Interfaces;
    using Verbex.Database.Sqlite.Implementations;
    using Verbex.Database.Sqlite.Queries;

    /// <summary>
    /// SQLite implementation of the database driver.
    /// </summary>
    /// <remarks>
    /// Supports both in-memory and file-based SQLite databases.
    /// Uses WAL mode for improved concurrent access performance.
    /// </remarks>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private SqliteConnection? _Connection;
        private SqliteTransaction? _ActiveTransaction;
        private bool _IsOpen = false;

        /// <inheritdoc />
        public override bool IsOpen => _IsOpen;

        /// <inheritdoc />
        public override bool IsTransactionActive => _ActiveTransaction != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteDatabaseDriver"/> class.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when settings.Type is not Sqlite.</exception>
        public SqliteDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            if (settings.Type != DatabaseTypeEnum.Sqlite)
            {
                throw new ArgumentException("Database type must be Sqlite for SqliteDatabaseDriver.", nameof(settings));
            }
        }

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_IsOpen)
                {
                    return;
                }

                string connectionString = BuildConnectionString();
                _Connection = new SqliteConnection(connectionString);
                await _Connection.OpenAsync(token).ConfigureAwait(false);

                await ApplyPragmasAsync(token).ConfigureAwait(false);
                await CreateSchemaAsync(token).ConfigureAwait(false);

                InitializeMethodImplementations();

                _IsOpen = true;
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await ExecuteQueryInternalAsync(query, isTransaction, token).ConfigureAwait(false);
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (queries == null)
            {
                throw new ArgumentNullException(nameof(queries));
            }

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                DataTable result = new DataTable();
                SqliteTransaction? transaction = null;

                if (isTransaction)
                {
                    transaction = _Connection!.BeginTransaction();
                }

                try
                {
                    foreach (string query in queries)
                    {
                        if (string.IsNullOrWhiteSpace(query))
                        {
                            continue;
                        }

                        token.ThrowIfCancellationRequested();
                        result = await ExecuteQueryInternalAsync(query, false, token).ConfigureAwait(false);
                    }

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                    }
                }
                catch
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    }
                    throw;
                }
                finally
                {
                    transaction?.Dispose();
                }

                return result;
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async Task CloseAsync(CancellationToken token = default)
        {
            if (!_IsOpen || _Connection == null)
            {
                return;
            }

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!Settings.InMemory)
                {
                    await CheckpointAsync(token).ConfigureAwait(false);
                }

                // Clear the connection pool before closing to prevent cached connections
                // from being reused when a database at the same path is recreated
                SqliteConnection connectionToClose = _Connection;
                await _Connection.CloseAsync().ConfigureAwait(false);
                SqliteConnection.ClearPool(connectionToClose);
                _Connection.Dispose();
                _Connection = null;
                _IsOpen = false;
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (!Settings.InMemory)
            {
                await CheckpointAsync(token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task BeginTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_ActiveTransaction != null)
                {
                    throw new InvalidOperationException("A transaction is already active.");
                }

                _ActiveTransaction = _Connection!.BeginTransaction();
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async Task CommitTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_ActiveTransaction == null)
                {
                    throw new InvalidOperationException("No transaction is active.");
                }

                await _ActiveTransaction.CommitAsync(token).ConfigureAwait(false);
                _ActiveTransaction.Dispose();
                _ActiveTransaction = null;
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async Task RollbackTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_ActiveTransaction == null)
                {
                    throw new InvalidOperationException("No transaction is active.");
                }

                await _ActiveTransaction.RollbackAsync(token).ConfigureAwait(false);
                _ActiveTransaction.Dispose();
                _ActiveTransaction = null;
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <summary>
        /// Performs a WAL checkpoint to move data from WAL to main database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task CheckpointAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using SqliteCommand cmd = _Connection!.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <summary>
        /// Saves an in-memory database to a file.
        /// </summary>
        /// <param name="targetPath">The target file path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not in in-memory mode.</exception>
        public async Task SaveToFileAsync(string targetPath, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (!Settings.InMemory)
            {
                throw new InvalidOperationException("SaveToFile is only available for in-memory databases.");
            }

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                string targetConnectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = targetPath,
                    Mode = SqliteOpenMode.ReadWriteCreate
                }.ToString();

                using SqliteConnection targetConnection = new SqliteConnection(targetConnectionString);
                await targetConnection.OpenAsync(token).ConfigureAwait(false);

                _Connection!.BackupDatabase(targetConnection);

                await targetConnection.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <summary>
        /// Gets the internal SQLite connection for advanced operations.
        /// </summary>
        /// <returns>The SQLite connection.</returns>
        /// <exception cref="InvalidOperationException">Thrown when connection is not open.</exception>
        internal SqliteConnection GetConnection()
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            return _Connection!;
        }

        /// <summary>
        /// Gets the semaphore for thread-safe operations.
        /// </summary>
        /// <returns>The semaphore.</returns>
        internal SemaphoreSlim GetSemaphore()
        {
            return _Semaphore;
        }

        /// <summary>
        /// Builds the SQLite connection string based on settings.
        /// </summary>
        /// <returns>The connection string.</returns>
        private string BuildConnectionString()
        {
            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder();

            if (Settings.InMemory)
            {
                builder.DataSource = ":memory:";
                builder.Mode = SqliteOpenMode.Memory;
            }
            else
            {
                builder.DataSource = Settings.Filename;
                builder.Mode = SqliteOpenMode.ReadWriteCreate;
            }

            builder.Cache = SqliteCacheMode.Shared;

            return builder.ToString();
        }

        /// <summary>
        /// Applies SQLite pragmas for optimal performance.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        private async Task ApplyPragmasAsync(CancellationToken token)
        {
            string[] pragmas = SetupQueries.GetPragmas().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string pragma in pragmas)
            {
                string trimmedPragma = pragma.Trim();
                if (string.IsNullOrEmpty(trimmedPragma))
                {
                    continue;
                }

                // Replace busy_timeout with configurable value based on CommandTimeout
                if (trimmedPragma.StartsWith("PRAGMA busy_timeout", StringComparison.OrdinalIgnoreCase))
                {
                    int busyTimeoutMs = Settings.CommandTimeout * 1000;
                    trimmedPragma = $"PRAGMA busy_timeout = {busyTimeoutMs}";
                }

                using SqliteCommand cmd = _Connection!.CreateCommand();
                cmd.CommandText = trimmedPragma;
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates the database schema if it doesn't exist.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        private async Task CreateSchemaAsync(CancellationToken token)
        {
            using SqliteCommand cmd = _Connection!.CreateCommand();
            cmd.CommandText = SetupQueries.CreateTables();
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            cmd.CommandText = SetupQueries.CreateIndices();
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Initializes all method interface implementations.
        /// </summary>
        private void InitializeMethodImplementations()
        {
            Tenants = new TenantMethods(this);
            Administrators = new AdministratorMethods(this);
            Users = new UserMethods(this);
            Credentials = new CredentialMethods(this);
            Indexes = new IndexMethods(this);
            Documents = new DocumentMethods(this);
            Terms = new TermMethods(this);
            DocumentTerms = new DocumentTermMethods(this);
            Labels = new LabelMethods(this);
            Tags = new TagMethods(this);
            Statistics = new StatisticsMethods(this);
        }

        /// <summary>
        /// Executes a query without acquiring the semaphore (internal use only).
        /// </summary>
        private async Task<DataTable> ExecuteQueryInternalAsync(string query, bool isTransaction, CancellationToken token)
        {
            DataTable result = new DataTable();

            // Use the active transaction if one exists, otherwise create a new one if requested
            bool useActiveTransaction = _ActiveTransaction != null;
            SqliteTransaction? transaction = useActiveTransaction ? _ActiveTransaction : (isTransaction ? _Connection!.BeginTransaction() : null);

            try
            {
                using SqliteCommand cmd = _Connection!.CreateCommand();
                cmd.CommandText = query;
                cmd.CommandTimeout = Settings.CommandTimeout;

                if (transaction != null)
                {
                    cmd.Transaction = transaction;
                }

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                result.Load(reader);

                // Only commit if we created a new transaction (not using the active one)
                if (transaction != null && !useActiveTransaction)
                {
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }
            catch
            {
                // Only rollback if we created a new transaction (not using the active one)
                if (transaction != null && !useActiveTransaction)
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                }
                throw;
            }
            finally
            {
                // Only dispose if we created a new transaction
                if (!useActiveTransaction)
                {
                    transaction?.Dispose();
                }
            }

            return result;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Close the connection first (uses semaphore), then dispose resources
                if (_IsOpen && _Connection != null)
                {
                    try
                    {
                        _Semaphore.Wait();
                        try
                        {
                            // Clear the connection pool before closing to prevent cached connections
                            // from being reused when a database at the same path is recreated
                            SqliteConnection connectionToClose = _Connection;
                            _Connection.Close();
                            SqliteConnection.ClearPool(connectionToClose);
                            _Connection.Dispose();
                            _Connection = null;
                            _IsOpen = false;
                        }
                        finally
                        {
                            _Semaphore.Release();
                        }
                    }
                    catch
                    {
                        // Ignore errors during disposal
                    }
                }
                else
                {
                    // Still clear pool if connection exists but isn't open
                    if (_Connection != null)
                    {
                        SqliteConnection.ClearPool(_Connection);
                    }
                    _Connection?.Dispose();
                    _Connection = null;
                }

                _Semaphore.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            // Close the connection first (uses semaphore), then dispose resources
            if (_IsOpen && _Connection != null)
            {
                try
                {
                    await _Semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        // Clear the connection pool before closing to prevent cached connections
                        // from being reused when a database at the same path is recreated
                        SqliteConnection connectionToClose = _Connection;
                        await _Connection.CloseAsync().ConfigureAwait(false);
                        SqliteConnection.ClearPool(connectionToClose);
                        _Connection.Dispose();
                        _Connection = null;
                        _IsOpen = false;
                    }
                    finally
                    {
                        _Semaphore.Release();
                    }
                }
                catch
                {
                    // Ignore errors during disposal
                }
            }
            else
            {
                // Still clear pool if connection exists but isn't open
                if (_Connection != null)
                {
                    SqliteConnection.ClearPool(_Connection);
                }
                _Connection?.Dispose();
                _Connection = null;
            }

            _Semaphore.Dispose();
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }
    }
}
