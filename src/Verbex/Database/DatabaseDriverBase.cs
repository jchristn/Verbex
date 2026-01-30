namespace Verbex.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;

    /// <summary>
    /// Abstract base class for database drivers providing storage backend abstraction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class defines the contract for all database implementations (SQLite, PostgreSQL, MySQL, SQL Server).
    /// Derived classes must implement connection management, query execution, and schema initialization.
    /// </para>
    /// <para>
    /// All method interfaces are initialized by derived classes in their constructors.
    /// The base class provides common functionality for disposal and query execution patterns.
    /// </para>
    /// <para>
    /// Thread safety: Implementations should use appropriate synchronization (e.g., SemaphoreSlim)
    /// for concurrent query execution.
    /// </para>
    /// </remarks>
    public abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        private bool _Disposed = false;

        /// <summary>
        /// Gets the database settings used to configure this driver.
        /// </summary>
        public DatabaseSettings Settings { get; }

        /// <summary>
        /// Gets whether the database connection is currently open.
        /// </summary>
        public abstract bool IsOpen { get; }

        /// <summary>
        /// Gets the tenant operations interface.
        /// </summary>
        /// <value>
        /// Interface for tenant CRUD operations. Initialized by derived classes.
        /// </value>
        public ITenantMethods Tenants { get; protected set; } = null!;

        /// <summary>
        /// Gets the administrator operations interface.
        /// </summary>
        /// <value>
        /// Interface for administrator CRUD operations. Initialized by derived classes.
        /// </value>
        public IAdministratorMethods Administrators { get; protected set; } = null!;

        /// <summary>
        /// Gets the user operations interface.
        /// </summary>
        /// <value>
        /// Interface for user CRUD operations within tenants. Initialized by derived classes.
        /// </value>
        public IUserMethods Users { get; protected set; } = null!;

        /// <summary>
        /// Gets the credential operations interface.
        /// </summary>
        /// <value>
        /// Interface for credential CRUD operations. Initialized by derived classes.
        /// </value>
        public ICredentialMethods Credentials { get; protected set; } = null!;

        /// <summary>
        /// Gets the index operations interface.
        /// </summary>
        /// <value>
        /// Interface for index CRUD operations within tenants. Initialized by derived classes.
        /// </value>
        public IIndexMethods Indexes { get; protected set; } = null!;

        /// <summary>
        /// Gets the document operations interface.
        /// </summary>
        /// <value>
        /// Interface for document CRUD operations. Initialized by derived classes.
        /// </value>
        public IDocumentMethods Documents { get; protected set; } = null!;

        /// <summary>
        /// Gets the term operations interface.
        /// </summary>
        /// <value>
        /// Interface for term vocabulary operations. Initialized by derived classes.
        /// </value>
        public ITermMethods Terms { get; protected set; } = null!;

        /// <summary>
        /// Gets the document-term operations interface.
        /// </summary>
        /// <value>
        /// Interface for document-term mapping operations. Initialized by derived classes.
        /// </value>
        public IDocumentTermMethods DocumentTerms { get; protected set; } = null!;

        /// <summary>
        /// Gets the label operations interface.
        /// </summary>
        /// <value>
        /// Interface for label operations on documents and indexes. Initialized by derived classes.
        /// </value>
        public ILabelMethods Labels { get; protected set; } = null!;

        /// <summary>
        /// Gets the tag operations interface.
        /// </summary>
        /// <value>
        /// Interface for tag operations on documents and indexes. Initialized by derived classes.
        /// </value>
        public ITagMethods Tags { get; protected set; } = null!;

        /// <summary>
        /// Gets the statistics operations interface.
        /// </summary>
        /// <value>
        /// Interface for index and term statistics. Initialized by derived classes.
        /// </value>
        public IStatisticsMethods Statistics { get; protected set; } = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseDriverBase"/> class.
        /// </summary>
        /// <param name="settings">Database settings for this driver.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        protected DatabaseDriverBase(DatabaseSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Initializes the database connection and creates the schema if needed.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
        /// <remarks>
        /// This method should:
        /// <list type="bullet">
        /// <item>Establish the database connection</item>
        /// <item>Create all required tables if they don't exist</item>
        /// <item>Create all required indexes</item>
        /// <item>Apply any pending migrations</item>
        /// <item>Initialize all method interface implementations</item>
        /// </list>
        /// </remarks>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Executes a single SQL query and returns the results.
        /// </summary>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="isTransaction">Whether to wrap the query in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A DataTable containing the query results, or an empty DataTable for non-SELECT queries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the connection is not open.</exception>
        /// <remarks>
        /// For INSERT, UPDATE, DELETE operations, the returned DataTable will be empty but not null.
        /// Implementations should use parameterized queries where possible to prevent SQL injection.
        /// </remarks>
        public abstract Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Executes multiple SQL queries in a batch.
        /// </summary>
        /// <param name="queries">The SQL queries to execute.</param>
        /// <param name="isTransaction">Whether to wrap all queries in a single transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A DataTable containing the results of the last query.</returns>
        /// <exception cref="ArgumentNullException">Thrown when queries is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the connection is not open.</exception>
        /// <remarks>
        /// When isTransaction is true, all queries are committed together or rolled back on failure.
        /// This is more efficient than executing queries individually for bulk operations.
        /// </remarks>
        public abstract Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Closes the database connection gracefully.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method should:
        /// <list type="bullet">
        /// <item>Complete any pending operations</item>
        /// <item>Perform cleanup (e.g., WAL checkpoint for SQLite)</item>
        /// <item>Close the connection</item>
        /// </list>
        /// </remarks>
        public abstract Task CloseAsync(CancellationToken token = default);

        /// <summary>
        /// Flushes any pending changes to persistent storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// For file-based databases (SQLite), this may perform a WAL checkpoint.
        /// For server-based databases, this may be a no-op.
        /// </remarks>
        public virtual Task FlushAsync(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Begins a database transaction for batching multiple operations.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a transaction is already active.</exception>
        /// <remarks>
        /// When a transaction is active, all subsequent queries executed via ExecuteQueryAsync
        /// will use the active transaction instead of creating individual transactions.
        /// Call CommitTransactionAsync or RollbackTransactionAsync to complete the transaction.
        /// </remarks>
        public abstract Task BeginTransactionAsync(CancellationToken token = default);

        /// <summary>
        /// Commits the active transaction.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
        public abstract Task CommitTransactionAsync(CancellationToken token = default);

        /// <summary>
        /// Rolls back the active transaction.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
        public abstract Task RollbackTransactionAsync(CancellationToken token = default);

        /// <summary>
        /// Gets whether a transaction is currently active.
        /// </summary>
        public abstract bool IsTransactionActive { get; }

        /// <summary>
        /// Throws an exception if the driver has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the driver has been disposed.</exception>
        protected void ThrowIfDisposed()
        {
            if (_Disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// Throws an exception if the database connection is not open.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the connection is not open.</exception>
        protected void ThrowIfNotOpen()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Database connection is not open. Call InitializeAsync first.");
            }
        }

        /// <summary>
        /// Releases all resources used by the database driver.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously releases all resources used by the database driver.
        /// </summary>
        /// <returns>A ValueTask representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the database driver and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                CloseAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            _Disposed = true;
        }

        /// <summary>
        /// Performs asynchronous cleanup of managed resources.
        /// </summary>
        /// <returns>A ValueTask representing the asynchronous operation.</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_Disposed)
            {
                return;
            }

            await CloseAsync(CancellationToken.None).ConfigureAwait(false);
            _Disposed = true;
        }
    }
}
