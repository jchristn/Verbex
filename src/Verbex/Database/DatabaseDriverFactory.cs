namespace Verbex.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database.Interfaces;
    using Verbex.Database.Mysql;
    using Verbex.Database.Postgresql;
    using Verbex.Database.Sqlite;
    using Verbex.Database.SqlServer;

    /// <summary>
    /// Factory for creating database driver instances based on configuration.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        /// <summary>
        /// Creates a database driver based on the provided settings.
        /// </summary>
        /// <param name="settings">Database configuration settings.</param>
        /// <returns>A database driver instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the database type is not supported.</exception>
        public static DatabaseDriverBase Create(DatabaseSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Validate();

            return settings.Type switch
            {
                DatabaseTypeEnum.Sqlite => new SqliteDatabaseDriver(settings),
                DatabaseTypeEnum.Postgresql => new PostgresqlDatabaseDriver(settings),
                DatabaseTypeEnum.Mysql => new MysqlDatabaseDriver(settings),
                DatabaseTypeEnum.SqlServer => new SqlServerDatabaseDriver(settings),
                _ => throw new NotSupportedException($"Database type '{settings.Type}' is not supported.")
            };
        }

        /// <summary>
        /// Creates and initializes a database driver based on the provided settings.
        /// </summary>
        /// <param name="settings">Database configuration settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An initialized database driver instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the database type is not supported.</exception>
        public static async Task<DatabaseDriverBase> CreateAndInitializeAsync(DatabaseSettings settings, CancellationToken token = default)
        {
            DatabaseDriverBase driver = Create(settings);
            await driver.InitializeAsync(token).ConfigureAwait(false);
            return driver;
        }
    }
}
