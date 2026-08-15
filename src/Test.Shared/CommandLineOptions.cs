namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Supported database types for testing.
    /// </summary>
    public enum TestDatabaseType
    {
        /// <summary>
        /// SQLite file-based database.
        /// </summary>
        Sqlite,

        /// <summary>
        /// SQLite in-memory database.
        /// </summary>
        SqliteRam,

        /// <summary>
        /// Microsoft SQL Server database.
        /// </summary>
        SqlServer,

        /// <summary>
        /// PostgreSQL database.
        /// </summary>
        Postgres,

        /// <summary>
        /// MySQL database.
        /// </summary>
        Mysql
    }

    /// <summary>
    /// Parses and holds command-line options for the test application.
    /// </summary>
    public class CommandLineOptions
    {
        private TestDatabaseType _DatabaseType = TestDatabaseType.SqliteRam;
        private string _Filename = string.Empty;
        private string _Hostname = string.Empty;
        private int _Port = 0;
        private string _Username = string.Empty;
        private string _Password = string.Empty;
        private string _DatabaseName = string.Empty;
        private string _Schema = string.Empty;
        private bool _NoCleanup = false;
        private bool _ShowHelp = false;
        private string _ErrorMessage = string.Empty;

        /// <summary>
        /// Gets the database type to use for tests.
        /// </summary>
        public TestDatabaseType DatabaseType => _DatabaseType;

        /// <summary>
        /// Gets the filename for SQLite file-based database.
        /// </summary>
        public string Filename => _Filename;

        /// <summary>
        /// Gets the hostname for server-based databases.
        /// </summary>
        public string Hostname => _Hostname;

        /// <summary>
        /// Gets the port for server-based databases.
        /// </summary>
        public int Port => _Port;

        /// <summary>
        /// Gets the username for server-based databases.
        /// </summary>
        public string Username => _Username;

        /// <summary>
        /// Gets the password for server-based databases.
        /// </summary>
        public string Password => _Password;

        /// <summary>
        /// Gets the database name for server-based databases.
        /// </summary>
        public string DatabaseName => _DatabaseName;

        /// <summary>
        /// Gets the schema for SQL Server and PostgreSQL databases.
        /// </summary>
        public string Schema => _Schema;

        /// <summary>
        /// Gets whether to skip cleanup of test records.
        /// </summary>
        public bool NoCleanup => _NoCleanup;

        /// <summary>
        /// Gets whether help was requested.
        /// </summary>
        public bool ShowHelp => _ShowHelp;

        /// <summary>
        /// Gets the error message if parsing failed.
        /// </summary>
        public string ErrorMessage => _ErrorMessage;

        /// <summary>
        /// Gets whether parsing was successful.
        /// </summary>
        public bool IsValid => string.IsNullOrEmpty(_ErrorMessage);

        /// <summary>
        /// Parses command-line arguments into options.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Parsed command-line options.</returns>
        public static CommandLineOptions Parse(string[] args)
        {
            CommandLineOptions options = new CommandLineOptions();

            if (args == null || args.Length == 0)
            {
                options._ErrorMessage = "No database type specified.";
                return options;
            }

            Dictionary<string, string> argDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> flags = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-?", StringComparison.OrdinalIgnoreCase))
                {
                    options._ShowHelp = true;
                    return options;
                }

                if (arg.Equals("--no-cleanup", StringComparison.OrdinalIgnoreCase))
                {
                    flags.Add("no-cleanup");
                    continue;
                }

                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    string key = arg.Substring(2).ToLowerInvariant();
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        argDict[key] = args[i + 1];
                        i++;
                    }
                    else
                    {
                        options._ErrorMessage = $"Missing value for argument: {arg}";
                        return options;
                    }
                }
            }

            options._NoCleanup = flags.Contains("no-cleanup");

            // Parse database type
            if (!argDict.TryGetValue("db", out string? dbType) || string.IsNullOrEmpty(dbType))
            {
                options._ErrorMessage = "Missing required argument: --db <type>";
                return options;
            }

            switch (dbType.ToLowerInvariant())
            {
                case "sqlite":
                    options._DatabaseType = TestDatabaseType.Sqlite;
                    break;
                case "sqlite-ram":
                    options._DatabaseType = TestDatabaseType.SqliteRam;
                    break;
                case "sqlserver":
                    options._DatabaseType = TestDatabaseType.SqlServer;
                    break;
                case "postgres":
                    options._DatabaseType = TestDatabaseType.Postgres;
                    break;
                case "mysql":
                    options._DatabaseType = TestDatabaseType.Mysql;
                    break;
                default:
                    options._ErrorMessage = $"Unknown database type: {dbType}. Valid types are: sqlite, sqlite-ram, sqlserver, postgres, mysql";
                    return options;
            }

            // Parse database-specific arguments
            switch (options._DatabaseType)
            {
                case TestDatabaseType.Sqlite:
                    if (!argDict.TryGetValue("filename", out string? filename) || string.IsNullOrEmpty(filename))
                    {
                        options._ErrorMessage = "SQLite file mode requires --filename <path>";
                        return options;
                    }
                    options._Filename = filename;
                    break;

                case TestDatabaseType.SqliteRam:
                    // No additional arguments required
                    break;

                case TestDatabaseType.SqlServer:
                case TestDatabaseType.Postgres:
                    if (!argDict.TryGetValue("hostname", out string? host1) || string.IsNullOrEmpty(host1))
                    {
                        options._ErrorMessage = $"{options._DatabaseType} requires --hostname <host>";
                        return options;
                    }
                    options._Hostname = host1;

                    if (!argDict.TryGetValue("port", out string? portStr1) || string.IsNullOrEmpty(portStr1))
                    {
                        options._ErrorMessage = $"{options._DatabaseType} requires --port <port>";
                        return options;
                    }
                    if (!int.TryParse(portStr1, out int port1) || port1 <= 0 || port1 > 65535)
                    {
                        options._ErrorMessage = $"Invalid port number: {portStr1}";
                        return options;
                    }
                    options._Port = port1;

                    if (!argDict.TryGetValue("user", out string? user1) || string.IsNullOrEmpty(user1))
                    {
                        options._ErrorMessage = $"{options._DatabaseType} requires --user <username>";
                        return options;
                    }
                    options._Username = user1;

                    if (!argDict.TryGetValue("pass", out string? pass1) || string.IsNullOrEmpty(pass1))
                    {
                        options._ErrorMessage = $"{options._DatabaseType} requires --pass <password>";
                        return options;
                    }
                    options._Password = pass1;

                    if (!argDict.TryGetValue("dbname", out string? dbname1) || string.IsNullOrEmpty(dbname1))
                    {
                        options._ErrorMessage = $"{options._DatabaseType} requires --dbname <database>";
                        return options;
                    }
                    options._DatabaseName = dbname1;

                    if (argDict.TryGetValue("schema", out string? schema1) && !string.IsNullOrEmpty(schema1))
                    {
                        options._Schema = schema1;
                    }
                    break;

                case TestDatabaseType.Mysql:
                    if (!argDict.TryGetValue("hostname", out string? host2) || string.IsNullOrEmpty(host2))
                    {
                        options._ErrorMessage = "MySQL requires --hostname <host>";
                        return options;
                    }
                    options._Hostname = host2;

                    if (!argDict.TryGetValue("port", out string? portStr2) || string.IsNullOrEmpty(portStr2))
                    {
                        options._ErrorMessage = "MySQL requires --port <port>";
                        return options;
                    }
                    if (!int.TryParse(portStr2, out int port2) || port2 <= 0 || port2 > 65535)
                    {
                        options._ErrorMessage = $"Invalid port number: {portStr2}";
                        return options;
                    }
                    options._Port = port2;

                    if (!argDict.TryGetValue("user", out string? user2) || string.IsNullOrEmpty(user2))
                    {
                        options._ErrorMessage = "MySQL requires --user <username>";
                        return options;
                    }
                    options._Username = user2;

                    if (!argDict.TryGetValue("pass", out string? pass2) || string.IsNullOrEmpty(pass2))
                    {
                        options._ErrorMessage = "MySQL requires --pass <password>";
                        return options;
                    }
                    options._Password = pass2;

                    if (!argDict.TryGetValue("dbname", out string? dbname2) || string.IsNullOrEmpty(dbname2))
                    {
                        options._ErrorMessage = "MySQL requires --dbname <database>";
                        return options;
                    }
                    options._DatabaseName = dbname2;
                    break;
            }

            return options;
        }

        /// <summary>
        /// Gets the usage text for command-line help.
        /// </summary>
        /// <returns>Usage text.</returns>
        public static string GetUsageText()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Verbex Test Suite");
            sb.AppendLine("=================");
            sb.AppendLine();
            sb.AppendLine("Usage: Test --db <type> [options]");
            sb.AppendLine();
            sb.AppendLine("Required:");
            sb.AppendLine("  --db <type>           Database type to use for tests");
            sb.AppendLine("                        Valid types: sqlite, sqlite-ram, sqlserver, postgres, mysql");
            sb.AppendLine();
            sb.AppendLine("SQLite Options (--db sqlite):");
            sb.AppendLine("  --filename <path>     Path to the SQLite database file (required)");
            sb.AppendLine();
            sb.AppendLine("SQLite In-Memory Options (--db sqlite-ram):");
            sb.AppendLine("  (no additional options required)");
            sb.AppendLine();
            sb.AppendLine("SQL Server Options (--db sqlserver):");
            sb.AppendLine("  --hostname <host>     Database server hostname (required)");
            sb.AppendLine("  --port <port>         Database server port (required, typically 1433)");
            sb.AppendLine("  --user <username>     Database username (required)");
            sb.AppendLine("  --pass <password>     Database password (required)");
            sb.AppendLine("  --dbname <database>   Database name (required)");
            sb.AppendLine("  --schema <schema>     Schema name (optional, defaults to 'dbo')");
            sb.AppendLine();
            sb.AppendLine("PostgreSQL Options (--db postgres):");
            sb.AppendLine("  --hostname <host>     Database server hostname (required)");
            sb.AppendLine("  --port <port>         Database server port (required, typically 5432)");
            sb.AppendLine("  --user <username>     Database username (required)");
            sb.AppendLine("  --pass <password>     Database password (required)");
            sb.AppendLine("  --dbname <database>   Database name (required)");
            sb.AppendLine("  --schema <schema>     Schema name (optional, defaults to 'public')");
            sb.AppendLine();
            sb.AppendLine("MySQL Options (--db mysql):");
            sb.AppendLine("  --hostname <host>     Database server hostname (required)");
            sb.AppendLine("  --port <port>         Database server port (required, typically 3306)");
            sb.AppendLine("  --user <username>     Database username (required)");
            sb.AppendLine("  --pass <password>     Database password (required)");
            sb.AppendLine("  --dbname <database>   Database name (required)");
            sb.AppendLine();
            sb.AppendLine("General Options:");
            sb.AppendLine("  --no-cleanup          Do not clean up test records after tests complete");
            sb.AppendLine("  --help, -h, -?        Show this help message");
            sb.AppendLine();
            sb.AppendLine("Examples:");
            sb.AppendLine();
            sb.AppendLine("  SQLite in-memory (fastest, no persistence):");
            sb.AppendLine("    Test --db sqlite-ram");
            sb.AppendLine();
            sb.AppendLine("  SQLite file-based:");
            sb.AppendLine("    Test --db sqlite --filename ./test.db");
            sb.AppendLine();
            sb.AppendLine("  SQL Server:");
            sb.AppendLine("    Test --db sqlserver --hostname localhost --port 1433 \\");
            sb.AppendLine("         --user sa --pass YourPassword123 --dbname verbex_test --schema dbo");
            sb.AppendLine();
            sb.AppendLine("  PostgreSQL:");
            sb.AppendLine("    Test --db postgres --hostname localhost --port 5432 \\");
            sb.AppendLine("         --user postgres --pass YourPassword123 --dbname verbex_test --schema public");
            sb.AppendLine();
            sb.AppendLine("  MySQL:");
            sb.AppendLine("    Test --db mysql --hostname localhost --port 3306 \\");
            sb.AppendLine("         --user root --pass YourPassword123 --dbname verbex_test");
            sb.AppendLine();
            sb.AppendLine("  With no cleanup (keep test records):");
            sb.AppendLine("    Test --db sqlite-ram --no-cleanup");

            return sb.ToString();
        }
    }
}
