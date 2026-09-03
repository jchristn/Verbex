#!/usr/bin/env sh
# Connect Mux to the Verbex MCP server by adding a 'verbex' entry to Mux's mcp-servers.json.
# The Verbex MCP server requires no authentication.
# Usage: sh install-mux.sh
# Override the endpoint with VERBEX_MCP_BASE_URL and the config path with VERBEX_MUX_CONFIG.
set -e

BASE_URL="${VERBEX_MCP_BASE_URL:-http://127.0.0.1:8200}"
CONFIG="${VERBEX_MUX_CONFIG:-$HOME/.mux/mcp-servers.json}"

command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" "$BASE_URL" <<'PY'
import json, os, sys
path, url = sys.argv[1], sys.argv[2]
d = os.path.dirname(path)
if d and not os.path.isdir(d):
    os.makedirs(d, exist_ok=True)
try:
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    if not isinstance(cfg, dict):
        cfg = {}
except Exception:
    cfg = {}
servers = cfg.get("servers")
if not isinstance(servers, list):
    servers = []
servers = [s for s in servers if not (isinstance(s, dict) and s.get("name") == "verbex")]
servers.append({"name": "verbex", "transport": "http", "url": url, "mcpPath": "/mcp"})
cfg["servers"] = servers
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
print("Added 'verbex' to " + path)
PY
echo "Point Mux at this file with --mcp-config, or add it to your Mux config directory, then restart Mux."
