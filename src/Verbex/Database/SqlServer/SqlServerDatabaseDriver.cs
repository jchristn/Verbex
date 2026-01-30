namespace Verbex.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Verbex.Database.Interfaces;
    using Verbex.Database.SqlServer.Implementations;
    using Verbex.Database.SqlServer.Queries;

    /// <summary>
    /// SQL Server implementation of the database driver.
    /// </summary>
    /// <remarks>
    /// Provides connection pooling and transactional support for SQL Server databases.
    /// </remarks>
    public class SqlServerDatabaseDriver : DatabaseDriverBase
    {
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private string? _ConnectionString;
        private SqlConnection? _ActiveConnection;
        private SqlTransaction? _ActiveTransaction;
        private bool _IsOpen = false;

        /// <inheritdoc />
        public override bool IsOpen => _IsOpen;

        /// <inheritdoc />
        public override bool IsTransactionActive => _ActiveTransaction != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerDatabaseDriver"/> class.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when settings.Type is not SqlServer.</exception>
        public SqlServerDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            if (settings.Type != DatabaseTypeEnum.SqlServer)
            {
                throw new ArgumentException("Database type must be SqlServer for SqlServerDatabaseDriver.", nameof(settings));
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

                await using SqlConnection connection = new SqlConnection(_ConnectionString);
                await connection.OpenAsync(token).ConfigureAwait(false);
                SqlTransaction? transaction = null;

                if (isTransaction)
                {
                    transaction = connection.BeginTransaction();
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

                        await using SqlCommand cmd = new SqlCommand(query, connection, transaction);
                        cmd.CommandTimeout = Settings.CommandTimeout;

                        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
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

                _ActiveConnection = new SqlConnection(_ConnectionString);
                await _ActiveConnection.OpenAsync(token).ConfigureAwait(false);
                _ActiveTransaction = _ActiveConnection.BeginTransaction();
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
        /// Builds the SQL Server connection string based on settings.
        /// </summary>
        /// <returns>The connection string.</returns>
        private string BuildConnectionString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Server={Settings.Hostname},{Settings.Port};");
            sb.Append($"Database={Settings.DatabaseName};");
            sb.Append($"User Id={Settings.Username};");
            sb.Append($"Password={Settings.Password};");

            if (Settings.RequireEncryption)
            {
                sb.Append("Encrypt=True;");
            }
            else
            {
                sb.Append("Encrypt=False;");
            }

            sb.Append("TrustServerCertificate=True;");

            if (Settings.MinPoolSize > 0)
            {
                sb.Append($"Min Pool Size={Settings.MinPoolSize};");
            }

            if (Settings.MaxPoolSize > 0)
            {
                sb.Append($"Max Pool Size={Settings.MaxPoolSize};");
            }

            sb.Append($"Connect Timeout={Settings.ConnectionTimeout};");
            sb.Append($"Command Timeout={Settings.CommandTimeout};");

            return sb.ToString();
        }

        /// <summary>
        /// Creates the database schema if it doesn't exist.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        private async Task CreateSchemaAsync(CancellationToken token)
        {
            await using SqlConnection connection = new SqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            await using SqlCommand cmd = new SqlCommand(SetupQueries.CreateTables, connection);
            cmd.CommandTimeout = Settings.CommandTimeout;
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates indexes for the database tables.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <remarks>
        /// SQL Server uses IF NOT EXISTS pattern within a single batch script.
        /// </remarks>
        private async Task CreateIndexesAsync(CancellationToken token)
        {
            await using SqlConnection connection = new SqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            await using SqlCommand cmd = new SqlCommand(SetupQueries.CreateIndexes, connection);
            cmd.CommandTimeout = Settings.CommandTimeout;
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

            // Use active transaction connection if one exists
            bool useActiveTransaction = _ActiveTransaction != null && _ActiveConnection != null;

            if (useActiveTransaction)
            {
                await using SqlCommand cmd = new SqlCommand(query, _ActiveConnection, _ActiveTransaction);
                cmd.CommandTimeout = Settings.CommandTimeout;

                await using SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                result.Load(reader);
            }
            else
            {
                await using SqlConnection connection = new SqlConnection(_ConnectionString);
                await connection.OpenAsync(token).ConfigureAwait(false);
                SqlTransaction? transaction = null;

                if (isTransaction)
                {
                    transaction = connection.BeginTransaction();
                }

                try
                {
                    await using SqlCommand cmd = new SqlCommand(query, connection, transaction);
                    cmd.CommandTimeout = Settings.CommandTimeout;

                    await using SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
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
