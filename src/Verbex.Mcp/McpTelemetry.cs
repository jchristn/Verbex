namespace Verbex.Mcp
{
    using System;
    using System.Diagnostics;
    using Radiant;
    using Verbex.Telemetry;
    using Voltaic.Core;

    /// <summary>
    /// Hosts the Verbex MCP server's Radiant telemetry pipeline and instruments MCP tool invocations.
    /// The pipeline subscribes to the MCP server's own instruments and the Verbex core-library
    /// instruments, then pushes metrics, traces, and logs over OTLP. Each tool call is measured with an
    /// RPC server span, a duration histogram, and a call counter tagged by tool name and outcome.
    /// <para>
    /// Thread safety: <see cref="Start(bool, string, string)"/> and <see cref="Shutdown"/> are the
    /// boundary operations; after start, <see cref="Invoke(string, RpcParameters, Func{RpcParameters, object})"/>
    /// is safe to call concurrently from any thread.
    /// </para>
    /// </summary>
    public static class McpTelemetry
    {
        #region Public-Members

        /// <summary>
        /// Whether a live telemetry pipeline is running.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                RadiantHost? host = _Host;
                return host != null && host.IsEnabled;
            }
        }

        #endregion

        #region Private-Members

        private const string RpcSystem = "mcp";

        private static readonly Convention _Duration =
            SemConv.Rpc.ServerDuration.WithBuckets(LatencyBuckets.Default);

        private static readonly Convention _Calls =
            Convention.Counter("rpc.server.calls", "{call}",
                    SemConv.Rpc.AttributeSystem, SemConv.Rpc.AttributeMethod, "outcome")
                .WithDescription("MCP tool invocations by tool and outcome.");

        private static RadiantHost? _Host;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build and start the telemetry pipeline.
        /// </summary>
        /// <param name="enable">Whether telemetry export is enabled.</param>
        /// <param name="otlpEndpoint">The OTLP collector endpoint (for example <c>http://localhost:4317</c>).</param>
        /// <param name="protocol">The OTLP protocol: <c>grpc</c> or <c>httpprotobuf</c>.</param>
        public static void Start(bool enable, string otlpEndpoint, string protocol)
        {
            RadiantSettings radiant = new RadiantSettings("verbex-mcp");
            radiant.Enable = enable;

            radiant.Otlp.Enable = true;
            if (!String.IsNullOrEmpty(otlpEndpoint)) radiant.Otlp.Endpoint = otlpEndpoint;
            radiant.Otlp.Protocol = ParseProtocol(protocol);

            // The MCP server does not serve an in-process Prometheus endpoint; multiple short-lived
            // stdio instances would contend for the port. Metrics reach Prometheus via the collector.
            radiant.Prometheus.Enable = false;

            radiant.Sources.AddMeter(VerbexTelemetry.MeterName);
            radiant.Sources.AddActivitySource(VerbexTelemetry.ActivitySourceName);

            radiant.Metrics.DefineAll(_Duration, _Calls);

            _Host = RadiantHost.Start(radiant);
        }

        /// <summary>
        /// Invoke an MCP tool handler with telemetry: an RPC server span plus a duration histogram and
        /// a call counter tagged by tool and outcome. When telemetry is not started, the handler is
        /// invoked directly.
        /// </summary>
        /// <param name="tool">The MCP tool/method name (for example <c>verbex_search</c>).</param>
        /// <param name="args">The RPC parameters passed to the handler.</param>
        /// <param name="handler">The underlying tool handler. Must be non-null.</param>
        /// <returns>The handler result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        public static object Invoke(string tool, RpcParameters? args, Func<RpcParameters, object> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            RadiantHost? host = _Host;
            if (host == null) return handler(args!);

            using RadiantSpan span = host.StartSpan("mcp " + tool, SpanKindEnum.Server);
            span.SetTag(SemConv.Rpc.AttributeSystem, RpcSystem);
            span.SetTag(SemConv.Rpc.AttributeMethod, tool);

            long startTicks = Stopwatch.GetTimestamp();
            bool success = true;

            try
            {
                object result = handler(args!);
                span.SetOk();
                return result;
            }
            catch (Exception e)
            {
                success = false;
                span.RecordException(e);
                throw;
            }
            finally
            {
                double seconds = (Stopwatch.GetTimestamp() - startTicks) / (double)Stopwatch.Frequency;
                RadiantTag systemTag = new RadiantTag(SemConv.Rpc.AttributeSystem, RpcSystem);
                RadiantTag methodTag = new RadiantTag(SemConv.Rpc.AttributeMethod, tool);

                host.Client.Record(_Duration, seconds, systemTag, methodTag);
                host.Client.Increment(_Calls, 1.0, systemTag, methodTag, new RadiantTag("outcome", success ? "ok" : "error"));
            }
        }

        /// <summary>
        /// Force all exporters to flush pending telemetry.
        /// </summary>
        public static void Flush()
        {
            _Host?.ForceFlush();
        }

        /// <summary>
        /// Dispose the telemetry pipeline.
        /// </summary>
        public static void Shutdown()
        {
            RadiantHost? host = _Host;
            _Host = null;
            host?.Dispose();
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
