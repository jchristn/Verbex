namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// In-process Prometheus scrape endpoint settings. When enabled, the server binds an HTTP listener
    /// serving a Prometheus exposition endpoint so a Prometheus server can scrape the process directly,
    /// making metrics useful even without a collector deployed.
    /// </summary>
    public class TelemetryPrometheusSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the in-process scrape endpoint is enabled. Default true.
        /// </summary>
        public bool Enable
        {
            get
            {
                return _Enable;
            }
            set
            {
                _Enable = value;
            }
        }

        /// <summary>
        /// The hostname to bind. Default <c>localhost</c>. Use <c>+</c> or <c>*</c> to bind all
        /// interfaces (required inside a container so Prometheus can scrape it).
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// The TCP port to bind. Default 9464 (the OpenTelemetry Prometheus convention). Valid range 1 to 65535.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// The scrape path. Default <c>/metrics</c>. Must begin with <c>/</c>.
        /// </summary>
        public string Path
        {
            get
            {
                return _Path;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Path));
                _Path = value.StartsWith("/", StringComparison.Ordinal) ? value : "/" + value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Enable = true;
        private string _Hostname = "localhost";
        private int _Port = 9464;
        private string _Path = "/metrics";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryPrometheusSettings()
        {

        }

        #endregion
    }
}
