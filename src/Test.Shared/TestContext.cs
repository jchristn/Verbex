namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Verbex;
    using Verbex.Database;

    /// <summary>
    /// Test context that manages storage mode settings and database configuration for all tests.
    /// </summary>
    public static class TestContext
    {
        private static StorageMode _CurrentStorageMode = StorageMode.InMemory;
        private static CommandLineOptions? _Options = null;
        private static DatabaseSettings? _DatabaseSettings = null;

        /// <summary>
        /// Gets the current storage mode for tests.
        /// </summary>
        public static StorageMode CurrentStorageMode => _CurrentStorageMode;

        /// <summary>
        /// Gets the command-line options.
        /// </summary>
        public static CommandLineOptions? Options => _Options;

        /// <summary>
        /// Gets the database settings for multi-tenancy tests.
        /// </summary>
        public static DatabaseSettings? DatabaseSettings => _DatabaseSettings;

        /// <summary>
        /// Gets whether cleanup should be performed after tests.
        /// </summary>
        public static bool ShouldCleanup => _Options == null || !_Options.NoCleanup;

        /// <summary>
        /// Initializes the test context with command-line options.
        /// </summary>
        /// <param name="options">Parsed command-line options.</param>
        public static void Initialize(CommandLineOptions options)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _DatabaseSettings = CreateDatabaseSettings(options);
        }

        /// <summary>
        /// Creates database settings from command-line options.
        /// </summary>
        /// <param name="options">Command-line options.</param>
        /// <returns>Database settings configured from the options.</returns>
        private static DatabaseSettings CreateDatabaseSettings(CommandLineOptions options)
        {
            DatabaseSettings settings = new DatabaseSettings();

            switch (options.DatabaseType)
            {
                case TestDatabaseType.SqliteRam:
                    settings.Type = DatabaseTypeEnum.Sqlite;
                    settings.InMemory = true;
                    break;

                case TestDatabaseType.Sqlite:
                    settings.Type = DatabaseTypeEnum.Sqlite;
                    settings.InMemory = false;
                    settings.Filename = options.Filename;
                    break;

                case TestDatabaseType.SqlServer:
                    settings.Type = DatabaseTypeEnum.SqlServer;
                    settings.Hostname = options.Hostname;
                    settings.Port = options.Port;
                    settings.Username = options.Username;
                    settings.Password = options.Password;
                    settings.DatabaseName = options.DatabaseName;
                    if (!string.IsNullOrEmpty(options.Schema))
                    {
                        settings.Schema = options.Schema;
                    }
                    break;

                case TestDatabaseType.Postgres:
                    settings.Type = DatabaseTypeEnum.Postgresql;
                    settings.Hostname = options.Hostname;
                    settings.Port = options.Port;
                    settings.Username = options.Username;
                    settings.Password = options.Password;
                    settings.DatabaseName = options.DatabaseName;
                    if (!string.IsNullOrEmpty(options.Schema))
                    {
                        settings.Schema = options.Schema;
                    }
                    break;

                case TestDatabaseType.Mysql:
                    settings.Type = DatabaseTypeEnum.Mysql;
                    settings.Hostname = options.Hostname;
                    settings.Port = options.Port;
                    settings.Username = options.Username;
                    settings.Password = options.Password;
                    settings.DatabaseName = options.DatabaseName;
                    break;
            }

            return settings;
        }

        /// <summary>
        /// Sets the storage mode for tests.
        /// </summary>
        /// <param name="storageMode">Storage mode to use.</param>
        public static void SetStorageMode(StorageMode storageMode)
        {
            _CurrentStorageMode = storageMode;
        }

        /// <summary>
        /// Clears the current storage mode settings.
        /// </summary>
        public static void ClearStorageMode()
        {
            _CurrentStorageMode = StorageMode.InMemory;
        }

        /// <summary>
        /// Creates an InvertedIndex using the current test context settings and opens it.
        /// </summary>
        /// <param name="indexName">Name for the index.</param>
        /// <param name="customConfig">Optional custom configuration.</param>
        /// <returns>Task returning an opened InvertedIndex configured for current test context.</returns>
        public static async Task<InvertedIndex> CreateTestIndexAsync(string? indexName = null, VerbexConfiguration? customConfig = null)
        {
            string name = indexName ?? $"test_{Guid.NewGuid():N}";
            VerbexConfiguration config = customConfig?.Clone() ?? new VerbexConfiguration();

            config.StorageMode = _CurrentStorageMode;

            if (_CurrentStorageMode == StorageMode.OnDisk)
            {
                config.StorageDirectory = Path.Combine(Path.GetTempPath(), "VerbexTest", name);
            }

            InvertedIndex index = new InvertedIndex(name, config);
            await index.OpenAsync().ConfigureAwait(false);
            return index;
        }

        /// <summary>
        /// Creates a VerbexConfiguration using the current test context settings.
        /// </summary>
        /// <returns>VerbexConfiguration for current test context.</returns>
        public static VerbexConfiguration CreateTestConfiguration()
        {
            VerbexConfiguration config = new VerbexConfiguration
            {
                StorageMode = _CurrentStorageMode
            };

            if (_CurrentStorageMode == StorageMode.OnDisk)
            {
                config.StorageDirectory = Path.Combine(Path.GetTempPath(), "VerbexTest", Guid.NewGuid().ToString("N"));
            }

            return config;
        }

        /// <summary>
        /// Cleans up test storage directory.
        /// </summary>
        /// <param name="directory">Directory to clean.</param>
        public static void CleanupTestDirectory(string? directory)
        {
            if (!ShouldCleanup)
            {
                return;
            }

            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Cleans up a test database file (for SQLite file mode).
        /// </summary>
        /// <param name="filePath">Path to the database file.</param>
        public static void CleanupTestDatabaseFile(string? filePath)
        {
            if (!ShouldCleanup)
            {
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Gets a description of the current database configuration for display.
        /// </summary>
        /// <returns>Description of the database configuration.</returns>
        public static string GetDatabaseDescription()
        {
            if (_Options == null)
            {
                return "Not configured";
            }

            switch (_Options.DatabaseType)
            {
                case TestDatabaseType.SqliteRam:
                    return "SQLite (in-memory)";

                case TestDatabaseType.Sqlite:
                    return $"SQLite (file: {_Options.Filename})";

                case TestDatabaseType.SqlServer:
                    string sqlSchema = string.IsNullOrEmpty(_Options.Schema) ? "dbo" : _Options.Schema;
                    return $"SQL Server ({_Options.Hostname}:{_Options.Port}/{_Options.DatabaseName}, schema: {sqlSchema})";

                case TestDatabaseType.Postgres:
                    string pgSchema = string.IsNullOrEmpty(_Options.Schema) ? "public" : _Options.Schema;
                    return $"PostgreSQL ({_Options.Hostname}:{_Options.Port}/{_Options.DatabaseName}, schema: {pgSchema})";

                case TestDatabaseType.Mysql:
                    return $"MySQL ({_Options.Hostname}:{_Options.Port}/{_Options.DatabaseName})";

                default:
                    return "Unknown";
            }
        }
    }
}
