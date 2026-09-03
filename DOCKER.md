# Docker Deployment Guide

This guide covers running Verbex with Docker.

## Quick Start

```bash
cd docker
docker compose up -d
```

This starts the application **and** a full observability stack:
- **Verbex Server** at http://localhost:8080 (Swagger at `/swagger`)
- **Dashboard** at http://localhost:8200
- **Grafana** at http://localhost:3000 (`admin` / `admin`)
- **Prometheus** at http://localhost:9090
- **Tempo** (traces, via Grafana) at http://localhost:3200
- **Loki** (logs, via Grafana) at http://localhost:3100
- **OpenTelemetry Collector** (OTLP gRPC 4317 / HTTP 4318, Prometheus exposition 8889)

See [TELEMETRY.md](TELEMETRY.md) for the full telemetry reference. The dashboard's
**Observability** view links out to each service with its URL and default credentials.

## Compose Files

| File | Description |
|------|-------------|
| `compose.yaml` | Full stack (server + dashboard) |
| `compose-server.yaml` | Server only |
| `compose-dashboard.yaml` | Dashboard only |

### Server Only

```bash
docker compose -f compose-server.yaml up -d
```

### Dashboard Only

```bash
docker compose -f compose-dashboard.yaml up -d
```

## Configuration

### Server Configuration

The server is configured via `docker/server/verbex.json`:

```json
{
  "Logging": {
    "ConsoleLogging": true,
    "LogDirectory": "./logs",
    "LogFilename": "verbex.log",
    "FileLogging": true
  },
  "Rest": {
    "Hostname": "*",
    "Port": 8080,
    "Ssl": false
  },
  "DataDirectory": "./data",
  "AdminBearerToken": "verbexadmin"
}
```

**Important**: Change `AdminBearerToken` for production deployments.

The config also contains a `Telemetry` block controlling metrics/traces export (OTLP to the
collector and, optionally, an in-process Prometheus endpoint). In the bundled stack it points at
`http://otel-collector:4317`. See [TELEMETRY.md](TELEMETRY.md) for all options and environment
overrides (`VERBEX_OTLP_ENDPOINT`, `VERBEX_TELEMETRY_ENABLE`, etc.).

### Volumes

The compose files mount these directories:

| Path | Purpose |
|------|---------|
| `./server/data` | Index data (persistent) |
| `./server/logs` | Server logs |
| `./server/verbex.json` | Configuration file |
| `./dashboard/logs` | Dashboard logs |

### Ports

| Service | Port |
|---------|------|
| Server | 8080 |
| Dashboard | 8200 |
| Grafana | 3000 |
| Prometheus | 9090 |
| Tempo | 3200 |
| Loki | 3100 |
| OTLP collector (gRPC / HTTP / Prometheus) | 4317 / 4318 / 8889 |

To change ports, edit the compose file:

```yaml
ports:
  - "9000:8080"  # Host:Container
```

## Observability

`compose.yaml` runs a complete observability stack alongside the application. The server and MCP
server push OpenTelemetry data to the collector; Prometheus scrapes the collector; Grafana reads
Prometheus (metrics), Tempo (traces), and Loki (logs).

| Service | URL | Credentials |
|---------|-----|-------------|
| Grafana | http://localhost:3000 | `admin` / `admin` |
| Prometheus | http://localhost:9090 | none |
| Tempo (via Grafana) | http://localhost:3200 | none |
| Loki (via Grafana) | http://localhost:3100 | none |

Grafana dashboards are provisioned into a top-level **Verbex** folder, organized by domain:
**Verbex - HTTP**, **Verbex - Application**, **Verbex - MCP**, and **Verbex - Runtime**.
Datasources are wired for metric↔trace↔log correlation. Configuration lives under
`docker/observability/` (collector, Prometheus, Tempo, Loki) and `docker/grafana/` (provisioning +
dashboards). Full details are in [TELEMETRY.md](TELEMETRY.md).

## Building Images

### Build Server Image

```bash
cd src
docker build -t jchristn77/verbex-server:v0.2.1 -f Verbex.Server/Dockerfile .
```

### Build Dashboard Image

```bash
cd dashboard
docker build -t jchristn77/verbex-dashboard:v0.2.1 .
```

## Production Considerations

### Persistent Storage

Ensure the data directory is mounted to preserve indices across container restarts:

```yaml
volumes:
  - /host/path/data:/app/data
```

### SSL/TLS

For HTTPS, configure in `verbex.json`:

```json
{
  "Rest": {
    "Ssl": true,
    "SslCertificateFile": "/app/certs/cert.pfx",
    "SslCertificatePassword": "your-password"
  }
}
```

Mount your certificate:

```yaml
volumes:
  - ./certs:/app/certs:ro
```

### Authentication

Change the default admin token:

```json
{
  "AdminBearerToken": "your-secure-token-here"
}
```

### Resource Limits

Add resource constraints:

```yaml
services:
  verbex-server:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
```

## Logs

View logs:

```bash
docker compose logs -f verbex-server
docker compose logs -f verbex-dashboard
```

## Stopping

```bash
docker compose down
```

To also remove volumes:

```bash
docker compose down -v
```

## Troubleshooting

### Container Won't Start

Check logs:
```bash
docker compose logs verbex-server
```

Verify configuration file syntax:
```bash
cat docker/server/verbex.json | python -m json.tool
```

### Can't Connect to Server

Verify the container is running:
```bash
docker compose ps
```

Check port bindings:
```bash
docker port verbex-server
```

### Data Not Persisting

Ensure the data directory exists and has correct permissions:
```bash
mkdir -p docker/server/data
chmod 755 docker/server/data
```
