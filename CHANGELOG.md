# Change Log

## Current Version

v0.2.1

- Added MCP client installation: `verbex-mcp --install` / `--uninstall` register the Verbex MCP server in Claude Code, Cursor, Codex, the Gemini CLI, and Mux, plus standalone per-agent scripts under `scripts/` for Windows, macOS, and Linux. Added [MCP_API.md](MCP_API.md) documenting the full MCP tool surface, transports, and installation.
- Added built-in observability: OpenTelemetry-based metrics and distributed tracing across the HTTP API, the MCP server, and the core library (indexing, search, batch operations), exported over OTLP and/or an in-process Prometheus endpoint. See [TELEMETRY.md](TELEMETRY.md).
- Added a Docker observability stack (OpenTelemetry Collector, Prometheus, Tempo, Loki, Grafana) to `docker/compose.yaml`, with provisioned Grafana datasources and dashboards in a top-level "Verbex" folder organized by domain (HTTP, Application, MCP, Runtime).
- Added an "Observability" view to the dashboard that links out to Grafana, Prometheus, Tempo, Loki, Swagger, and the metrics endpoint (name, default credentials, and URL).
- `Verbex` and `Verbex.Sdk` NuGet packages aligned at 0.2.1 and published with symbol packages; Docker images tagged `v0.2.1`.

## Previous Versions

### v0.1.x

- Initial alpha release
- Added opt-in search result enrichment for REST, SDKs, MCP, CLI, and dashboard clients. Default search responses remain unchanged; matched terms and term details reuse existing scoring data, and document term stats use one grouped query.
