namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// Telemetry (metrics, traces, logs) settings for the Verbex server. The server hosts a Radiant
    /// telemetry pipeline that subscribes to the server's own instruments and the Verbex core library
    /// instruments, then exports them over OTLP and/or an in-process Prometheus endpoint.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch for telemetry. Default true. When false, no telemetry pipeline is built and
        /// all instrumentation stays a no-op.
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
        /// The logical service name stamped as the <c>service.name</c> resource attribute. Default
        /// <c>verbex-server</c>.
        /// </summary>
        public string ServiceName
        {
            get
            {
                return _ServiceName;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(ServiceName));
                _ServiceName = value;
            }
        }

        /// <summary>
        /// The service instance identifier stamped as <c>service.instance.id</c>. When null or empty,
        /// a stable GUID is generated for the process lifetime.
        /// </summary>
        public string? ServiceInstanceId
        {
            get
            {
                return _ServiceInstanceId;
            }
            set
            {
                _ServiceInstanceId = value;
            }
        }

        /// <summary>
        /// OTLP push exporter settings.
        /// </summary>
        public TelemetryOtlpSettings Otlp
        {
            get
            {
                return _Otlp;
            }
            set
            {
                if (value == null) _Otlp = new TelemetryOtlpSettings();
                else _Otlp = value;
            }
        }

        /// <summary>
        /// In-process Prometheus scrape endpoint settings.
        /// </summary>
        public TelemetryPrometheusSettings Prometheus
        {
            get
            {
                return _Prometheus;
            }
            set
            {
                if (value == null) _Prometheus = new TelemetryPrometheusSettings();
                else _Prometheus = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Enable = true;
        private string _ServiceName = "verbex-server";
        private string? _ServiceInstanceId = null;
        private TelemetryOtlpSettings _Otlp = new TelemetryOtlpSettings();
        private TelemetryPrometheusSettings _Prometheus = new TelemetryPrometheusSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetrySettings()
        {

        }

        #endregion
    }
}
