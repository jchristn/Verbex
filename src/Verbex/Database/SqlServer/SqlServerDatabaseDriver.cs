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
        private const int UnlimitedCommandTimeout = 0;
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private string? _ConnectionString;
        private readonly AsyncLocal<ActiveTransactionContext?> _ActiveTransactionContext = new AsyncLocal<ActiveTransactionContext?>();
        private bool _IsOpen = false;

        private sealed class ActiveTransactionContext
        {
            public SqlConnection? Connection { get; set; }

            public SqlTransaction? Transaction { get; set; }
        }

        /// <inheritdoc />
        public override bool IsOpen => _IsOpen;

        /// <inheritdoc />
        public override bool IsTransactionActive => _ActiveTransactionContext.Value?.Transaction != null;

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
                _IsOpen = true;
                try
                {
                    await EnsureRequestHistorySchemaAsync(token).ConfigureAwait(false);
                    InitializeMethodImplementations();
                }
                catch
                {
                    _IsOpen = false;
                    throw;
                }
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
            foreach (string indexQuery in indexQueries)
            {
                await ExecuteNonQueryAsync(indexQuery, false, UnlimitedCommandTimeout, token).ConfigureAwait(false);
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
            SqlConnection? activeConnection = _ActiveTransactionContext.Value?.Connection;
            SqlTransaction? activeTransaction = _ActiveTransactionContext.Value?.Transaction;
            bool useActiveTransaction = activeTransaction != null && activeConnection != null;
            SqlConnection? connection = activeConnection;
            SqlTransaction? localTransaction = null;

            try
            {
                if (!useActiveTransaction)
                {
                    connection = new SqlConnection(_ConnectionString);
                    await connection.OpenAsync(token).ConfigureAwait(false);

                    if (isTransaction)
                    {
                        localTransaction = connection.BeginTransaction();
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

                        await using SqlCommand cmd = new SqlCommand(query, connection, useActiveTransaction ? activeTransaction : localTransaction);
                        cmd.CommandTimeout = Settings.CommandTimeout;

                        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
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
                ActiveTransactionContext? activeContext = _ActiveTransactionContext.Value;
                if (activeContext?.Transaction != null)
                {
                    await activeContext.Transaction.DisposeAsync().ConfigureAwait(false);
                    activeContext.Transaction = null;
                }

                if (activeContext?.Connection != null)
                {
                    await activeContext.Connection.DisposeAsync().ConfigureAwait(false);
                    activeContext.Connection = null;
                }
                _ActiveTransactionContext.Value = null;

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

            if (_ActiveTransactionContext.Value?.Transaction != null)
            {
                throw new InvalidOperationException("A transaction is already active.");
            }

            ActiveTransactionContext context = new ActiveTransactionContext();
            _ActiveTransactionContext.Value = context;
            SqlConnection? connection = null;

            try
            {
                connection = new SqlConnection(_ConnectionString);
                await connection.OpenAsync(token).ConfigureAwait(false);
                SqlTransaction transaction = connection.BeginTransaction();
                context.Connection = connection;
                context.Transaction = transaction;
            }
            catch
            {
                _ActiveTransactionContext.Value = null;
                if (context.Transaction != null)
                {
                    await context.Transaction.DisposeAsync().ConfigureAwait(false);
                    context.Transaction = null;
                }

                if (connection != null)
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task CommitTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            ActiveTransactionContext? context = _ActiveTransactionContext.Value;
            SqlTransaction? transaction = context?.Transaction;
            SqlConnection? connection = context?.Connection;
            if (transaction == null || connection == null)
            {
                _ActiveTransactionContext.Value = null;
                return;
            }

            try
            {
                await transaction.CommitAsync(token).ConfigureAwait(false);
            }
            finally
            {
                if (context != null)
                {
                    context.Transaction = null;
                    context.Connection = null;
                }
                _ActiveTransactionContext.Value = null;
                await transaction.DisposeAsync().ConfigureAwait(false);
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task RollbackTransactionAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            ActiveTransactionContext? context = _ActiveTransactionContext.Value;
            SqlTransaction? transaction = context?.Transaction;
            SqlConnection? connection = context?.Connection;
            if (transaction == null || connection == null)
            {
                _ActiveTransactionContext.Value = null;
                return;
            }

            try
            {
                await transaction.RollbackAsync(token).ConfigureAwait(false);
            }
            finally
            {
                if (context != null)
                {
                    context.Transaction = null;
                    context.Connection = null;
                }
                _ActiveTransactionContext.Value = null;
                await transaction.DisposeAsync().ConfigureAwait(false);
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            ActiveTransactionContext? activeContext = _ActiveTransactionContext.Value;
            if (activeContext?.Transaction != null && activeContext.Connection != null)
            {
                return await operation(token).ConfigureAwait(false);
            }

            if (activeContext != null)
            {
                throw new InvalidOperationException("Inconsistent transaction state.");
            }

            ActiveTransactionContext context = new ActiveTransactionContext();
            _ActiveTransactionContext.Value = context;
            SqlConnection? connection = null;
            SqlTransaction? transaction = null;

            try
            {
                connection = new SqlConnection(_ConnectionString);
                await connection.OpenAsync(token).ConfigureAwait(false);
                transaction = connection.BeginTransaction();
                context.Connection = connection;
                context.Transaction = transaction;

                T result = await operation(token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return result;
            }
            catch
            {
                if (transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Preserve the original operation failure.
                    }
                }

                throw;
            }
            finally
            {
                context.Transaction = null;
                context.Connection = null;
                _ActiveTransactionContext.Value = null;

                if (transaction != null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }

                if (connection != null)
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
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
            await ExecuteNonQueryAsync(SetupQueries.CreateTables, false, UnlimitedCommandTimeout, token).ConfigureAwait(false);
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
            await ExecuteNonQueryAsync(SetupQueries.CreateIndexes, false, UnlimitedCommandTimeout, token).ConfigureAwait(false);
        }

        private async Task EnsureRequestHistorySchemaAsync(CancellationToken token)
        {
            await ExecuteNonQueryAsync(RequestHistorySchema.GetCreateTableQuery(Settings.Type), false, UnlimitedCommandTimeout, token).ConfigureAwait(false);
            await ExecuteNonQueryAsync(RequestHistorySchema.GetCreateDetailTableQuery(Settings.Type), false, UnlimitedCommandTimeout, token).ConfigureAwait(false);

            await EnsureRequestHistoryColumnsAsync("request_history", RequestHistorySchema.RequestHistoryColumns, token).ConfigureAwait(false);
            await EnsureRequestHistoryColumnsAsync("request_history_detail", RequestHistorySchema.RequestHistoryDetailColumns, token).ConfigureAwait(false);

            foreach (string indexQuery in RequestHistorySchema.GetCreateIndexQueries(Settings.Type))
            {
                await ExecuteNonQueryAsync(indexQuery, false, UnlimitedCommandTimeout, token).ConfigureAwait(false);
            }
        }

        private async Task EnsureRequestHistoryColumnsAsync(string tableName, IReadOnlyList<RequestHistorySchema.RequestHistoryColumnDefinition> columns, CancellationToken token)
        {
            HashSet<string> existingColumns = await GetExistingRequestHistoryColumnsAsync(tableName, token).ConfigureAwait(false);

            foreach (RequestHistorySchema.RequestHistoryColumnDefinition column in columns)
            {
                if (existingColumns.Contains(column.Name))
                {
                    continue;
                }

                try
                {
                    await ExecuteNonQueryAsync(RequestHistorySchema.GetAddColumnQuery(Settings.Type, tableName, column), false, UnlimitedCommandTimeout, token).ConfigureAwait(false);
                    existingColumns.Add(column.Name);
                }
                catch (SqlException ex) when (RequestHistorySchema.CanIgnoreDuplicateColumnException(ex))
                {
                    existingColumns.Add(column.Name);
                }
            }
        }

        private async Task<HashSet<string>> GetExistingRequestHistoryColumnsAsync(string tableName, CancellationToken token)
        {
            DataTable result = await ExecuteQueryAsync(RequestHistorySchema.GetExistingColumnsQuery(Settings, tableName), false, token).ConfigureAwait(false);
            HashSet<string> columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in result.Rows)
            {
                string? columnName = RequestHistorySchema.GetColumnName(row);
                if (!String.IsNullOrWhiteSpace(columnName))
                {
                    columns.Add(columnName);
                }
            }

            return columns;
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
        private static async Task<DataTable> LoadDataTableWithoutConstraintsAsync(SqlDataReader reader, CancellationToken token)
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

            SqlConnection? activeConnection = _ActiveTransactionContext.Value?.Connection;
            SqlTransaction? activeTransaction = _ActiveTransactionContext.Value?.Transaction;
            bool useActiveTransaction = activeTransaction != null && activeConnection != null;

            if (useActiveTransaction)
            {
                await using SqlCommand cmd = new SqlCommand(query, activeConnection, activeTransaction);
                cmd.CommandTimeout = Settings.CommandTimeout;

                await using SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                result = await LoadDataTableWithoutConstraintsAsync(reader, token).ConfigureAwait(false);
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
            SqlConnection? activeConnection = _ActiveTransactionContext.Value?.Connection;
            SqlTransaction? activeTransaction = _ActiveTransactionContext.Value?.Transaction;
            bool useActiveTransaction = activeTransaction != null && activeConnection != null;

            if (useActiveTransaction)
            {
                await using SqlCommand cmd = new SqlCommand(query, activeConnection, activeTransaction);
                cmd.CommandTimeout = commandTimeout;
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return;
            }

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
