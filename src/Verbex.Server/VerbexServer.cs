namespace Verbex.Server
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using SyslogLogging;
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
        public static void Main(string[] args)
        {
            try
            {
                Welcome();
                ParseArguments(args);
                InitializeSettings();
                InitializeLogging();
                InitializeGlobals();
                CreateDefaultRecords();

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
                IndexManager?.DisposeAllAsync().GetAwaiter().GetResult();

                Logging?.Info(_Header + "stopped at " + DateTime.UtcNow);
            }
            catch (Exception e)
            {
                ExceptionConsole("Main", "Fatal startup exception", e);
                Environment.Exit(1);
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
        private static void InitializeGlobals()
        {
            if (Settings == null) throw new InvalidOperationException("Settings must be initialized before globals");

            Authentication = new AuthenticationService(Settings.AdminBearerToken);
            IndexManager = new IndexManager(Logging);
            IndexManager.DiscoverIndicesAsync(Settings.DataDirectory).GetAwaiter().GetResult();
            RestService = new RestServiceHandler(Settings, Authentication, IndexManager, Logging!);
        }

        /// <summary>
        /// Create default records.
        /// </summary>
        private static void CreateDefaultRecords()
        {
            // Add default record creation logic here
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