#!/usr/bin/env sh
# Connect Codex to the Verbex MCP server by writing an [mcp_servers.verbex] table to ~/.codex/config.toml.
# The Verbex MCP server requires no authentication.
# Usage: sh install-codex.sh
# Override with VERBEX_MCP_URL / VERBEX_CODEX_CONFIG.
set -e

URL="${VERBEX_MCP_URL:-http://127.0.0.1:8200/mcp}"
CONFIG="${VERBEX_CODEX_CONFIG:-$HOME/.codex/config.toml}"

command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" "$URL" <<'PY'
import json, os, re, sys
path, url = sys.argv[1], sys.argv[2]
d = os.path.dirname(path)
if d and not os.path.isdir(d):
    os.makedirs(d, exist_ok=True)
try:
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
except FileNotFoundError:
    lines = []
# Drop any existing [mcp_servers.verbex] table and its sub-tables; keep everything else verbatim.
out, skipping = [], False
for line in lines:
    m = re.match(r"\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$", line)
    if m:
        name = m.group(1).replace('"', "")
        skipping = name == "mcp_servers.verbex" or name.startswith("mcp_servers.verbex.")
    if not skipping:
        out.append(line)
while out and not out[-1].strip():
    out.pop()
if out:
    out.append("")
out += ["[mcp_servers.verbex]",
        "url = " + json.dumps(url)]
with open(path, "w", encoding="utf-8") as f:
    f.write("\n".join(out) + "\n")
print("Added 'verbex' to " + path)
PY
echo "Restart Codex to pick up the change."
