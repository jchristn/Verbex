namespace Verbex.Server.Services
{
    using System;
    using Radiant;
    using Verbex.Server.Classes;
    using Verbex.Telemetry;

    /// <summary>
    /// Owns the server's Radiant telemetry pipeline and exposes the emit helpers the REST handler uses
    /// to record HTTP metrics and spans. The pipeline subscribes to the server's own instruments and
    /// to the Verbex core-library instruments (<see cref="VerbexTelemetry.MeterName"/> /
    /// <see cref="VerbexTelemetry.ActivitySourceName"/>) so a single host exports HTTP, application,
    /// and runtime telemetry over OTLP and/or an in-process Prometheus endpoint.
    /// <para>
    /// Even when telemetry is disabled, a host is created in an inert state so the emit helpers remain
    /// safe no-ops and callers never branch on null.
    /// </para>
    /// <para>
    /// Thread safety: after <see cref="Start(TelemetrySettings)"/> returns, all members are safe to
    /// use concurrently.
    /// </para>
    /// </summary>
    public sealed class ServerTelemetry : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Whether telemetry export is enabled (a live pipeline was built). False when configuration
        /// disabled it, in which case the emit helpers are no-ops.
        /// </summary>
        public bool Enabled
        {
            get
            {
                return _Host.IsEnabled;
            }
        }

        /// <summary>
        /// The absolute Prometheus scrape URL when the in-process endpoint is enabled, otherwise null.
        /// </summary>
        public string? PrometheusScrapeUrl
        {
            get
            {
                return _PrometheusScrapeUrl;
            }
        }

        #endregion

        #region Private-Members

        /// <summary>
        /// Count of inbound HTTP server requests. A monotonic counter complementing the duration
        /// histogram so request rate is a simple <c>rate(http_server_request_count_total[..])</c>.
        /// </summary>
        private static readonly Convention _RequestCount =
            Convention.Counter("http.server.request.count", "{request}",
                    SemConv.Http.AttributeMethod, SemConv.Http.AttributeStatusCode, SemConv.Http.AttributeRoute)
                .WithDescription("Count of inbound HTTP server requests.");

        private static readonly Convention _RequestDuration =
            SemConv.Http.ServerRequestDuration.WithBuckets(LatencyBuckets.Default);

        private readonly RadiantHost _Host;
        private readonly string? _PrometheusScrapeUrl;

        #endregion

        #region Constructors-and-Factories

        private ServerTelemetry(RadiantHost host, string? prometheusScrapeUrl)
        {
            _Host = host;
            _PrometheusScrapeUrl = prometheusScrapeUrl;
        }

        /// <summary>
        /// Build and start the telemetry pipeline from the supplied settings.
        /// </summary>
        /// <param name="settings">The telemetry settings. Must be non-null.</param>
        /// <returns>A started <see cref="ServerTelemetry"/>. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public static ServerTelemetry Start(TelemetrySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            RadiantSettings radiant = new RadiantSettings(settings.ServiceName);
            radiant.Enable = settings.Enable;
            radiant.ServiceInstanceId = settings.ServiceInstanceId;

            radiant.Otlp.Enable = settings.Otlp.Enable;
            if (settings.Otlp.Enable)
            {
                radiant.Otlp.Endpoint = settings.Otlp.Endpoint;
                radiant.Otlp.Protocol = ParseProtocol(settings.Otlp.Protocol);
            }

            radiant.Prometheus.Enable = settings.Prometheus.Enable;
            radiant.Prometheus.Hostname = settings.Prometheus.Hostname;
            radiant.Prometheus.Port = settings.Prometheus.Port;
            radiant.Prometheus.Path = settings.Prometheus.Path;

            // Subscribe to the Verbex core-library instruments so application-layer telemetry
            // (indexing, search) flows through this same host, correctly nested under HTTP spans.
            radiant.Sources.AddMeter(VerbexTelemetry.MeterName);
            radiant.Sources.AddActivitySource(VerbexTelemetry.ActivitySourceName);

            // Declare the HTTP instruments so histogram buckets and label policy are applied.
            radiant.Metrics.DefineAll(
                _RequestDuration,
                _RequestCount,
                SemConv.Http.ServerActiveRequests,
                SemConv.Http.ServerRequestBodySize,
                SemConv.Http.ServerResponseBodySize);

            RadiantHost host = RadiantHost.Start(radiant);

            string? scrapeUrl = null;
            if (settings.Enable && settings.Prometheus.Enable)
            {
                scrapeUrl = radiant.Prometheus.ToScrapeUrl();
            }

            return new ServerTelemetry(host, scrapeUrl);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Record a completed HTTP request: increments the request counter, records the request
        /// duration, and records request/response body sizes.
        /// </summary>
        /// <param name="method">The HTTP method (for example <c>GET</c>).</param>
        /// <param name="statusCode">The HTTP response status code.</param>
        /// <param name="route">The low-cardinality route template (for example <c>/v1.0/indices/{id}</c>).</param>
        /// <param name="seconds">The request duration in seconds.</param>
        /// <param name="requestBytes">The request body size in bytes, or 0 if none.</param>
        /// <param name="responseBytes">The response body size in bytes, or 0 if none.</param>
        public void RecordHttpRequest(string method, int statusCode, string route, double seconds, long requestBytes, long responseBytes)
        {
            RadiantClient client = _Host.Client;

            RadiantTag methodTag = new RadiantTag(SemConv.Http.AttributeMethod, method);
            RadiantTag statusTag = new RadiantTag(SemConv.Http.AttributeStatusCode, statusCode);
            RadiantTag routeTag = new RadiantTag(SemConv.Http.AttributeRoute, route);

            client.Record(_RequestDuration, seconds, methodTag, statusTag, routeTag);
            client.Increment(_RequestCount, 1.0, methodTag, statusTag, routeTag);

            if (requestBytes > 0)
            {
                client.Record(SemConv.Http.ServerRequestBodySize, requestBytes, methodTag);
            }

            if (responseBytes > 0)
            {
                client.Record(SemConv.Http.ServerResponseBodySize, responseBytes, methodTag, statusTag);
            }
        }

        /// <summary>
        /// Adjust the in-flight request gauge by <paramref name="delta"/> (+1 on receive, -1 on
        /// completion).
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="delta">The delta to apply.</param>
        public void AdjustActiveRequests(string method, double delta)
        {
            _Host.Client.Add(SemConv.Http.ServerActiveRequests, delta, new RadiantTag(SemConv.Http.AttributeMethod, method));
        }

        /// <summary>
        /// Start an HTTP server span. Returns a span that is inert (but safe) when nothing is sampling.
        /// </summary>
        /// <param name="name">The span name. Must be non-null and non-empty.</param>
        /// <returns>A <see cref="RadiantSpan"/>. Never null.</returns>
        public RadiantSpan StartServerSpan(string name)
        {
            return _Host.StartSpan(name, SpanKindEnum.Server);
        }

        /// <summary>
        /// Force all exporters to flush pending telemetry.
        /// </summary>
        public void Flush()
        {
            _Host.ForceFlush();
        }

        /// <summary>
        /// Dispose the telemetry pipeline, flushing exporters and releasing the Prometheus port.
        /// </summary>
        public void Dispose()
        {
            _Host.Dispose();
        }

        #endregion

        #region Private-Methods

        private static OtlpProtocolEnum ParseProtocol(string protocol)
        {
            if (String.Equals(protocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                || String.Equals(protocol, "http/protobuf", StringComparison.OrdinalIgnoreCase)
                || String.Equals(protocol, "http", StringComparison.OrdinalIgnoreCase))
            {
                return OtlpProtocolEnum.HttpProtobuf;
            }

            return OtlpProtocolEnum.Grpc;
        }

        #endregion
    }
}
