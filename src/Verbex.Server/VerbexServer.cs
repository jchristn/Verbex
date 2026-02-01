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
        #region Private-Members

        private static readonly string _Header = "[VerbexServer] ";
        private static readonly int _ProcessId = Environment.ProcessId;

        private static Settings? _Settings = null;
        private static DatabaseDriverBase? _Database = null;
        private static AuthenticationService? _Authentication = null;
        private static IndexManager? _IndexManager = null;
        private static RestServiceHandler? _RestService = null;
        private static LoggingModule? _Logging = null;

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

                _RestService?.Start();
                _Logging?.Info(_Header + "started at " + DateTime.UtcNow + " using process ID " + _ProcessId);

                ManualResetEventSlim shutdownEvent = new ManualResetEventSlim(false);

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    _Logging?.Info(_Header + "shutdown signal received (Ctrl+C)");
                    shutdownEvent.Set();
                };

                AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
                {
                    _Logging?.Info(_Header + "process exit signal received");
                    shutdownEvent.Set();
                };

                shutdownEvent.Wait();

                _Logging?.Info(_Header + "stopping at " + DateTime.UtcNow);
                _RestService?.Stop();

                _Logging?.Info(_Header + "disposing indices...");
                if (_IndexManager != null)
                {
                    await _IndexManager.DisposeAllAsync().ConfigureAwait(false);
                }

                _Logging?.Info(_Header + "stopped at " + DateTime.UtcNow);
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
            Console.WriteLine("(c)2026 Joel Christner");
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

            _Settings = Classes.Settings.FromFile(settingsFile);

            // Environment variable overrides
            string? adminToken = Environment.GetEnvironmentVariable("VERBEX_ADMIN_TOKEN");
            if (!String.IsNullOrEmpty(adminToken)) _Settings.AdminBearerToken = adminToken;

            string? hostname = Environment.GetEnvironmentVariable("VERBEX_HOSTNAME");
            if (!String.IsNullOrEmpty(hostname)) _Settings.Rest.Hostname = hostname;

            string? port = Environment.GetEnvironmentVariable("VERBEX_PORT");
            if (!String.IsNullOrEmpty(port) && Int32.TryParse(port, out int portInt))
            {
                _Settings.Rest.Port = portInt;
            }
        }

        /// <summary>
        /// Initialize logging.
        /// </summary>
        private static void InitializeLogging()
        {
            if (_Settings == null) throw new InvalidOperationException("Settings must be initialized before logging");

            List<SyslogServer> syslogServers = new List<SyslogServer>();
            if (_Settings.Logging.SyslogServers != null && _Settings.Logging.SyslogServers.Count > 0)
            {
                foreach (SyslogServer server in _Settings.Logging.SyslogServers)
                {
                    syslogServers.Add(new SyslogServer(server.Hostname, server.Port));
                    Console.WriteLine("Adding syslog server: " + server.Hostname + ":" + server.Port);
                }
            }

            if (syslogServers.Count > 0)
                _Logging = new LoggingModule(syslogServers);
            else
                _Logging = new LoggingModule();

            _Logging.Settings.EnableConsole = _Settings.Logging.ConsoleLogging;
            _Logging.Settings.EnableColors = _Settings.Logging.EnableColors;
            _Logging.Settings.MinimumSeverity = (Severity)_Settings.Logging.MinimumSeverity;

            if (_Settings.Logging.FileLogging
                && !String.IsNullOrEmpty(_Settings.Logging.LogDirectory)
                && !String.IsNullOrEmpty(_Settings.Logging.LogFilename))
            {
                _Logging.Settings.LogFilename = Path.Combine(_Settings.Logging.LogDirectory, _Settings.Logging.LogFilename);

                if (_Settings.Logging.IncludeDateInFilename)
                {
                    _Logging.Settings.FileLogging = SyslogLogging.FileLoggingMode.FileWithDate;
                }
                else
                {
                    _Logging.Settings.FileLogging = SyslogLogging.FileLoggingMode.SingleLogFile;
                }
            }

            _Logging.Debug(_Header + "initialized logging");
        }

        /// <summary>
        /// Initialize globals.
        /// </summary>
        /// <returns>Task.</returns>
        private static async Task InitializeGlobalsAsync()
        {
            if (_Settings == null) throw new InvalidOperationException("Settings must be initialized before globals");

            // Initialize database driver
            _Database = await DatabaseDriverFactory.CreateAndInitializeAsync(_Settings.Database).ConfigureAwait(false);
            _Logging?.Info(_Header + "database driver initialized (" + _Settings.Database.Type + ")");

            _Authentication = new AuthenticationService(_Settings.AdminBearerToken, _Database);
            _IndexManager = new IndexManager(_Database, _Logging);
            _RestService = new RestServiceHandler(_Settings, _Authentication, _IndexManager, _Database, _Logging!);
        }

        /// <summary>
        /// Discover and load indices for all tenants from the database.
        /// </summary>
        /// <returns>Task.</returns>
        private static async Task DiscoverAllIndicesAsync()
        {
            if (_Database == null || _Settings == null || _IndexManager == null)
            {
                throw new InvalidOperationException("Database, Settings, and IndexManager must be initialized before discovering indices");
            }

            try
            {
                List<TenantMetadata> tenants = await _Database.Tenants.ReadManyAsync().ConfigureAwait(false);
                _Logging?.Info(_Header + "discovering indices for " + tenants.Count + " tenant(s)");

                foreach (TenantMetadata tenant in tenants)
                {
                    if (!tenant.Active)
                    {
                        _Logging?.Info(_Header + "skipping inactive tenant '" + tenant.Identifier + "'");
                        continue;
                    }

                    await _IndexManager.DiscoverIndicesAsync(tenant.Identifier, _Settings.DataDirectory).ConfigureAwait(false);
                }

                _Logging?.Info(_Header + "index discovery complete");
            }
            catch (Exception e)
            {
                _Logging?.Warn(_Header + "failed to discover indices: " + e.Message);
            }
        }

        /// <summary>
        /// Create default records if the database is empty.
        /// Creates a default tenant, user, credential, and index on first startup.
        /// </summary>
        /// <returns>Task.</returns>
        private static async Task CreateDefaultRecordsAsync()
        {
            if (_Database == null || _Settings == null || _IndexManager == null)
            {
                throw new InvalidOperationException("Database, Settings, and IndexManager must be initialized before creating default records");
            }

            try
            {
                // Check if any tenants exist
                List<TenantMetadata> existingTenants = await _Database.Tenants.ReadManyAsync().ConfigureAwait(false);
                if (existingTenants.Count > 0)
                {
                    _Logging?.Info(_Header + "database already has records, skipping default record creation");
                    return;
                }

                _Logging?.Info(_Header + "creating default records for initial setup");

                // Create default tenant
                TenantMetadata defaultTenant = new TenantMetadata("Default Tenant")
                {
                    Identifier = "default",
                    Description = "Default tenant created during initial setup",
                    Active = true
                };
                await _Database.Tenants.CreateAsync(defaultTenant).ConfigureAwait(false);
                _Logging?.Info(_Header + "created tenant: " + defaultTenant.Identifier);

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
                await _Database.Users.CreateAsync(defaultUser).ConfigureAwait(false);
                _Logging?.Info(_Header + "created default user: " + defaultUser.Email);

                // Create default credential with bearer token "default"
                Credential defaultCredential = new Credential("default", "default", "Default API Key")
                {
                    Identifier = "default",
                    TenantId = defaultTenant.Identifier,
                    BearerToken = "default",
                    Active = true
                };
                await _Database.Credentials.CreateAsync(defaultCredential).ConfigureAwait(false);
                _Logging?.Info(_Header + "created credential with bearer token: " + defaultCredential.BearerToken);

                // Create default index
                IndexMetadata defaultIndex = new IndexMetadata(
                    defaultTenant.Identifier, 
                    "Default Index", 
                    "Default index created during initial setup")
                {
                    Identifier = "default",
                    Enabled = true,
                    InMemory = false
                };
                IndexMetadata createdIndex = await _IndexManager.CreateIndexAsync(defaultIndex).ConfigureAwait(false);
                _Logging?.Info(_Header + "created index: " + createdIndex.Identifier);

                _Logging?.Info(_Header + "default records created successfully");
            }
            catch (Exception e)
            {
                _Logging?.Warn(_Header + "failed to create default records: " + e.Message);
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
            string msg = "";
            if (e != null && e.InnerException != null) msg = e.InnerException.Message;
            else if (e != null) msg = e.Message;

            _Logging?.Error(_Header + "[" + method + "] Exception: " + text + ": " + msg);
        }

        #endregion
    }
}