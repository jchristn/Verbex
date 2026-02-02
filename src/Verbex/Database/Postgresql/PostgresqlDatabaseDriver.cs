namespace Verbex.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Verbex.Database.Interfaces;
    using Verbex.Database.Postgresql.Implementations;
    using Verbex.Database.Postgresql.Queries;

    /// <summary>
    /// PostgreSQL implementation of the database driver.
    /// </summary>
    /// <remarks>
    /// Provides connection pooling and transactional support for PostgreSQL databases.
    /// </remarks>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private NpgsqlDataSource? _DataSource;
        private NpgsqlConnection? _ActiveConnection;
        private NpgsqlTransaction? _ActiveTransaction;
        private bool _IsOpen = false;

        /// <inheritdoc />
        public override bool IsOpen => _IsOpen;

        /// <inheritdoc />
        public override bool IsTransactionActive => _ActiveTransaction != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgresqlDatabaseDriver"/> class.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when settings.Type is not Postgresql.</exception>
        public PostgresqlDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            if (settings.Type != DatabaseTypeEnum.Postgresql)
            {
                throw new ArgumentException("Database type must be Postgresql for PostgresqlDatabaseDriver.", nameof(settings));
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
                NpgsqlDataSourceBuilder builder = new NpgsqlDataSourceBuilder(connectionString);
                _DataSource = builder.Build();

                await CreateSchemaAsync(token).ConfigureAwait(false);
                await CreateIndexesAsync(token).ConfigureAwait(false);

                InitializeMethodImplementations();

                _IsOpen = true;
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async Task CreateIndexTablesAsync(string tablePrefix, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            string createTablesQuery = Queries.SetupQueries.CreateIndexTables(tablePrefix);
            await ExecuteQueryAsync(createTablesQuery, true, token).ConfigureAwait(false);

            List<string> indexQueries = Queries.SetupQueries.CreateIndexTableIndexes(tablePrefix);
            foreach (string indexQuery in indexQueries)
            {
                await ExecuteQueryAsync(indexQuery, true, token).ConfigureAwait(false);
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

                await using NpgsqlConnection connection = await _DataSource!.OpenConnectionAsync(token).ConfigureAwait(false);
                NpgsqlTransaction? transaction = null;

                if (isTransaction)
                {
                    transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
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

                        await using NpgsqlCommand cmd = new NpgsqlCommand(query, connection, transaction);
                        cmd.CommandTimeout = Settings.CommandTimeout;

                        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                        result = await LoadDataTableWithoutConstraintsAsync(reader, token).ConfigureAwait(false);
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
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync().ConfigureAwait(false);
                    }
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
            if (!_IsOpen || _DataSource == null)
            {
                return;
            }

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_ActiveTransaction != null)
                {
                    await _ActiveTransaction.DisposeAsync().ConfigureAwait(false);
                    _ActiveTransaction = null;
                }

                if (_ActiveConnection != null)
                {
                    await _ActiveConnection.DisposeAsync().ConfigureAwait(false);
                    _ActiveConnection = null;
                }

                await _DataSource.DisposeAsync().ConfigureAwait(false);
                _DataSource = null;
                _IsOpen = false;
            }
            finally
            {
                _Semaphore.Release();
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

                _ActiveConnection = await _DataSource!.OpenConnectionAsync(token).ConfigureAwait(false);
                _ActiveTransaction = await _ActiveConnection.BeginTransactionAsync(token).ConfigureAwait(false);
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
                await _ActiveTransaction.DisposeAsync().ConfigureAwait(false);
                _ActiveTransaction = null;

                await _ActiveConnection!.DisposeAsync().ConfigureAwait(false);
                _ActiveConnection = null;
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
                await _ActiveTransaction.DisposeAsync().ConfigureAwait(false);
                _ActiveTransaction = null;

                await _ActiveConnection!.DisposeAsync().ConfigureAwait(false);
                _ActiveConnection = null;
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <summary>
        /// Builds the PostgreSQL connection string based on settings.
        /// </summary>
        /// <returns>The connection string.</returns>
        private string BuildConnectionString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Host={Settings.Hostname};");
            sb.Append($"Port={Settings.Port};");
            sb.Append($"Database={Settings.DatabaseName};");
            sb.Append($"Username={Settings.Username};");
            sb.Append($"Password={Settings.Password};");

            if (Settings.RequireEncryption)
            {
                sb.Append("SSL Mode=Require;Trust Server Certificate=true;");
            }

            if (Settings.MinPoolSize > 0)
            {
                sb.Append($"Minimum Pool Size={Settings.MinPoolSize};");
            }

            if (Settings.MaxPoolSize > 0)
            {
                sb.Append($"Maximum Pool Size={Settings.MaxPoolSize};");
            }

            sb.Append($"Timeout={Settings.ConnectionTimeout};");
            sb.Append($"Command Timeout={Settings.CommandTimeout};");

            return sb.ToString();
        }

        /// <summary>
        /// Creates the database schema if it doesn't exist.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        private async Task CreateSchemaAsync(CancellationToken token)
        {
            await using NpgsqlConnection connection = await _DataSource!.OpenConnectionAsync(token).ConfigureAwait(false);
            await using NpgsqlCommand cmd = new NpgsqlCommand(SetupQueries.CreateTables, connection);
            cmd.CommandTimeout = Settings.CommandTimeout;
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates indexes for the database tables.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <remarks>
        /// PostgreSQL supports CREATE INDEX IF NOT EXISTS syntax,
        /// so indexes are created idempotently without error handling.
        /// </remarks>
        private async Task CreateIndexesAsync(CancellationToken token)
        {
            await using NpgsqlConnection connection = await _DataSource!.OpenConnectionAsync(token).ConfigureAwait(false);

            foreach (string indexQuery in SetupQueries.CreateIndexes)
            {
                token.ThrowIfCancellationRequested();

                await using NpgsqlCommand cmd = new NpgsqlCommand(indexQuery, connection);
                cmd.CommandTimeout = Settings.CommandTimeout;
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
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
        /// Loads data from a reader into a DataTable without constraint checking.
        /// </summary>
        /// <param name="reader">The data reader to read from.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A DataTable containing the data.</returns>
        private static async Task<DataTable> LoadDataTableWithoutConstraintsAsync(NpgsqlDataReader reader, CancellationToken token)
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
        /// Executes a query without acquiring the semaphore (internal use only).
        /// </summary>
        private async Task<DataTable> ExecuteQueryInternalAsync(string query, bool isTransaction, CancellationToken token)
        {
            DataTable result = new DataTable();

            // Use active transaction connection if one exists
            bool useActiveTransaction = _ActiveTransaction != null && _ActiveConnection != null;

            if (useActiveTransaction)
            {
                await using NpgsqlCommand cmd = new NpgsqlCommand(query, _ActiveConnection, _ActiveTransaction);
                cmd.CommandTimeout = Settings.CommandTimeout;

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                result = await LoadDataTableWithoutConstraintsAsync(reader, token).ConfigureAwait(false);
            }
            else
            {
                await using NpgsqlConnection connection = await _DataSource!.OpenConnectionAsync(token).ConfigureAwait(false);
                NpgsqlTransaction? transaction = null;

                if (isTransaction)
                {
                    transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                }

                try
                {
                    await using NpgsqlCommand cmd = new NpgsqlCommand(query, connection, transaction);
                    cmd.CommandTimeout = Settings.CommandTimeout;

                    await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                    result = await LoadDataTableWithoutConstraintsAsync(reader, token).ConfigureAwait(false);

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
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Close the data source first (uses semaphore), then dispose resources
                if (_IsOpen && _DataSource != null)
                {
                    try
                    {
                        _Semaphore.Wait();
                        try
                        {
                            _DataSource.Dispose();
                            _DataSource = null;
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
                    _DataSource?.Dispose();
                    _DataSource = null;
                }

                _Semaphore.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            // Close the data source first (uses semaphore), then dispose resources
            if (_IsOpen && _DataSource != null)
            {
                try
                {
                    await _Semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        await _DataSource.DisposeAsync().ConfigureAwait(false);
                        _DataSource = null;
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
                if (_DataSource != null)
                {
                    await _DataSource.DisposeAsync().ConfigureAwait(false);
                    _DataSource = null;
                }
            }

            _Semaphore.Dispose();
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }
    }
}
