namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// OTLP (OpenTelemetry Protocol) push exporter settings. When enabled, the server pushes metrics,
    /// traces, and logs to an OpenTelemetry Collector (or any OTLP-compatible endpoint such as Tempo
    /// or Loki's OTLP receiver).
    /// </summary>
    public class TelemetryOtlpSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the OTLP push exporter is enabled. Default true.
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
        /// The collector endpoint. Default <c>http://localhost:4317</c> (the gRPC port). Use
        /// <c>http://localhost:4318</c> with protocol <c>httpprotobuf</c>. Must be a non-empty absolute URI.
        /// </summary>
        public string Endpoint
        {
            get
            {
                return _Endpoint;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Endpoint));
                _Endpoint = value;
            }
        }

        /// <summary>
        /// The OTLP wire protocol: <c>grpc</c> (default, port 4317) or <c>httpprotobuf</c> (port 4318).
        /// </summary>
        public string Protocol
        {
            get
            {
                return _Protocol;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Protocol));
                _Protocol = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Enable = true;
        private string _Endpoint = "http://localhost:4317";
        private string _Protocol = "grpc";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryOtlpSettings()
        {

        }

        #endregion
    }
}
