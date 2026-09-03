# Verbex Telemetry

Verbex is fully instrumented for **metrics**, **traces**, and **logs** using the .NET
`System.Diagnostics.Metrics` (`Meter`) and `System.Diagnostics` (`ActivitySource`) primitives, exported
through [OpenTelemetry](https://opentelemetry.io/). Instrumentation covers every layer:

- **HTTP** — every REST API request (`Verbex.Server`)
- **MCP** — every tool invocation (`Verbex.Mcp`)
- **Application** — indexing, search, and batch operations (`Verbex` core library)
- **Runtime / process** — memory, threads, uptime, GC and .NET runtime counters

This document describes what is emitted, how to turn it on, and how to connect Verbex to an
observability stack — either the bundled one under `docker/` or your own (Prometheus, Grafana, Tempo,
Loki, Jaeger, Datadog, Honeycomb, Grafana Cloud, or any OTLP-compatible backend).

---

## 1. Architecture

Verbex follows the "emit rides the BCL, the app hosts" model:

- The **`Verbex` core library takes no telemetry dependency.** It creates a `Meter` and an
  `ActivitySource` both named **`Verbex.Core`** and records through them. Until something subscribes,
  every measurement is a cheap no-op — so the library stays lightweight for embedders who don't want
  telemetry.
- The **`Verbex.Server` and `Verbex.Mcp` executables host the pipeline.** Each references the
  [`Radiant`](https://www.nuget.org/packages/Radiant) telemetry SDK, which builds the OpenTelemetry
  `MeterProvider` / `TracerProvider` / logging pipeline from a small settings object, subscribes to
  both the host's own instruments **and** the `Verbex.Core` instruments, and exports over OTLP and/or
  an in-process Prometheus endpoint.

Because the server's HTTP server span is the ambient `Activity` while a request runs, the core
library's spans (e.g. `verbex.search`) nest correctly beneath it — one trace spans HTTP → search →
storage.

| Process | `service.name` | Meter / ActivitySource subscribed | Export |
|---|---|---|---|
| `verbex-server` | `verbex-server` | `verbex-server`, `Verbex.Core` | OTLP + in-process Prometheus (optional) |
| `verbex-mcp` | `verbex-mcp` | `verbex-mcp`, `Verbex.Core` | OTLP |

---

## 2. What is emitted

Metric names below are shown in **OpenTelemetry form**. When scraped through the bundled collector the
Prometheus names differ (dots become underscores, counters gain `_total`, histograms expand to
`_bucket`/`_sum`/`_count`, and units like `s`/`By` are appended). Both forms are listed for the HTTP
metrics; the same transformation applies throughout.

### 2.1 HTTP (service `verbex-server`)

Recorded centrally for **every** routed request (including 404s and HEAD), so coverage is complete.

| OTel instrument | Kind | Unit | Prometheus | Labels |
|---|---|---|---|---|
| `http.server.request.count` | Counter | `{request}` | `http_server_request_count_total` | `http_request_method`, `http_response_status_code`, `http_route` |
| `http.server.request.duration` | Histogram | `s` | `http_server_request_duration_seconds_*` | `http_request_method`, `http_response_status_code`, `http_route` |
| `http.server.active_requests` | UpDownCounter | `{request}` | `http_server_active_requests` | `http_request_method` |
| `http.server.request.body.size` | Histogram | `By` | `http_server_request_body_size_bytes_*` | `http_request_method` |
| `http.server.response.body.size` | Histogram | `By` | `http_server_response_body_size_bytes_*` | `http_request_method`, `http_response_status_code` |

The `http_route` label is the **normalized route template** (e.g. `/v1.0/indices/{id}`), not the raw
URL, keeping cardinality bounded. **Spans:** one server span per request named `GET /v1.0/indices/{id}`
etc., tagged with method, route, protocol, and status; exceptions are recorded on the span.

### 2.2 MCP (service `verbex-mcp`)

Recorded for every MCP tool invocation across all transports (stdio, HTTP, WebSocket).

| OTel instrument | Kind | Unit | Prometheus | Labels |
|---|---|---|---|---|
| `rpc.server.calls` | Counter | `{call}` | `rpc_server_calls_total` | `rpc_system` (=`mcp`), `rpc_method` (tool name), `outcome` |
| `rpc.server.duration` | Histogram | `s` | `rpc_server_duration_seconds_*` | `rpc_system`, `rpc_method` |

**Spans:** one server span per call named `mcp <tool>` (e.g. `mcp verbex_search`), tagged with system
and method; exceptions recorded.

### 2.3 Application (core library `Verbex.Core`)

Emitted by the core library and collected by whichever host process is running.

| OTel instrument | Kind | Unit | Prometheus | Labels |
|---|---|---|---|---|
| `verbex.documents.indexed` | Counter | `{document}` | `verbex_documents_indexed_total` | `verbex_index`, `outcome` |
| `verbex.documents.removed` | Counter | `{document}` | `verbex_documents_removed_total` | `verbex_index` |
| `verbex.terms.indexed` | Counter | `{term}` | `verbex_terms_indexed_total` | `verbex_index`, `outcome` |
| `verbex.index.document.duration` | Histogram | `s` | `verbex_index_document_duration_seconds_*` | `verbex_index`, `outcome` |
| `verbex.index.batch.duration` | Histogram | `s` | `verbex_index_batch_duration_seconds_*` | `verbex_index`, `verbex_operation`, `outcome` |
| `verbex.search.requests` | Counter | `{search}` | `verbex_search_requests_total` | `verbex_index`, `verbex_search_mode`, `outcome` |
| `verbex.search.duration` | Histogram | `s` | `verbex_search_duration_seconds_*` | `verbex_index`, `verbex_search_mode`, `outcome` |
| `verbex.search.results` | Histogram | `{document}` | `verbex_search_results_*` | `verbex_index`, `verbex_search_mode`, `outcome` |

`verbex_search_mode` is `and`, `or`, or `wildcard`. `verbex_operation` is `add` or `remove`.
**Spans:** `verbex.add_document`, `verbex.remove_document`, `verbex.search`, `verbex.batch_add`,
`verbex.batch_remove`, each tagged with the index name (and, for search, mode and result count).

### 2.4 Runtime / process (every service)

Emitted for every hosted process by the Radiant host and the OpenTelemetry runtime instrumentation:

- `process_memory_usage_bytes`, `process_uptime_seconds`, `process_thread_count`
- .NET runtime counters under `process_runtime_dotnet_*` (GC collections, heap size, allocations,
  thread pool, exceptions, JIT) — exact names depend on the `OpenTelemetry.Instrumentation.Runtime`
  version.

All metrics and traces carry the `service_name` (and `service_instance_id`) resource attributes, so a
single backend cleanly separates `verbex-server` from `verbex-mcp`.

---

## 3. Configuration

### 3.1 Server (`verbex.json` → `Telemetry`)

```json
"Telemetry": {
  "Enable": true,
  "ServiceName": "verbex-server",
  "ServiceInstanceId": null,
  "Otlp": {
    "Enable": true,
    "Endpoint": "http://localhost:4317",
    "Protocol": "grpc"
  },
  "Prometheus": {
    "Enable": true,
    "Hostname": "localhost",
    "Port": 9464,
    "Path": "/metrics"
  }
}
```

- **`Enable`** — master switch. When `false` the pipeline is inert and all instrumentation is a no-op.
- **`Otlp`** — push exporter to an OpenTelemetry Collector or OTLP backend. `Protocol` is `grpc`
  (port 4317) or `httpprotobuf` (port 4318).
- **`Prometheus`** — in-process scrape endpoint. Serves an OpenMetrics page at
  `http://<Hostname>:<Port><Path>` for Prometheus to scrape the process directly, useful without a
  collector. Bind `*` (or `+`) to expose it outside a container.

**Environment overrides** (take precedence over `verbex.json`; ideal for containers):

| Variable | Effect |
|---|---|
| `VERBEX_TELEMETRY_ENABLE` | `true`/`false` master switch |
| `VERBEX_OTLP_ENABLE` | enable/disable the OTLP exporter |
| `VERBEX_OTLP_ENDPOINT` | OTLP endpoint (e.g. `http://otel-collector:4317`) |
| `VERBEX_OTLP_PROTOCOL` | `grpc` or `httpprotobuf` |
| `VERBEX_PROMETHEUS_ENABLE` | enable/disable in-process Prometheus |
| `VERBEX_PROMETHEUS_HOSTNAME` | bind hostname (`*` for all interfaces) |
| `VERBEX_PROMETHEUS_PORT` | scrape port (default `9464`) |

### 3.2 MCP (environment only)

The MCP server is configured entirely by environment variables (it commonly runs as a stdio
subprocess):

| Variable | Default | Effect |
|---|---|---|
| `VERBEX_TELEMETRY_ENABLE` | `true` | master switch |
| `VERBEX_OTLP_ENDPOINT` | `http://localhost:4317` | OTLP endpoint |
| `VERBEX_OTLP_PROTOCOL` | `grpc` | `grpc` or `httpprotobuf` |

The MCP server pushes OTLP only (no in-process Prometheus). When running MCP over stdio under a client
that does not have a collector available, set `VERBEX_TELEMETRY_ENABLE=false` to silence export
attempts.

---

## 4. The bundled observability stack

`docker/compose.yaml` brings up the application **and** a complete observability stack:

```bash
cd docker
docker compose up -d
```

| Service | URL | Credentials |
|---|---|---|
| Grafana | http://localhost:3000 | `admin` / `admin` |
| Prometheus | http://localhost:9090 | none |
| Tempo (traces, via Grafana) | http://localhost:3200 | none |
| Loki (logs, via Grafana) | http://localhost:3100 | none |
| OTLP collector | gRPC `4317`, HTTP `4318`, Prometheus exposition `8889` | none |
| Verbex API | http://localhost:8080 (Swagger `/swagger`) | bearer token |
| Verbex dashboard | http://localhost:8200 | login |

These links (with credentials) are also surfaced inside the dashboard under the **Observability** view,
and are configurable via the dashboard's runtime env (`VERBEX_GRAFANA_URL`, `VERBEX_PROMETHEUS_URL`,
`VERBEX_TEMPO_URL`, `VERBEX_LOKI_URL`, `VERBEX_METRICS_URL`).

**Data flow:** `verbex-server`/`verbex-mcp` → OTLP → collector → Prometheus (metrics) / Tempo (traces)
/ Loki (logs) → Grafana. Prometheus scrapes the collector's `:8889` exposition endpoint (resource
attributes are promoted to labels), and the collector fans traces/logs to Tempo/Loki.

