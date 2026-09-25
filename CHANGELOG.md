# Change Log

## Unreleased

- Upgraded Voltaic to 1.1.0. MCP clients on the stateless `2026-07-28` revision, such as Claude Code 2.1.x, now see the Verbex tools over Streamable HTTP. Also updated Microsoft.Data.Sqlite 10.0.12, Microsoft.Data.SqlClient 7.1.0, Watson 7.2.0, SyslogLogging 2.2.2, System.CommandLine 2.0.12, Microsoft.NET.Test.Sdk 18.10.1, and NUnit3TestAdapter 6.3.0.
- Fixed the MCP server's stdio and WebSocket transports exposing no Verbex tools. Tools were registered as raw methods, so `tools/list` omitted them and `tools/call` returned "Tool not found". All transports now register one shared tool set.
- Fixed a path traversal in the MCP server: index names flowed straight into filesystem paths, so `verbex_delete_index` with a name like `../..` could recursively delete directories outside the storage root. Index names are now validated on every tool.
- MCP tool failures are now returned with `isError: true`, and unexpected exceptions become `isError` results instead of JSON-RPC errors.
- Fixed a race where concurrent MCP calls for an index that was not yet open could open it twice.
- Fixed `IndexStatistics.PostingCount` and `AverageTermsPerDocument` always being 0 on every database driver.
- Added `VERBEX_MCP_STORAGE_DIR` to override the MCP index storage directory.
- Rebuilt the `Test.Mcp` harness. It previously failed to start the server (no `--framework`) and ignored connection failures. It now covers all 13 tools, error results, path traversal, concurrency, and the stdio, WebSocket, handshake Streamable HTTP, and stateless Streamable HTTP transports.

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
