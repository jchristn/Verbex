# Docker Deployment Guide

This guide covers running Verbex with Docker.

## Quick Start

```bash
cd docker
docker compose up -d
```

This starts:
- **Verbex Server** at http://localhost:8080
- **Dashboard** at http://localhost:8200

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

To change ports, edit the compose file:

```yaml
ports:
  - "9000:8080"  # Host:Container
```

## Building Images

### Build Server Image

```bash
cd src
docker build -t jchristn/verbex-server:v0.1.0 -f Verbex.Server/Dockerfile .
```

### Build Dashboard Image

```bash
cd dashboard
docker build -t jchristn/verbex-dashboard:v0.1.0 .
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
