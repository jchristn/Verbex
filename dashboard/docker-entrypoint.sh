#!/bin/sh
cat <<EOF > /usr/share/nginx/html/env-config.js
window.__ENV__ = {
  VERBEX_SERVER_URL: "${VERBEX_SERVER_URL:-http://verbex-server:8080}",
  VERBEX_API_KEY: "${VERBEX_API_KEY:-verbexadmin}",
  VERBEX_GRAFANA_URL: "${VERBEX_GRAFANA_URL:-http://localhost:3000}",
  VERBEX_PROMETHEUS_URL: "${VERBEX_PROMETHEUS_URL:-http://localhost:9090}",
  VERBEX_TEMPO_URL: "${VERBEX_TEMPO_URL:-http://localhost:3200}",
  VERBEX_LOKI_URL: "${VERBEX_LOKI_URL:-http://localhost:3100}",
  VERBEX_METRICS_URL: "${VERBEX_METRICS_URL:-http://localhost:9464/metrics}"
};
EOF
exec "$@"
