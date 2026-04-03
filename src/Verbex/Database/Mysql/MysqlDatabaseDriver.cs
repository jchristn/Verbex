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
        private const int UnlimitedCommandTimeout = 0;
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private string? _ConnectionString;
        private readonly AsyncLocal<MySqlConnection?> _ActiveConnection = new AsyncLocal<MySqlConnection?>();
        private readonly AsyncLocal<MySqlTransaction?> _ActiveTransaction = new AsyncLocal<MySqlTransaction?>();
        private bool _IsOpen = false;

        /// <inheritdoc />
        public override bool IsOpen => _IsOpen;

        /// <inheritdoc />
        public override bool IsTransactionActive => _ActiveTransaction.Value != null;

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
        public override async Task CreateIndexTablesAsync(string tablePrefix, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            string createTablesQuery = Queries.SetupQueries.CreateIndexTables(tablePrefix);
            await ExecuteNonQueryAsync(createTablesQuery, false, UnlimitedCommandTimeout, token).ConfigureAwait(false);

            List<string> indexQueries = Queries.SetupQueries.CreateIndexTableIndexes(tablePrefix);
            await using MySqlConnection connection = new MySqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            foreach (string indexQuery in indexQueries)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    await using MySqlCommand cmd = new MySqlCommand(indexQuery, connection);
                    cmd.CommandTimeout = UnlimitedCommandTimeout;
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
                catch (MySqlException ex) when (ex.Number == 1061)
                {
                    // Duplicate key name, index already exists.
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

            return await ExecuteQueryInternalAsync(query, isTransaction, token).ConfigureAwait(false);
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

            DataTable result = new DataTable();
            MySqlConnection? activeConnection = _ActiveConnection.Value;
            MySqlTransaction? activeTransaction = _ActiveTransaction.Value;
            bool useActiveTransaction = activeTransaction != null && activeConnection != null;
            MySqlConnection? connection = activeConnection;
            MySqlTransaction? localTransaction = null;

            try
            {
                if (!useActiveTransaction)
                {
                    connection = new MySqlConnection(_ConnectionString);
                    await connection.OpenAsync(token).ConfigureAwait(false);

                    if (isTransaction)
                    {
                        localTransaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                    }
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

                        await using MySqlCommand cmd = new MySqlCommand(query, connection, useActiveTransaction ? activeTransaction : localTransaction);
                        cmd.CommandTimeout = Settings.CommandTimeout;

                        await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                        result = await LoadDataTableWithoutConstraintsAsync(reader, token).ConfigureAwait(false);
                    }

                    if (localTransaction != null)
                    {
                        await localTransaction.CommitAsync(token).ConfigureAwait(false);
                    }
                }
                catch
                {
                    if (localTransaction != null)
                    {
                        await localTransaction.RollbackAsync(token).ConfigureAwait(false);
                    }
                    throw;
                }
                finally
                {
                    if (localTransaction != null)
                    {
                        await localTransaction.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (!useActiveTransaction && connection != null)
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public override async Task CloseAsync(CancellationToken token = default)
        {
            await _Semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_ActiveTransaction.Value != null)
                {
                    await _ActiveTransaction.Value.DisposeAsync().ConfigureAwait(false);
                    _ActiveTransaction.Value = null;
                }

                if (_ActiveConnection.Value != null)
                {
                    await _ActiveConnection.Value.DisposeAsync().ConfigureAwait(false);
                    _ActiveConnection.Value = null;
                }

                _IsOpen = false;
                _ConnectionString = null;
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

            if (_ActiveTransaction.Value != null)
            {
                throw new InvalidOperationException("A transaction is already active.");
            }

            MySqlConnection connection = new MySqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            try
            {
                MySqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                _ActiveConnection.Value = connection;
                _ActiveTransaction.Value = transaction;
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task CommitTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            MySqlTransaction? transaction = _ActiveTransaction.Value;
            MySqlConnection? connection = _ActiveConnection.Value;
            if (transaction == null || connection == null)
            {
                throw new InvalidOperationException("No transaction is active.");
            }

            try
            {
                await transaction.CommitAsync(token).ConfigureAwait(false);
            }
            finally
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
                await connection.DisposeAsync().ConfigureAwait(false);
                _ActiveTransaction.Value = null;
                _ActiveConnection.Value = null;
            }
        }

        /// <inheritdoc />
        public override async Task RollbackTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            MySqlTransaction? transaction = _ActiveTransaction.Value;
            MySqlConnection? connection = _ActiveConnection.Value;
            if (transaction == null || connection == null)
            {
                throw new InvalidOperationException("No transaction is active.");
            }

            try
            {
                await transaction.RollbackAsync(token).ConfigureAwait(false);
            }
            finally
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
                await connection.DisposeAsync().ConfigureAwait(false);
                _ActiveTransaction.Value = null;
                _ActiveConnection.Value = null;
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
            await ExecuteNonQueryAsync(SetupQueries.CreateTables, false, UnlimitedCommandTimeout, token).ConfigureAwait(false);
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
                    cmd.CommandTimeout = UnlimitedCommandTimeout;
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
        /// Loads data from a reader into a DataTable without constraint checking.
        /// </summary>
        /// <param name="reader">The data reader to read from.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A DataTable containing the data.</returns>
        private static async Task<DataTable> LoadDataTableWithoutConstraintsAsync(MySqlDataReader reader, CancellationToken token)
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

            MySqlConnection? activeConnection = _ActiveConnection.Value;
            MySqlTransaction? activeTransaction = _ActiveTransaction.Value;
            bool useActiveTransaction = activeTransaction != null && activeConnection != null;

            if (useActiveTransaction)
            {
                await using MySqlCommand cmd = new MySqlCommand(query, activeConnection, activeTransaction);
                cmd.CommandTimeout = Settings.CommandTimeout;

                await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                result = await LoadDataTableWithoutConstraintsAsync(reader, token).ConfigureAwait(false);
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

        /// <summary>
        /// Executes a non-query script with an optional transaction and command timeout override.
        /// </summary>
        private async Task ExecuteNonQueryAsync(string query, bool isTransaction, int commandTimeout, CancellationToken token)
        {
            MySqlConnection? activeConnection = _ActiveConnection.Value;
            MySqlTransaction? activeTransaction = _ActiveTransaction.Value;
            bool useActiveTransaction = activeTransaction != null && activeConnection != null;

            if (useActiveTransaction)
            {
                await using MySqlCommand cmd = new MySqlCommand(query, activeConnection, activeTransaction);
                cmd.CommandTimeout = commandTimeout;
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return;
            }

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
                cmd.CommandTimeout = commandTimeout;
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

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
