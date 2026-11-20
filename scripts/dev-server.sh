#!/bin/bash
# Runs Dendera.Api natively, with hot reload, on http://localhost:5180 (what
# src/frontend/proxy.conf.json expects). It migrates its SQLite database
# (src/backend/Dendera.Api/dendera.db) on startup — nothing else to run first.
# Requires the .NET SDK pinned in global.json.
# Usage: ./scripts/dev-server.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=lib/env.sh
source "$ROOT/scripts/lib/env.sh"
# shellcheck source=lib/dotnet.sh
source "$ROOT/scripts/lib/dotnet.sh"

dendera_load_env "$ROOT"
dendera_ensure_dotnet "$ROOT"

export ASPNETCORE_URLS="http://localhost:5180"

echo "==> Starting Dendera.Api on $ASPNETCORE_URLS (dotnet watch: edits to backend code apply live)"
# Run from the project folder: the default SQLite path (dendera.db) is relative to it.
cd "$ROOT/src/backend/Dendera.Api"
# wwwroot (the built SPA, gitignored) may be missing: dotnet watch crashes if a directory it
# remembers from an earlier build is gone. In dev the UI comes from :4200, so empty is fine.
mkdir -p wwwroot
# `dotnet watch` rebuilds and hot-reloads on every edit (Dendera.Core and Dendera.Sources
# included); an edit hot reload can't apply, a "rude edit" such as a changed signature,
# restarts the API by itself. --non-interactive: no prompt to answer, this runs under
# process-compose.
export DOTNET_WATCH_SUPPRESS_EMOJIS=1
exec dotnet watch run --non-interactive --no-launch-profile
