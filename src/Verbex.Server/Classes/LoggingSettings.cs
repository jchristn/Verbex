namespace Verbex.Server.Classes
{
    using SyslogLogging;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        #region Public-Members

        /// <summary>
        /// Syslog servers.
        /// </summary>
        public List<SyslogServer> SyslogServers
        {
            get
            {
                return _SyslogServers;
            }
            set
            {
                if (value == null) _SyslogServers = new List<SyslogServer>();
                else _SyslogServers = value;
            }
        }

        /// <summary>
        /// Enable console logging.
        /// </summary>
        public bool ConsoleLogging
        {
            get
            {
                return _ConsoleLogging;
            }
            set
            {
                _ConsoleLogging = value;
            }
        }

        /// <summary>
        /// Enable colors in console output.
        /// </summary>
        public bool EnableColors
        {
            get
            {
                return _EnableColors;
            }
            set
            {
                _EnableColors = value;
            }
        }

        /// <summary>
        /// Minimum severity level, 0 through 7.
        /// Default is 0.
        /// Invalid values are converted to 0.
        /// </summary>
        public int MinimumSeverity
        {
            get => _MinimumSeverity;
            set => _MinimumSeverity = (value >= 0 && value <= 7) ? value : 0;
        }

        /// <summary>
        /// Log directory.
        /// </summary>
        public string LogDirectory
        {
            get
            {
                return _LogDirectory;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(LogDirectory));
                _LogDirectory = value;
            }
        }

        /// <summary>
        /// Log filename.
        /// </summary>
        public string LogFilename
        {
            get
            {
                return _LogFilename;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(LogFilename));
                _LogFilename = value;
            }
        }

        /// <summary>
        /// File logging enabled.
        /// </summary>
        public bool FileLogging
        {
            get
            {
                return _FileLogging;
            }
            set
            {
                _FileLogging = value;
            }
        }

        /// <summary>
        /// Include date in filename.
        /// </summary>
        public bool IncludeDateInFilename
        {
            get
            {
                return _IncludeDateInFilename;
            }
            set
            {
                _IncludeDateInFilename = value;
            }
        }

        #endregion

        #region Private-Members

        private List<SyslogServer> _SyslogServers = new List<SyslogServer>
        {
            new SyslogServer("127.0.0.1", 514)
        };
        private bool _ConsoleLogging = true;
        private bool _EnableColors = true;
        private int _MinimumSeverity = 0;
        private string _LogDirectory = "logs";
        private string _LogFilename = "verbex.log";
        private bool _FileLogging = true;
        private bool _IncludeDateInFilename = true;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LoggingSettings()
        {

        }

        #endregion
    }
}