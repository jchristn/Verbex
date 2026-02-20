#!/bin/sh
cat <<EOF > /usr/share/nginx/html/env-config.js
window.__ENV__ = {
  VERBEX_SERVER_URL: "${VERBEX_SERVER_URL:-http://verbex-server:8080}",
  VERBEX_API_KEY: "${VERBEX_API_KEY:-verbexadmin}"
};
EOF
exec "$@"
