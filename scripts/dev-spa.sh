#!/bin/bash
# Runs the Angular dev server (fast rebuild/HMR loop) on http://localhost:4200. Its
# proxy.conf.json forwards /api to Dendera.Api on :5180 — start ./scripts/dev-server.sh first
# (or use ./scripts/run.sh to get the whole stack under process-compose). Requires the Node
# pinned in src/frontend/.node-version (installed for you when nvm is present).
# Usage: ./scripts/dev-spa.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SPA_DIR="$ROOT/src/frontend"
# shellcheck source=lib/node.sh
source "$ROOT/scripts/lib/node.sh"

dendera_ensure_node "$SPA_DIR"

cd "$SPA_DIR"
if [ ! -d node_modules ]; then
    echo "==> Installing SPA dependencies (npm ci)"
    npm ci
fi

HOST="${DENDERA_HOST:-localhost}"
echo "==> Starting the Angular dev server on http://$HOST:4200"
# DENDERA_HOST=0.0.0.0 serves on every interface (other devices on the network).
exec npx ng serve --host "$HOST"
