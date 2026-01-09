namespace Verbex.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using SyslogLogging;
    using Verbex.Database;
    using Verbex.Database.Interfaces;
    using Verbex.Models;
    using Verbex.Server.Classes;
    using Verbex.Server.Services;
    using Verbex.Server.API.REST;

    /// <summary>
    /// Verbex server.
    /// </summary>
    public static class VerbexServer
    {
        #region Public-Members

        /// <summary>
        /// Settings.
        /// </summary>
        public static Settings? Settings = null;

        /// <summary>
        /// Database driver for multi-tenant data storage.
        /// </summary>
        public static DatabaseDriverBase? Database = null;

        /// <summary>
        /// Authentication service.
        /// </summary>
        public static AuthenticationService? Authentication = null;

        /// <summary>
        /// Index manager.
        /// </summary>
        public static IndexManager? IndexManager = null;

        /// <summary>
        /// REST service handler.
        /// </summary>
        public static RestServiceHandler? RestService = null;

        /// <summary>
        /// Logging module.
        /// </summary>
        public static LoggingModule? Logging = null;

        #endregion

        #region Private-Members

        private static readonly string _Header = "[VerbexServer] ";
        private static readonly int _ProcessId = Environment.ProcessId;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code (0 for success, 1 for failure).</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                Welcome();
                ParseArguments(args);
                InitializeSettings();
                InitializeLogging();
                await InitializeGlobalsAsync().ConfigureAwait(false);
                await CreateDefaultRecordsAsync().ConfigureAwait(false);
                await DiscoverAllIndicesAsync().ConfigureAwait(false);

                RestService?.Start();
                Logging?.Info(_Header + "started at " + DateTime.UtcNow + " using process ID " + _ProcessId);

                ManualResetEventSlim shutdownEvent = new ManualResetEventSlim(false);

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    Logging?.Info(_Header + "shutdown signal received (Ctrl+C)");
                    shutdownEvent.Set();
                };

                AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
                {
                    Logging?.Info(_Header + "process exit signal received");
                    shutdownEvent.Set();
                };

                shutdownEvent.Wait();

                Logging?.Info(_Header + "stopping at " + DateTime.UtcNow);
                RestService?.Stop();

                Logging?.Info(_Header + "disposing indices...");
                if (IndexManager != null)
                {
                    await IndexManager.DisposeAllAsync().ConfigureAwait(false);
                }

                Logging?.Info(_Header + "stopped at " + DateTime.UtcNow);
                return 0;
            }
            catch (Exception e)
            {
                ExceptionConsole("Main", "Fatal startup exception", e);
                return 1;
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Welcome message.
        /// </summary>
        private static void Welcome()
        {
            Console.WriteLine(Constants.Logo);
            Console.WriteLine("(c) 2025 Joel Christner");
            Console.WriteLine("");
        }

        /// <summary>
        /// Parse arguments.
        /// </summary>
        /// <param name="args">Arguments.</param>
        private static void ParseArguments(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (String.IsNullOrEmpty(args[i])) continue;
                    // Parse arguments here if needed
                }
            }
        }

        /// <summary>
        /// Initialize settings.
        /// </summary>
        private static void InitializeSettings()
        {
            string settingsFile = "verbex.json";

            // Check for environment variable override
            string? envSettingsFile = Environment.GetEnvironmentVariable("VERBEX_SETTINGS_FILE");
            if (!String.IsNullOrEmpty(envSettingsFile)) settingsFile = envSettingsFile;

            Settings = Classes.Settings.FromFile(settingsFile);

            // Environment variable overrides
            string? adminToken = Environment.GetEnvironmentVariable("VERBEX_ADMIN_TOKEN");
            if (!String.IsNullOrEmpty(adminToken)) Settings.AdminBearerToken = adminToken;

            string? hostname = Environment.GetEnvironmentVariable("VERBEX_HOSTNAME");
            if (!String.IsNullOrEmpty(hostname)) Settings.Rest.Hostname = hostname;

            string? port = Environment.GetEnvironmentVariable("VERBEX_PORT");
            if (!String.IsNullOrEmpty(port) && Int32.TryParse(port, out int portInt))
            {
                Settings.Rest.Port = portInt;
            }
        }

        /// <summary>
        /// Initialize logging.
        /// </summary>
        private static void InitializeLogging()
        {
            if (Settings == null) throw new InvalidOperationException("Settings must be initialized before logging");

            Logging = new LoggingModule();
            Logging.Settings.EnableConsole = Settings.Logging.ConsoleLogging;
            Logging.Settings.EnableColors = Settings.Logging.EnableColors;

            if (Settings.Logging.FileLogging)
            {
                if (Settings.Logging.IncludeDateInFilename)
                {
                    Logging.Settings.FileLogging = SyslogLogging.FileLoggingMode.FileWithDate;
                }
                else
                {
                    Logging.Settings.FileLogging = SyslogLogging.FileLoggingMode.SingleLogFile;
                }
            }
        }

        /// <summary>
        /// Initialize globals.
        /// </summary>
        /// <returns>Task.</returns>
        private static async Task InitializeGlobalsAsync()
        {
            if (Settings == null) throw new InvalidOperationException("Settings must be initialized before globals");

            // Initialize database driver
            Database = await DatabaseDriverFactory.CreateAndInitializeAsync(Settings.Database).ConfigureAwait(false);
            Logging?.Info(_Header + "database driver initialized (" + Settings.Database.Type + ")");

            Authentication = new AuthenticationService(Settings.AdminBearerToken, Database);
            IndexManager = new IndexManager(Database, Logging);
            RestService = new RestServiceHandler(Settings, Authentication, IndexManager, Database, Logging!);
        }

        /// <summary>
        /// Discover and load indices for all tenants from the database.
        /// </summary>
        /// <returns>Task.</returns>
        private static async Task DiscoverAllIndicesAsync()
        {
            if (Database == null || Settings == null || IndexManager == null)
            {
                throw new InvalidOperationException("Database, Settings, and IndexManager must be initialized before discovering indices");
            }

            try
            {
                List<TenantMetadata> tenants = await Database.Tenants.ReadManyAsync().ConfigureAwait(false);
                Logging?.Info(_Header + "discovering indices for " + tenants.Count + " tenant(s)");

                foreach (TenantMetadata tenant in tenants)
                {
                    if (!tenant.Active)
                    {
                        Logging?.Info(_Header + "skipping inactive tenant '" + tenant.Identifier + "'");
                        continue;
                    }

                    await IndexManager.DiscoverIndicesAsync(tenant.Identifier, Settings.DataDirectory).ConfigureAwait(false);
                }

                Logging?.Info(_Header + "index discovery complete");
            }
            catch (Exception e)
            {
                Logging?.Warn(_Header + "failed to discover indices: " + e.Message);
            }
        }

        /// <summary>
        /// Create default records if the database is empty.
        /// Creates a default tenant, user, credential, and index on first startup.
        /// </summary>
        /// <returns>Task.</returns>
        private static async Task CreateDefaultRecordsAsync()
        {
            if (Database == null || Settings == null || IndexManager == null)
            {
                throw new InvalidOperationException("Database, Settings, and IndexManager must be initialized before creating default records");
            }

            try
            {
                // Check if any tenants exist
                List<TenantMetadata> existingTenants = await Database.Tenants.ReadManyAsync().ConfigureAwait(false);
                if (existingTenants.Count > 0)
                {
                    Logging?.Info(_Header + "database already has records, skipping default record creation");
                    return;
                }

                Logging?.Info(_Header + "creating default records for initial setup");

                // Create default tenant
                TenantMetadata defaultTenant = new TenantMetadata("Default Tenant")
                {
                    Identifier = "default",
                    Description = "Default tenant created during initial setup",
                    Active = true
                };
                await Database.Tenants.CreateAsync(defaultTenant).ConfigureAwait(false);
                Logging?.Info(_Header + "created default tenant: " + defaultTenant.Identifier);

                // Create default user
                UserMaster defaultUser = new UserMaster("default", "default@user.com")
                {
                    Identifier = "default",
                    TenantId = defaultTenant.Identifier,
                    FirstName = "Default",
                    LastName = "User",
                    IsAdmin = true,
                    Active = true
                };
                defaultUser.SetPassword("password");
                await Database.Users.CreateAsync(defaultUser).ConfigureAwait(false);
                Logging?.Info(_Header + "created default user: " + defaultUser.Email);

                // Create default credential with bearer token "default"
                Credential defaultCredential = new Credential("default", "default", "Default API Key")
                {
                    Identifier = "default",
                    TenantId = defaultTenant.Identifier,
                    BearerToken = "default",
                    Active = true
                };
                await Database.Credentials.CreateAsync(defaultCredential).ConfigureAwait(false);
                Logging?.Info(_Header + "created default credential with bearer token: default");

                // Create default index
                IndexMetadata defaultIndex = new IndexMetadata(
                    defaultTenant.Identifier, 
                    "Default Index", 
                    "Default index created during initial setup")
                {
                    Enabled = true,
                    InMemory = false
                };
                IndexMetadata createdIndex = await IndexManager.CreateIndexAsync(defaultIndex).ConfigureAwait(false);
                Logging?.Info(_Header + "created default index: " + createdIndex.Identifier);

                Logging?.Info(_Header + "default records created successfully");
            }
            catch (Exception e)
            {
                Logging?.Warn(_Header + "failed to create default records: " + e.Message);
                // Don't throw - allow server to continue even if default record creation fails
            }
        }

        /// <summary>
        /// Exception console.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="text">Text.</param>
        /// <param name="e">Exception.</param>
        private static void ExceptionConsole(string method, string text, Exception e)
        {
            var msg = "";
            if (e != null && e.InnerException != null) msg = e.InnerException.Message;
            else if (e != null) msg = e.Message;

            Logging?.Error(_Header + "[" + method + "] Exception: " + text + ": " + msg);
        }

        #endregion
    }
}