### Grafana dashboards

Dashboards are provisioned into a single top-level Grafana folder named **`Verbex`**, organized by
domain (no subfolders):

- **Verbex - HTTP** — request rate, latency percentiles, in-flight, errors, payload sizes
- **Verbex - Application** — indexing/search/batch rates, latencies, results, error ratios
- **Verbex - MCP** — tool call rate, latency, outcomes
- **Verbex - Runtime** — memory, threads, uptime, GC, allocations, exceptions

Each dashboard has a `service` template variable so you can scope to `verbex-server` or `verbex-mcp`.
Datasources (Prometheus, Tempo, Loki) are provisioned with metric↔trace↔log correlation:
metric exemplars jump to a trace, a trace jumps to its logs, and a log line jumps back to its trace.

> Note: `docker/compose.yaml` references prebuilt `verbex-server`/`verbex-dashboard` images. To emit
> telemetry from your own build, rebuild those images from the current source (the observability
> services themselves need no rebuild).

---

## 5. Connecting to your own observability stack (DevOps)

Verbex speaks standard OTLP, so any OpenTelemetry-compatible backend works. Two integration models:

### Model A — push OTLP to your collector (recommended)

Point Verbex at your existing OpenTelemetry Collector (or a vendor OTLP endpoint):

```bash
VERBEX_OTLP_ENDPOINT=http://otel-collector.observability.svc:4317
VERBEX_OTLP_PROTOCOL=grpc          # or httpprotobuf for :4318
VERBEX_PROMETHEUS_ENABLE=false      # collector handles metrics
```

