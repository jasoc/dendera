# Dendera

Real-time astrological resonance instrument: your natal chart, the current sky, solar and
terrestrial signals, mapped onto the body and tested against your own daily journal.

Named after the Temple of Hathor at Dendera, home of the oldest known circular zodiac.

- **Adesso**: chart wheel (natal and current sky, active transits), body map, active signals.
- **Timeline**: every signal, body zone and journal metric on one time axis.
- **Diario**: 10 seconds a day (energy, mood, sleep, focus, stress, gut, head, body).
- **Corrispondenze**: which signals actually move *you* (permutation tests and FDR control).

## Prerequisites

Development runs on Linux or WSL. The scripts check each tool is present at the pinned
version and stop if it isn't. The one exception is Node: if [nvm](https://github.com/nvm-sh/nvm)
is installed, they install and switch to the pinned version themselves.

| Tool | Version | Pinned in |
|------|---------|-----------|
| .NET SDK | `10.0.300` (or a later feature band) | `global.json` |
| Node.js | `24.22.3`, exactly | `src/frontend/.node-version` |
| [process-compose](https://github.com/F1bonacc1/process-compose) | any recent | — |
| Docker Engine + compose plugin | any recent (only for the container) | — |

<details>
<summary>Install commands</summary>

```bash
# .NET SDK, via Microsoft's install script (or your distro's package, if it ships 10.0.3xx)
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --version 10.0.300 --install-dir "$HOME/.dotnet"
# add to your shell profile:  export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"

# Node: just have nvm (https://github.com/nvm-sh/nvm) — the scripts install the version in
# src/frontend/.node-version. Any other version manager works too, as long as
# `node --version` matches it when the scripts run
curl -fsSL https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.1/install.sh | bash

# process-compose, via its official installer, into ~/.local/bin
sh -c "$(curl --location https://raw.githubusercontent.com/F1bonacc1/process-compose/main/scripts/get-pc.sh)" -- -d -b "$HOME/.local/bin"
```

</details>

## Run it locally

```bash
./scripts/run.sh            # API + UI dev server under process-compose (http://localhost:4200);
                            # quitting the TUI leaves it running, re-run to reattach
./scripts/run.sh --stop     # stop everything (the SQLite database stays)
./scripts/dev-server.sh     # just the API (dotnet watch) on :5180
./scripts/dev-spa.sh        # just the Angular dev server on :4200 (proxies /api to :5180)
./scripts/prod-test.sh      # build the image and run it as production does, on :8080
```

The API migrates its SQLite database (`src/backend/Dendera.Api/dendera.db`) on startup.
Tests: `dotnet test` in `src/backend`.

## Run it for real

One container: the API serves the built frontend from its `wwwroot`, SQLite lives in the
`dendera-data` volume.

```bash
docker compose up -d            # ghcr.io/jasoc/dendera:latest (DENDERA_IMAGE overrides it)
docker compose up -d --build    # build from this checkout instead
```

Listens on `:8080` (`DENDERA_PORT` changes the host port). `GET /api/health` reports the
image version.

See [CLAUDE.md](CLAUDE.md) for architecture and [docs/](docs/) for the vision and the
signal/rule guide.

## Data sources

- Ephemeris: [Astronomy Engine](https://github.com/cosinekitty/astronomy) (MIT)
- Geomagnetic Kp index: [GFZ Potsdam](https://kp.gfz.de) (CC BY 4.0)
- Weather and geocoding: [Open-Meteo](https://open-meteo.com) (CC BY 4.0)
