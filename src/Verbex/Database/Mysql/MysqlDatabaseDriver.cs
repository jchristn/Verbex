namespace Verbex.Database.Mysql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MySqlConnector;
    using Verbex.Database.Interfaces;
    using Verbex.Database.Mysql.Implementations;
    using Verbex.Database.Mysql.Queries;

    /// <summary>
    /// MySQL implementation of the database driver.
    /// </summary>
    /// <remarks>
    /// Provides connection pooling and transactional support for MySQL databases.
    /// </remarks>
    public class MysqlDatabaseDriver : DatabaseDriverBase
    {
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private string? _ConnectionString;
        private MySqlConnection? _ActiveConnection;
        private MySqlTransaction? _ActiveTransaction;
        private bool _IsOpen = false;

        /// <inheritdoc />
        public override bool IsOpen => _IsOpen;

        /// <inheritdoc />
        public override bool IsTransactionActive => _ActiveTransaction != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="MysqlDatabaseDriver"/> class.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when settings.Type is not Mysql.</exception>
        public MysqlDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            if (settings.Type != DatabaseTypeEnum.Mysql)
            {
                throw new ArgumentException("Database type must be Mysql for MysqlDatabaseDriver.", nameof(settings));
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

                _ConnectionString = BuildConnectionString();

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

                await using MySqlConnection connection = new MySqlConnection(_ConnectionString);
                await connection.OpenAsync(token).ConfigureAwait(false);
                MySqlTransaction? transaction = null;

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

                        await using MySqlCommand cmd = new MySqlCommand(query, connection, transaction);
                        cmd.CommandTimeout = Settings.CommandTimeout;

                        await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                        result = new DataTable();
                        result.Load(reader);
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

            _IsOpen = false;
            _ConnectionString = null;
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

                _ActiveConnection = new MySqlConnection(_ConnectionString);
                await _ActiveConnection.OpenAsync(token).ConfigureAwait(false);
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
        /// Builds the MySQL connection string based on settings.
        /// </summary>
        /// <returns>The connection string.</returns>
        private string BuildConnectionString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Server={Settings.Hostname};");
            sb.Append($"Port={Settings.Port};");
            sb.Append($"Database={Settings.DatabaseName};");
            sb.Append($"User Id={Settings.Username};");
            sb.Append($"Password={Settings.Password};");

            if (Settings.RequireEncryption)
            {
                sb.Append("SslMode=Required;");
            }

            if (Settings.MinPoolSize > 0)
            {
                sb.Append($"MinimumPoolSize={Settings.MinPoolSize};");
            }

            if (Settings.MaxPoolSize > 0)
            {
                sb.Append($"MaximumPoolSize={Settings.MaxPoolSize};");
            }

            sb.Append($"ConnectionTimeout={Settings.ConnectionTimeout};");
            sb.Append($"DefaultCommandTimeout={Settings.CommandTimeout};");
            sb.Append("AllowUserVariables=True;");

            return sb.ToString();
        }

        /// <summary>
        /// Creates the database schema if it doesn't exist.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        private async Task CreateSchemaAsync(CancellationToken token)
        {
            await using MySqlConnection connection = new MySqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            await using MySqlCommand cmd = new MySqlCommand(SetupQueries.CreateTables, connection);
            cmd.CommandTimeout = Settings.CommandTimeout;
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates indexes for the database tables. Each index is created individually
        /// with error handling for duplicate index errors (MySQL error 1061).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <remarks>
        /// MySQL does not support CREATE INDEX IF NOT EXISTS syntax.
        /// Duplicate key errors are caught and swallowed, allowing the method to continue.
        /// </remarks>
        private async Task CreateIndexesAsync(CancellationToken token)
        {
            await using MySqlConnection connection = new MySqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            foreach (string indexQuery in SetupQueries.CreateIndexes)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    await using MySqlCommand cmd = new MySqlCommand(indexQuery, connection);
                    cmd.CommandTimeout = Settings.CommandTimeout;
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
                catch (MySqlException ex) when (ex.Number == 1061)
                {
                    // Error 1061: Duplicate key name - index already exists, continue silently
                }
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
        /// Executes a query without acquiring the semaphore (internal use only).
        /// </summary>
        private async Task<DataTable> ExecuteQueryInternalAsync(string query, bool isTransaction, CancellationToken token)
        {
            DataTable result = new DataTable();

            // Use active transaction connection if one exists
            bool useActiveTransaction = _ActiveTransaction != null && _ActiveConnection != null;

            if (useActiveTransaction)
            {
                await using MySqlCommand cmd = new MySqlCommand(query, _ActiveConnection, _ActiveTransaction);
                cmd.CommandTimeout = Settings.CommandTimeout;

                await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                result.Load(reader);
            }
            else
            {
                await using MySqlConnection connection = new MySqlConnection(_ConnectionString);
                await connection.OpenAsync(token).ConfigureAwait(false);
                MySqlTransaction? transaction = null;

                if (isTransaction)
                {
                    transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                }

                try
                {
                    await using MySqlCommand cmd = new MySqlCommand(query, connection, transaction);
                    cmd.CommandTimeout = Settings.CommandTimeout;

                    await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                    result.Load(reader);

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
                _Semaphore.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            await CloseAsync(CancellationToken.None).ConfigureAwait(false);
            _Semaphore.Dispose();
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }
    }
}
