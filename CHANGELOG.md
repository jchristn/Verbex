# Change Log

## Current Version

v0.2.2

- Updated NuGet dependencies: Caching 5.0.1 -> 5.1.2, Microsoft.Data.SqlClient 7.1.0 -> 7.1.1, Watson 7.2.0 -> 7.2.2, SyslogLogging 2.2.2 -> 2.3.1, Timestamps 1.0.12 -> 1.0.13, Voltaic 2.0.0 -> 2.2.1, NUnit 4.6.1 -> 5.0.0, and Touchstone.Core / Touchstone.Cli / Touchstone.XunitAdapter / Touchstone.NunitAdapter 0.1.12 -> 0.2.0.
- Voltaic 2.2.1 changes visible to MCP clients:
  - A tool call whose arguments fail the input schema (for example a non-string `tags` value) now returns a tool result with `isError: true` naming the problem, instead of a JSON-RPC `-32602` error. The tool still never runs.
  - A second `initialize` on an initialized session is rejected with `-32600`. Voltaic clients initialize automatically on connect.
  - The stateless `2026-07-28` revision has no `ping`; it returns `-32601` there. `ping` still returns `{}` on the handshake revisions.
  - Voltaic now emits its own metrics and traces on a `Meter` and `ActivitySource` named `Voltaic`.
- `Test.Mcp` now checks the stored handshake result on every transport, asserts that a second `initialize` is rejected, expects `isError` results for invalid tag values, and expects `-32601` for a stateless `ping`.
- Upgraded Voltaic to 2.0.0. The MCP server now publishes only the 13 Verbex tools: Voltaic's demo tools (`ping`, `echo`, `getTime`, `getSessions`, `getClients`) are gone from `tools/list` and `tools/call`. The protocol `ping` returns `{}` instead of `"pong"`, tools are callable only through `tools/call` (a bare tool-name call returns `-32601`), and tool argument schemas are enforced more strictly: a non-string `tags` value is now rejected instead of being silently dropped (as an `isError` result since Voltaic 2.1; see above).
- `Test.Mcp` now asserts that `tools/list` contains exactly the Verbex tools on every transport, that `ping` returns an empty result on every handshake transport, that demo tools and bare tool-name calls are rejected, and that non-string tag values are rejected on `verbex_add_document`, `verbex_add_tags`, and `verbex_search`.
- Upgraded Voltaic to 1.1.0. MCP clients on the stateless `2026-07-28` revision, such as Claude Code 2.1.x, now see the Verbex tools over Streamable HTTP. Also updated Microsoft.Data.Sqlite 10.0.12, Microsoft.Data.SqlClient 7.1.0, Watson 7.2.0, SyslogLogging 2.2.2, System.CommandLine 2.0.12, Microsoft.NET.Test.Sdk 18.10.1, and NUnit3TestAdapter 6.3.0.
- Fixed the MCP server's stdio and WebSocket transports exposing no Verbex tools. Tools were registered as raw methods, so `tools/list` omitted them and `tools/call` returned "Tool not found". All transports now register one shared tool set.
- Fixed a path traversal in the MCP server: index names flowed straight into filesystem paths, so `verbex_delete_index` with a name like `../..` could recursively delete directories outside the storage root. Index names are now validated on every tool.
- MCP tool failures are now returned with `isError: true`, and unexpected exceptions become `isError` results instead of JSON-RPC errors.
- Fixed a race where concurrent MCP calls for an index that was not yet open could open it twice.
- Fixed `IndexStatistics.PostingCount` and `AverageTermsPerDocument` always being 0 on every database driver.
- Added `VERBEX_MCP_STORAGE_DIR` to override the MCP index storage directory.
- Rebuilt the `Test.Mcp` harness. It previously failed to start the server (no `--framework`) and ignored connection failures. It now covers all 13 tools, error results, path traversal, concurrency, and the stdio, WebSocket, handshake Streamable HTTP, and stateless Streamable HTTP transports.
- `Verbex` and `Verbex.Sdk` NuGet packages aligned at 0.2.2 and published with symbol packages. The Docker images were rebuilt in place under the existing `v0.2.1` and `latest` tags.

## Previous Versions

### v0.2.1

- Added MCP client installation: `verbex-mcp --install` / `--uninstall` register the Verbex MCP server in Claude Code, Cursor, Codex, the Gemini CLI, and Mux, plus standalone per-agent scripts under `scripts/` for Windows, macOS, and Linux. Added [MCP_API.md](MCP_API.md) documenting the full MCP tool surface, transports, and installation.
- Added built-in observability: OpenTelemetry-based metrics and distributed tracing across the HTTP API, the MCP server, and the core library (indexing, search, batch operations), exported over OTLP and/or an in-process Prometheus endpoint. See [TELEMETRY.md](TELEMETRY.md).
- Added a Docker observability stack (OpenTelemetry Collector, Prometheus, Tempo, Loki, Grafana) to `docker/compose.yaml`, with provisioned Grafana datasources and dashboards in a top-level "Verbex" folder organized by domain (HTTP, Application, MCP, Runtime).
- Added an "Observability" view to the dashboard that links out to Grafana, Prometheus, Tempo, Loki, Swagger, and the metrics endpoint (name, default credentials, and URL).
- `Verbex` and `Verbex.Sdk` NuGet packages aligned at 0.2.1 and published with symbol packages; Docker images tagged `v0.2.1`.

### v0.1.x

- Initial alpha release
- Added opt-in search result enrichment for REST, SDKs, MCP, CLI, and dashboard clients. Default search responses remain unchanged; matched terms and term details reuse existing scoring data, and document term stats use one grouped query.
