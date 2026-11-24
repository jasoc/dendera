#!/bin/bash
# Local dev: runs Dendera.Api and the Angular dev server under process-compose
# (process-compose.yaml at the repo root), with a TUI showing each one's status and its own
# scrollable log. Linux / WSL (the Windows build of process-compose has no detached mode).
#
# The stack runs detached from the TUI: quitting the TUI leaves everything running, and
# re-running this script reattaches. `./scripts/run.sh --stop` stops the whole stack
# (the SQLite database, src/backend/Dendera.Api/dendera.db, stays). The only file this can create on its own is a root .env, copied from
# .env.example on first run.
#
# It installs no tools except the pinned Node, via nvm when nvm is present: it checks that
# process-compose, the .NET SDK pinned in global.json and the Node pinned in
# src/frontend/.node-version are available, and stops with a pointer to the README's Prerequisites if one isn't.
#
# Usage: ./scripts/run.sh [--stop | --background]
#   --background  start detached without the TUI (for scripts and remote sessions)
#   DENDERA_HOST=0.0.0.0 (environment or .env) serves on every interface instead of localhost.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Outside the checkout: a unix socket can't be created on a Windows drive (/mnt/c under WSL).
SOCKET="${XDG_RUNTIME_DIR:-/tmp}/dendera-process-compose.sock"

# shellcheck source=lib/env.sh
source "$ROOT/scripts/lib/env.sh"
# shellcheck source=lib/dotnet.sh
source "$ROOT/scripts/lib/dotnet.sh"
# shellcheck source=lib/node.sh
source "$ROOT/scripts/lib/node.sh"
# shellcheck source=lib/process-compose.sh
source "$ROOT/scripts/lib/process-compose.sh"

dendera_stack_running() {
    [ -S "$SOCKET" ] && process-compose list -U -u "$SOCKET" >/dev/null 2>&1
}

dendera_ensure_process_compose
mkdir -p "$ROOT/.run"

if [ "${1:-}" = "--stop" ]; then
    if dendera_stack_running; then
        process-compose down -U -u "$SOCKET"
        echo "==> Stopped the dev stack (api, SPA)."
    else
        echo "==> No dev stack running."
    fi
    exit 0
fi

if dendera_stack_running; then
    echo "==> Dev stack already running — attaching"
    exec process-compose attach -U -u "$SOCKET"
fi
# A socket left behind by a crashed run would make the new server fail to bind.
rm -f "$SOCKET"

dendera_load_env "$ROOT"

echo "== Toolchain =="
dendera_ensure_dotnet "$ROOT"
dendera_ensure_node "$ROOT/src/frontend"

cat <<EOF2

==> Starting the dev stack (the API migrates its SQLite database on first boot):
      API     http://${DENDERA_HOST:-localhost}:5180
      UI dev  http://${DENDERA_HOST:-localhost}:4200  (fast rebuild loop, proxies /api to :5180)
    Quitting the TUI leaves the stack running; re-run this script to reattach,
    ./scripts/run.sh --stop to stop everything.

EOF2

cd "$ROOT"
if [ "${1:-}" = "--background" ]; then
    exec process-compose up -f process-compose.yaml -U -u "$SOCKET" --detached
fi
exec process-compose up -f process-compose.yaml -U -u "$SOCKET" --detached-with-tui
