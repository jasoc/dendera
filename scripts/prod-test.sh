#!/bin/bash
# Builds the image from this checkout and runs it exactly as production does
# (docker-compose.yml: one container, API + built frontend, SQLite in the `dendera-data`
# volume) on http://localhost:8080 — the closest local approximation to a real deployment.
# Its data lives in the volume, never in the dev database (src/backend/Dendera.Api/dendera.db).
#
# Usage: ./scripts/prod-test.sh [up|down]  (default: up)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=lib/docker.sh
source "$ROOT/scripts/lib/docker.sh"

dendera_ensure_docker
cd "$ROOT"

case "${1:-up}" in
    up)
        docker compose up --build
        ;;
    down)
        docker compose down
        ;;
    *)
        echo "usage: $0 [up|down]"
        exit 1
        ;;
esac
