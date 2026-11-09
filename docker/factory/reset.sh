#!/usr/bin/env bash
#
# reset.sh - Reset Verbex Docker deployment to factory defaults
#
# This script:
#   1. Prompts the user to confirm by typing RESET
#   2. Stops and removes all Verbex containers via docker compose down
#   3. Restores the gold-copy config and database from the factory directory
#   4. Clears all log files and transient data
#

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "========================================"
echo "  Verbex Docker Factory Reset"
echo "========================================"
echo ""
echo "WARNING: This will destroy all current data and restore factory defaults."
echo "  - All indices, documents, users, and credentials will be lost"
echo "  - All log files will be deleted"
echo "  - Configuration will be reset to defaults"
echo ""
read -r -p "Type RESET to confirm: " confirmation

if [ "$confirmation" != "RESET" ]; then
    echo ""
    echo "Reset cancelled."
    exit 1
fi

echo ""
echo "[1/5] Stopping containers..."

# Try all compose files that might be running
for compose_file in compose.yaml compose-server.yaml compose-dashboard.yaml; do
    if [ -f "$DOCKER_DIR/$compose_file" ]; then
        docker compose -f "$DOCKER_DIR/$compose_file" down 2>/dev/null || true
    fi
done

echo "[2/5] Restoring factory configuration..."

# Restore server config
cp "$SCRIPT_DIR/verbex.json" "$DOCKER_DIR/server/verbex.json"
echo "  Restored: server/verbex.json"

echo "[3/5] Restoring factory database..."

# Remove existing database and WAL files
rm -f "$DOCKER_DIR/server/db/verbex.db"
rm -f "$DOCKER_DIR/server/db/verbex.db-wal"
rm -f "$DOCKER_DIR/server/db/verbex.db-shm"

# Copy gold-copy database
cp "$SCRIPT_DIR/verbex.db" "$DOCKER_DIR/server/db/verbex.db"
echo "  Restored: server/db/verbex.db"

echo "[4/5] Clearing data directory..."

# Remove all index data files (subdirectories and files in data/)
# but preserve the directory itself and .gitkeep
find "$DOCKER_DIR/server/data" -mindepth 1 ! -name '.gitkeep' -exec rm -rf {} + 2>/dev/null || true
echo "  Cleared:  server/data/"

echo "[5/5] Deleting log files..."

# Server logs - delete all log files including in subdirectories
find "$DOCKER_DIR/server/logs" -mindepth 1 ! -name '.gitkeep' -exec rm -rf {} + 2>/dev/null || true
echo "  Cleared:  server/logs/"

# Dashboard logs - delete all log files including in subdirectories
find "$DOCKER_DIR/dashboard/logs" -mindepth 1 ! -name '.gitkeep' -exec rm -rf {} + 2>/dev/null || true
echo "  Cleared:  dashboard/logs/"

echo ""
echo "========================================"
echo "  Factory reset complete."
echo "========================================"
echo ""
echo "Default credentials:"
echo "  Email:        default@user.com"
echo "  Password:     password"
echo "  Bearer Token: default"
echo "  Admin Token:  verbexadmin"
echo ""
echo "Run 'docker compose up -d' from the docker directory to start."
