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
    /// <para>
    /// Supports both in-memory and file-based SQLite databases.
    /// Uses WAL mode for improved concurrent access performance.
    /// </para>
    /// <para>
    /// Thread safety: Uses ReaderWriterLockSlim to allow concurrent read operations
    /// while serializing write operations. Read queries (SELECT) use ephemeral connections
    /// and can execute in parallel. Write queries and transactions use a dedicated write
    /// connection with exclusive access.
    /// </para>
    /// </remarks>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        private readonly ReaderWriterLockSlim _Lock = new ReaderWriterLockSlim();
        private SqliteConnection? _WriteConnection;
        private string _ConnectionString = string.Empty;
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

            _Lock.EnterWriteLock();
            try
            {
                if (_IsOpen)
                {
                    return;
                }

                _ConnectionString = BuildConnectionString();
                _WriteConnection = new SqliteConnection(_ConnectionString);
                await _WriteConnection.OpenAsync(token).ConfigureAwait(false);

                await ApplyPragmasAsync(token).ConfigureAwait(false);
                await CreateSchemaAsync(token).ConfigureAwait(false);

                InitializeMethodImplementations();

                _IsOpen = true;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public override async Task CreateIndexTablesAsync(string tablePrefix, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            string createTablesQuery = Queries.SetupQueries.CreateIndexTables(tablePrefix);
            await ExecuteQueryAsync(createTablesQuery, true, token).ConfigureAwait(false);

            // Create indexes with an extended timeout (10 minutes) since building indexes
            // on large existing tables can exceed the standard command timeout.
            List<string> indexQueries = Queries.SetupQueries.CreateIndexTableIndexes(tablePrefix);
            foreach (string indexQuery in indexQueries)
            {
                try
                {
                    _Lock.EnterWriteLock();
                    try
                    {
                        using SqliteCommand cmd = _WriteConnection!.CreateCommand();
                        cmd.CommandText = indexQuery;
                        cmd.CommandTimeout = 600;
                        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _Lock.ExitWriteLock();
                    }
                }
                catch
                {
                    // Non-fatal: index may already exist or creation may time out on large tables.
                    // Failure to create an index does not affect existing data or other indexes.
                }
            }
        }

        /// <inheritdoc />
        public override async Task DropIndexTablesAsync(string tablePrefix, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            string dropTablesQuery = Queries.SetupQueries.DropIndexTables(tablePrefix);
            await ExecuteQueryAsync(dropTablesQuery, true, token).ConfigureAwait(false);
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

            // If the current thread holds the write lock (we're in a transaction), execute directly
            if (_Lock.IsWriteLockHeld)
            {
                return await ExecuteQueryInternalAsync(query, false, token).ConfigureAwait(false);
            }

            // If this is a read query, use read lock with ephemeral connection
            bool isReadQuery = IsReadOnlyQuery(query) && !isTransaction;

            if (isReadQuery)
            {
                return await ExecuteReadQueryAsync(query, token).ConfigureAwait(false);
            }
            else
            {
                return await ExecuteWriteQueryAsync(query, isTransaction, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Loads data from a reader into a DataTable without constraint checking.
        /// </summary>
        /// <param name="reader">The data reader to read from.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A DataTable containing the data.</returns>
        private static async Task<DataTable> LoadDataTableWithoutConstraintsAsync(SqliteDataReader reader, CancellationToken token)
        {
            DataTable result = new DataTable();

            // Create columns from the reader's schema
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string columnName = reader.GetName(i);
                Type columnType = reader.GetFieldType(i);
                DataColumn column = new DataColumn(columnName, columnType);
                column.AllowDBNull = true;
                result.Columns.Add(column);
            }

            // Read all rows synchronously to ensure the reader is fully consumed
            // before returning, preventing connection reuse issues
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                DataRow row = result.NewRow();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                }
                result.Rows.Add(row);
            }

            // Explicitly close the reader to release the connection
            await reader.CloseAsync().ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Executes a read-only query using a read lock and ephemeral connection.
        /// </summary>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A DataTable containing the query results.</returns>
        private async Task<DataTable> ExecuteReadQueryAsync(string query, CancellationToken token)
        {
            _Lock.EnterReadLock();
            try
            {
                using SqliteConnection readConnection = new SqliteConnection(_ConnectionString);
                await readConnection.OpenAsync(token).ConfigureAwait(false);

                DataTable result = new DataTable();
                using SqliteCommand cmd = readConnection.CreateCommand();
                cmd.CommandText = query;
                cmd.CommandTimeout = Settings.CommandTimeout;

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                return await LoadDataTableWithoutConstraintsAsync(reader, token).ConfigureAwait(false);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Executes a write query using a write lock and the dedicated write connection.
        /// </summary>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="isTransaction">Whether to wrap the query in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A DataTable containing the query results.</returns>
        private async Task<DataTable> ExecuteWriteQueryAsync(string query, bool isTransaction, CancellationToken token)
        {
            _Lock.EnterWriteLock();
            try
            {
                return await ExecuteQueryInternalAsync(query, isTransaction, token).ConfigureAwait(false);
            }
            finally
            {
                _Lock.ExitWriteLock();
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

            // If the current thread holds the write lock, skip lock acquisition
            bool lockAcquired = false;
            if (!_Lock.IsWriteLockHeld)
            {
                _Lock.EnterWriteLock();
                lockAcquired = true;
            }

            try
            {
                DataTable result = new DataTable();
                SqliteTransaction? transaction = null;

                if (isTransaction && _ActiveTransaction == null)
                {
                    transaction = _WriteConnection!.BeginTransaction();
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
                if (lockAcquired)
                {
                    _Lock.ExitWriteLock();
                }
            }
        }

        /// <inheritdoc />
        public override async Task CloseAsync(CancellationToken token = default)
        {
            if (!_IsOpen || _WriteConnection == null)
            {
                return;
            }

            _Lock.EnterWriteLock();
            try
            {
                if (!Settings.InMemory)
                {
                    await CheckpointInternalAsync(token).ConfigureAwait(false);
                }

                // Clear the connection pool before closing to prevent cached connections
                // from being reused when a database at the same path is recreated
                SqliteConnection connectionToClose = _WriteConnection;
                await _WriteConnection.CloseAsync().ConfigureAwait(false);
                SqliteConnection.ClearPool(connectionToClose);
                _WriteConnection.Dispose();
                _WriteConnection = null;
                _IsOpen = false;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (!Settings.InMemory)
            {
                _Lock.EnterWriteLock();
                try
                {
                    await CheckpointInternalAsync(token).ConfigureAwait(false);
                }
                finally
                {
                    _Lock.ExitWriteLock();
                }
            }
        }

        /// <inheritdoc />
        public override async Task BeginTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            _Lock.EnterWriteLock();
            try
            {
                if (_ActiveTransaction != null)
                {
                    _Lock.ExitWriteLock();
                    throw new InvalidOperationException("A transaction is already active.");
                }

                _ActiveTransaction = _WriteConnection!.BeginTransaction();
                // Note: Write lock is intentionally held while transaction is active
                // It will be released in CommitTransactionAsync or RollbackTransactionAsync
            }
            catch
            {
                _Lock.ExitWriteLock();
                throw;
            }
            // Do not release the write lock here - it stays held during the transaction
            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task CommitTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            // Write lock should already be held from BeginTransactionAsync
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
                _Lock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public override async Task RollbackTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            // Write lock should already be held from BeginTransactionAsync
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
                _Lock.ExitWriteLock();
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

            _Lock.EnterWriteLock();
            try
            {
                await CheckpointInternalAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Performs a WAL checkpoint without acquiring the lock.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <remarks>Caller must hold the write lock.</remarks>
        private async Task CheckpointInternalAsync(CancellationToken token)
        {
            using SqliteCommand cmd = _WriteConnection!.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
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

            _Lock.EnterWriteLock();
            try
            {
                string targetConnectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = targetPath,
                    Mode = SqliteOpenMode.ReadWriteCreate
                }.ToString();

                using SqliteConnection targetConnection = new SqliteConnection(targetConnectionString);
                await targetConnection.OpenAsync(token).ConfigureAwait(false);

                _WriteConnection!.BackupDatabase(targetConnection);

                await targetConnection.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets the internal SQLite write connection for advanced operations.
        /// </summary>
        /// <returns>The SQLite write connection.</returns>
        /// <exception cref="InvalidOperationException">Thrown when connection is not open.</exception>
        /// <remarks>
        /// Caller must acquire appropriate lock before using this connection.
        /// For read operations, prefer using ExecuteQueryAsync which handles locking automatically.
        /// </remarks>
        internal SqliteConnection GetWriteConnection()
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
            return _WriteConnection!;
        }

        /// <summary>
        /// Gets the reader-writer lock for thread-safe operations.
        /// </summary>
        /// <returns>The reader-writer lock.</returns>
        /// <remarks>
        /// Use EnterReadLock for read operations and EnterWriteLock for write operations.
        /// </remarks>
        internal ReaderWriterLockSlim GetLock()
        {
            return _Lock;
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
        /// <remarks>Caller must hold the write lock.</remarks>
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

                using SqliteCommand cmd = _WriteConnection!.CreateCommand();
                cmd.CommandText = trimmedPragma;
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates the database schema if it doesn't exist.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <remarks>Caller must hold the write lock.</remarks>
        private async Task CreateSchemaAsync(CancellationToken token)
        {
            using SqliteCommand cmd = _WriteConnection!.CreateCommand();
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
        /// Determines if a query is read-only (SELECT statement).
        /// </summary>
        /// <param name="query">The SQL query to check.</param>
        /// <returns>True if the query is a SELECT statement; otherwise false.</returns>
        private static bool IsReadOnlyQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            string trimmed = query.TrimStart();
            return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Executes a query using the write connection without acquiring the lock (internal use only).
        /// </summary>
        /// <remarks>Caller must hold the write lock.</remarks>
        private async Task<DataTable> ExecuteQueryInternalAsync(string query, bool isTransaction, CancellationToken token)
        {
            DataTable result = new DataTable();

            // Use the active transaction if one exists, otherwise create a new one if requested
            bool useActiveTransaction = _ActiveTransaction != null;
            SqliteTransaction? transaction = useActiveTransaction ? _ActiveTransaction : (isTransaction ? _WriteConnection!.BeginTransaction() : null);

            try
            {
                using SqliteCommand cmd = _WriteConnection!.CreateCommand();
                cmd.CommandText = query;
                cmd.CommandTimeout = Settings.CommandTimeout;

                if (transaction != null)
                {
                    cmd.Transaction = transaction;
                }

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                result = await LoadDataTableWithoutConstraintsAsync(reader, token).ConfigureAwait(false);

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
                // Close the connection first (uses write lock), then dispose resources
                if (_IsOpen && _WriteConnection != null)
                {
                    try
                    {
                        _Lock.EnterWriteLock();
                        try
                        {
                            // Clear the connection pool before closing to prevent cached connections
                            // from being reused when a database at the same path is recreated
                            SqliteConnection connectionToClose = _WriteConnection;
                            _WriteConnection.Close();
                            SqliteConnection.ClearPool(connectionToClose);
                            _WriteConnection.Dispose();
                            _WriteConnection = null;
                            _IsOpen = false;
                        }
                        finally
                        {
                            _Lock.ExitWriteLock();
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
                    if (_WriteConnection != null)
                    {
                        SqliteConnection.ClearPool(_WriteConnection);
                    }
                    _WriteConnection?.Dispose();
                    _WriteConnection = null;
                }

                _Lock.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            // Close the connection first (uses write lock), then dispose resources
            if (_IsOpen && _WriteConnection != null)
            {
                try
                {
                    _Lock.EnterWriteLock();
                    try
                    {
                        // Clear the connection pool before closing to prevent cached connections
                        // from being reused when a database at the same path is recreated
                        SqliteConnection connectionToClose = _WriteConnection;
                        await _WriteConnection.CloseAsync().ConfigureAwait(false);
                        SqliteConnection.ClearPool(connectionToClose);
                        _WriteConnection.Dispose();
                        _WriteConnection = null;
                        _IsOpen = false;
                    }
                    finally
                    {
                        _Lock.ExitWriteLock();
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
                if (_WriteConnection != null)
                {
                    SqliteConnection.ClearPool(_WriteConnection);
                }
                _WriteConnection?.Dispose();
                _WriteConnection = null;
            }

            _Lock.Dispose();
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }
    }
}