For vendors requiring auth headers (Grafana Cloud, Honeycomb, Datadog OTLP, etc.), terminate/authorize
at a collector you control and forward from there. From the collector, route to your metrics store
(Prometheus/Mimir/Cortex), trace store (Tempo/Jaeger), and log store (Loki/Elasticsearch) as usual.

### Model B — scrape the in-process Prometheus endpoint

For a pull-only metrics setup with no collector, enable the in-process endpoint and scrape it:

```bash
VERBEX_PROMETHEUS_ENABLE=true
VERBEX_PROMETHEUS_HOSTNAME=*        # bind all interfaces (containers)
VERBEX_PROMETHEUS_PORT=9464
```

```yaml
# prometheus scrape_config
- job_name: verbex-server
  static_configs:
    - targets: ['verbex-server:9464']
```

Model B covers metrics only; use Model A (OTLP) if you also want traces and logs.

### DevOps notes

- **Resource attributes:** every signal carries `service.name` and `service.instance.id`. Set a stable
  instance id via `Telemetry.ServiceInstanceId` (server) when running multiple replicas so per-pod
  series are distinguishable; otherwise a per-process GUID is generated.
- **Cardinality:** metric labels are deliberately low-cardinality (method, status class, normalized
  route, index name, search mode, outcome). Document ids and query text are **not** used as metric
  labels — they belong on spans/logs. The `verbex_index` label scales with the number of indices in a
  deployment; if you run a very large number of indices and want to cap series, aggregate it away in
  recording rules.
