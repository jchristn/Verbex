namespace Verbex.Database
{
    using System;

    /// <summary>
    /// Configuration settings for database connectivity.
    /// </summary>
    /// <remarks>
    /// Supports SQLite (file or in-memory), PostgreSQL, MySQL, and SQL Server.
    /// For SQLite, use Filename and InMemory properties.
    /// For server-based databases, use Hostname, Port, DatabaseName, Username, and Password.
    /// </remarks>
    public class DatabaseSettings
    {
        private DatabaseTypeEnum _Type = DatabaseTypeEnum.Sqlite;
        private string _Filename = "./verbex.db";
        private bool _InMemory = false;
        private string _Hostname = "localhost";
        private int _Port = 0;
        private string _DatabaseName = "verbex";
        private string _Username = string.Empty;
        private string _Password = string.Empty;
        private bool _RequireEncryption = false;
        private string _Schema = string.Empty;
        private int _MinPoolSize = 1;
        private int _MaxPoolSize = 100;
        private int _CommandTimeout = 30;
        private int _ConnectionTimeout = 30;
        private int _AutoFlushInterval = 100;

        /// <summary>
        /// Gets or sets the database type.
        /// </summary>
        /// <value>The type of database to use. Default is <see cref="DatabaseTypeEnum.Sqlite"/>.</value>
        public DatabaseTypeEnum Type
        {
            get => _Type;
            set => _Type = value;
        }

        /// <summary>
        /// Gets or sets the filename for SQLite database storage.
        /// </summary>
        /// <value>
        /// The path to the SQLite database file. Default is "./verbex.db".
        /// Ignored when <see cref="InMemory"/> is true or when using server-based databases.
        /// </value>
        public string Filename
        {
            get => _Filename;
            set => _Filename = value ?? "./verbex.db";
        }

        /// <summary>
        /// Gets or sets whether to use in-memory SQLite storage.
        /// </summary>
        /// <value>
        /// True to use in-memory storage; false to use file storage. Default is false.
        /// Only applicable when <see cref="Type"/> is <see cref="DatabaseTypeEnum.Sqlite"/>.
        /// </value>
        public bool InMemory
        {
            get => _InMemory;
            set => _InMemory = value;
        }

        /// <summary>
        /// Gets or sets the database server hostname.
        /// </summary>
        /// <value>
        /// The hostname or IP address of the database server. Default is "localhost".
        /// Used for PostgreSQL, MySQL, and SQL Server connections.
        /// </value>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = value ?? "localhost";
        }

        /// <summary>
        /// Gets or sets the database server port.
        /// </summary>
        /// <value>
        /// The port number to connect to. A value of 0 uses the database-specific default port.
        /// Default ports: PostgreSQL=5432, MySQL=3306, SQL Server=1433.
        /// </value>
        public int Port
        {
            get => _Port;
            set => _Port = value >= 0 ? value : 0;
        }

        /// <summary>
        /// Gets or sets the database name.
        /// </summary>
        /// <value>
        /// The name of the database to connect to. Default is "verbex".
        /// Used for PostgreSQL, MySQL, and SQL Server connections.
        /// </value>
        public string DatabaseName
        {
            get => _DatabaseName;
            set => _DatabaseName = value ?? "verbex";
        }

        /// <summary>
        /// Gets or sets the username for database authentication.
        /// </summary>
        /// <value>
        /// The username for database authentication. Empty string indicates no authentication.
        /// Used for PostgreSQL, MySQL, and SQL Server connections.
        /// </value>
        public string Username
        {
            get => _Username;
            set => _Username = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the password for database authentication.
        /// </summary>
        /// <value>
        /// The password for database authentication. Empty string indicates no password.
        /// This value should be handled securely and not logged.
        /// </value>
        public string Password
        {
            get => _Password;
            set => _Password = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets whether to require encrypted connections.
        /// </summary>
        /// <value>
        /// True to require SSL/TLS encryption; false to allow unencrypted connections.
        /// Default is false. Recommended true for production environments.
        /// </value>
        public bool RequireEncryption
        {
            get => _RequireEncryption;
            set => _RequireEncryption = value;
        }

        /// <summary>
        /// Gets or sets the schema name for databases that support schemas.
        /// </summary>
        /// <value>
        /// The schema name. Empty string uses the default schema.
        /// Applicable to PostgreSQL (default: "public") and SQL Server (default: "dbo").
        /// </value>
        public string Schema
        {
            get => _Schema;
            set => _Schema = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the minimum connection pool size.
        /// </summary>
        /// <value>
        /// The minimum number of connections to maintain in the pool. Default is 1.
        /// Must be greater than 0 and less than or equal to <see cref="MaxPoolSize"/>.
        /// </value>
        public int MinPoolSize
        {
            get => _MinPoolSize;
            set => _MinPoolSize = value > 0 ? value : 1;
        }

        /// <summary>
        /// Gets or sets the maximum connection pool size.
        /// </summary>
        /// <value>
        /// The maximum number of connections allowed in the pool. Default is 100.
        /// Must be greater than or equal to <see cref="MinPoolSize"/>.
        /// </value>
        public int MaxPoolSize
        {
            get => _MaxPoolSize;
            set => _MaxPoolSize = value > 0 ? value : 100;
        }

        /// <summary>
        /// Gets or sets the command execution timeout in seconds.
        /// </summary>
        /// <value>
        /// The timeout for individual SQL commands in seconds. Default is 30.
        /// A value of 0 indicates no timeout.
        /// </value>
        public int CommandTimeout
        {
            get => _CommandTimeout;
            set => _CommandTimeout = value >= 0 ? value : 30;
        }

        /// <summary>
        /// Gets or sets the connection timeout in seconds.
        /// </summary>
        /// <value>
        /// The timeout for establishing a connection in seconds. Default is 30.
        /// A value of 0 indicates no timeout.
        /// </value>
        public int ConnectionTimeout
        {
            get => _ConnectionTimeout;
            set => _ConnectionTimeout = value >= 0 ? value : 30;
        }

        /// <summary>
        /// Gets or sets the automatic flush interval for WAL checkpointing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// During sustained write operations (e.g., bulk document ingestion), SQLite's Write-Ahead Log (WAL)
        /// can grow significantly. A large WAL file degrades read performance because SQLite must scan the
        /// WAL for each read operation to check for newer page versions.
        /// </para>
        /// <para>
        /// This setting controls how often an automatic WAL checkpoint (TRUNCATE mode) is performed during
        /// document ingestion. After the specified number of documents are added, a checkpoint is triggered
        /// to flush WAL data to the main database file and reset the WAL.
        /// </para>
        /// <para>
        /// This setting is only used for file-based SQLite databases. It is ignored when:
        /// <list type="bullet">
        /// <item><description><see cref="InMemory"/> is true (in-memory databases have no WAL file)</description></item>
        /// <item><description><see cref="Type"/> is not <see cref="DatabaseTypeEnum.Sqlite"/></description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <value>
        /// The number of documents to add before triggering an automatic flush.
        /// Default is 100. Minimum is 1. Maximum is 10000.
        /// A lower value reduces WAL growth but increases I/O overhead.
        /// A higher value improves write throughput but may degrade read performance during bulk operations.
        /// </value>
        public int AutoFlushInterval
        {
            get => _AutoFlushInterval;
            set
            {
                if (value < 1)
                {
                    _AutoFlushInterval = 1;
                }
                else if (value > 10000)
                {
                    _AutoFlushInterval = 10000;
                }
                else
                {
                    _AutoFlushInterval = value;
                }
            }
        }

        /// <summary>
        /// Gets the default port number for the specified database type.
        /// </summary>
        /// <returns>
        /// The default port: SQLite=0 (not applicable), PostgreSQL=5432, MySQL=3306, SQL Server=1433.
        /// </returns>
        public int GetDefaultPort()
        {
            return Type switch
            {
                DatabaseTypeEnum.Sqlite => 0,
                DatabaseTypeEnum.Postgresql => 5432,
                DatabaseTypeEnum.Mysql => 3306,
                DatabaseTypeEnum.SqlServer => 1433,
                _ => 0
            };
        }

        /// <summary>
        /// Gets the effective port number, using the default if not explicitly specified.
        /// </summary>
        /// <returns>
        /// The configured <see cref="Port"/> if greater than 0, otherwise the default port for the database type.
        /// </returns>
        public int GetEffectivePort()
        {
            return Port > 0 ? Port : GetDefaultPort();
        }

        /// <summary>
        /// Validates the settings and throws an exception if invalid.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the settings are invalid:
        /// - SQLite file mode requires a non-empty filename.
        /// - Server-based databases require a hostname.
        /// - MinPoolSize exceeds MaxPoolSize.
        /// </exception>
        public void Validate()
        {
            if (Type == DatabaseTypeEnum.Sqlite && !InMemory && string.IsNullOrWhiteSpace(Filename))
            {
                throw new InvalidOperationException("SQLite file mode requires a non-empty filename.");
            }

            if (Type != DatabaseTypeEnum.Sqlite && string.IsNullOrWhiteSpace(Hostname))
            {
                throw new InvalidOperationException("Server-based databases require a hostname.");
            }

            if (Type != DatabaseTypeEnum.Sqlite && string.IsNullOrWhiteSpace(DatabaseName))
            {
                throw new InvalidOperationException("Server-based databases require a database name.");
            }

            if (MinPoolSize > MaxPoolSize)
            {
                throw new InvalidOperationException("MinPoolSize cannot exceed MaxPoolSize.");
            }
        }

        /// <summary>
        /// Creates a deep copy of this settings instance.
        /// </summary>
        /// <returns>A new <see cref="DatabaseSettings"/> instance with the same values.</returns>
        public DatabaseSettings Clone()
        {
            return new DatabaseSettings
            {
                Type = Type,
                Filename = Filename,
                InMemory = InMemory,
                Hostname = Hostname,
                Port = Port,
                DatabaseName = DatabaseName,
                Username = Username,
                Password = Password,
                RequireEncryption = RequireEncryption,
                Schema = Schema,
                MinPoolSize = MinPoolSize,
                MaxPoolSize = MaxPoolSize,
                CommandTimeout = CommandTimeout,
                ConnectionTimeout = ConnectionTimeout,
                AutoFlushInterval = AutoFlushInterval
            };
        }

        /// <summary>
        /// Creates default settings for in-memory SQLite storage.
        /// </summary>
        /// <returns>A new <see cref="DatabaseSettings"/> configured for in-memory SQLite.</returns>
        public static DatabaseSettings CreateInMemory()
        {
            return new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                InMemory = true
            };
        }

        /// <summary>
        /// Creates default settings for file-based SQLite storage.
        /// </summary>
        /// <param name="filename">The path to the SQLite database file.</param>
        /// <returns>A new <see cref="DatabaseSettings"/> configured for file-based SQLite.</returns>
        /// <exception cref="ArgumentNullException">Thrown when filename is null or whitespace.</exception>
        public static DatabaseSettings CreateSqliteFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename), "Filename cannot be null or whitespace.");
            }

            return new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = filename,
                InMemory = false
            };
        }

        /// <summary>
        /// Creates default settings for PostgreSQL connection.
        /// </summary>
        /// <param name="hostname">The database server hostname.</param>
        /// <param name="databaseName">The database name.</param>
        /// <param name="username">The username for authentication.</param>
        /// <param name="password">The password for authentication.</param>
        /// <returns>A new <see cref="DatabaseSettings"/> configured for PostgreSQL.</returns>
        public static DatabaseSettings CreatePostgresql(string hostname, string databaseName, string username = "", string password = "")
        {
            return new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Postgresql,
                Hostname = hostname ?? "localhost",
                DatabaseName = databaseName ?? "verbex",
                Username = username ?? string.Empty,
                Password = password ?? string.Empty
            };
        }

        /// <summary>
        /// Creates default settings for MySQL connection.
        /// </summary>
        /// <param name="hostname">The database server hostname.</param>
        /// <param name="databaseName">The database name.</param>
        /// <param name="username">The username for authentication.</param>
        /// <param name="password">The password for authentication.</param>
        /// <returns>A new <see cref="DatabaseSettings"/> configured for MySQL.</returns>
        public static DatabaseSettings CreateMysql(string hostname, string databaseName, string username = "", string password = "")
        {
            return new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Mysql,
                Hostname = hostname ?? "localhost",
                DatabaseName = databaseName ?? "verbex",
                Username = username ?? string.Empty,
                Password = password ?? string.Empty
            };
        }

        /// <summary>
        /// Creates default settings for SQL Server connection.
        /// </summary>
        /// <param name="hostname">The database server hostname.</param>
        /// <param name="databaseName">The database name.</param>
        /// <param name="username">The username for authentication.</param>
        /// <param name="password">The password for authentication.</param>
        /// <returns>A new <see cref="DatabaseSettings"/> configured for SQL Server.</returns>
        public static DatabaseSettings CreateSqlServer(string hostname, string databaseName, string username = "", string password = "")
        {
            return new DatabaseSettings
            {
                Type = DatabaseTypeEnum.SqlServer,
                Hostname = hostname ?? "localhost",
                DatabaseName = databaseName ?? "verbex",
                Username = username ?? string.Empty,
                Password = password ?? string.Empty
            };
        }
    }
}