- **Histograms:** duration histograms use OpenTelemetry's default latency buckets (5 ms–10 s), so
  `histogram_quantile()` works out of the box.
- **Sampling:** traces are head-sampled at ratio 1.0 (everything) by default via the Radiant host.
  Reduce trace volume at the collector (tail sampling) or in front of a vendor if needed.
- **Metric export cadence:** OTLP metrics export every 15 s by default; the in-process Prometheus
  endpoint is pull-based and unaffected.
- **Security:** the bundled stack has **no authentication** and is intended for local/dev use. Do not
  expose Prometheus/Tempo/Loki/collector ports publicly in production; put them behind your network
  policy, and change Grafana's `admin` password (`GF_SECURITY_ADMIN_PASSWORD`).
- **Graceful shutdown:** both hosts flush exporters on shutdown, so the final window of telemetry is
  not lost on a clean stop.

---

## 6. Prometheus naming reference

The collector's Prometheus exporter transforms OTel names as follows:

| OTel | Prometheus |
|---|---|
| `foo.bar.count` (Counter) | `foo_bar_count_total` |
| `foo.bar.duration` unit `s` (Histogram) | `foo_bar_duration_seconds_bucket` / `_sum` / `_count` |
| `foo.bar.size` unit `By` (Histogram) | `foo_bar_size_bytes_bucket` / `_sum` / `_count` |
| `foo.bar` (UpDownCounter) | `foo_bar` (gauge) |
| label `http.request.method` | `http_request_method` |
| resource `service.name` | `service_name` label |

Example queries:

```promql
# Request rate by status (last 5m)
sum by (http_response_status_code) (rate(http_server_request_count_total{service_name="verbex-server"}[5m]))

# p95 API latency
histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket[5m])))

# Search throughput by mode
sum by (verbex_search_mode) (rate(verbex_search_requests_total[5m]))

# MCP tool error ratio
sum(rate(rpc_server_calls_total{outcome="error"}[5m])) / sum(rate(rpc_server_calls_total[5m]))
```

---

## 7. Troubleshooting

- **No metrics in Prometheus:** confirm the target is UP at http://localhost:9090/targets. Metrics
  appear only after the first 15 s OTLP export and one scrape interval.
- **No traces in Tempo:** verify the collector received spans (collector logs) and that
  `Telemetry.Otlp.Enable` is true. Traces require activity — hit an API endpoint or run an MCP tool.
- **`http_server_*` metrics missing:** ensure the server build in your image includes this
  instrumentation (rebuild the image if using the bundled prebuilt tag).
- **Port already in use on 9464:** another process holds the in-process Prometheus port; change
  `Telemetry.Prometheus.Port` or disable it and rely on OTLP.
- **Noisy OTLP errors with no collector:** set `VERBEX_TELEMETRY_ENABLE=false` (or
  `VERBEX_OTLP_ENABLE=false`) when running without a backend.
