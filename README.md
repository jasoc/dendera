# Dendera

A platform to observe signals from reliable sources (the sky, geomagnetism, weather, …) and
interpret them through switchable **schools** (medical astrology, biometeorology, Chinese
medicine, Human Design), mapped onto the body and tested against your own daily journal.

Named after the Temple of Hathor at Dendera, home of the oldest known circular zodiac.

- **Adesso**: chart wheel (natal and current sky, active transits), body map, active signals.
- **Timeline**: every signal, body zone and journal metric on one time axis.
- **Diario**: 10 seconds a day (energy, mood, sleep, focus, stress, gut, head, body).
- **Corrispondenze**: which signals, and which schools, actually move *you* (permutation tests and FDR control).
- **Scuole**: the catalog: every school and every influence, explained in plain words.

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

## Sources (influences)

| Influence | What it measures | Provider | Name | License | Data |
|---|---|---|---|---|---|
| `ephemeris` | Positions of Sun, Moon and planets: signs, retrogrades and stations, aspects in the sky and to the natal chart, lunar phase and distance, daylight hours, eclipses | library | [Astronomy Engine](https://github.com/cosinekitty/astronomy) | MIT | primary |
| `gfz` | Planetary Kp index (geomagnetic activity), every 3 hours, history and nowcast | API | [GFZ Potsdam Kp API](https://kp.gfz.de) | CC BY 4.0 | primary |
| `open-meteo` | Local weather: pressure and its change, temperature and swings, humidity, dryness, wind | API | [Open-Meteo](https://open-meteo.com) | CC BY 4.0 | primary |
| `schumann` | Schumann resonance activity index (relative 0–100 scale) | API | [schumannresonancelive.com](https://schumannresonancelive.com), from the Tomsk TSU spectrograms | CC0 | **derived** |
| `tcm-calendar` | Chinese medical calendar: the 24 solar terms and five-phase seasons, the organ clock | computed | Dendera, on Astronomy Engine | — | primary |
| `human-design` | The gate, line and center each body occupies in the Rave mandala, against the natal design | computed | Dendera, on Astronomy Engine | — | primary |

Geocoding (place search) also uses [Open-Meteo](https://open-meteo.com) (CC BY 4.0).

**Schumann limitations.** A third party digitises the Tomsk State University spectrograms and
partly models them into a relative score, not a physical unit. The feed only holds the last
24 hours, so older days have no samples and count as 0 in correlations. Persisting samples
to build history is a planned follow-up. The UI always marks it as *derivata*.

## Schools

| Rule file | School | Tradition | References | Influences |
|---|---|---|---|---|
| `rules/melothesia.json` | Melotesia | Ptolemaic and medieval medical astrology (iatromathematics) | Ptolemy, *Tetrabiblos*; Manilius, *Astronomica*; Culpeper, *Astrological Judgement of Diseases*; medieval medical calendars | `ephemeris` |
| `rules/biometeorology.json` | Biometeorologia | Human biometeorology: effects of weather, light and geomagnetism on the body | Hippocrates, *Airs, Waters, Places*; International Society of Biometeorology; SAD research; Chizhevsky; Schumann, Wever | `ephemeris`, `gfz`, `open-meteo`, `schumann` |
| `rules/tcm.json` | Medicina tradizionale cinese | Zang-fu and meridians, five elements, seven emotions, five Shen, six excesses, Sheng/Ke cycles | *Huangdi Neijing* (Suwen, Lingshu); *Nanjing*; *Shanghan Lun*; Chen Wuze (1174); *Huainanzi* | `tcm-calendar`, `open-meteo`, `ephemeris` |
| `rules/tantric-yoga.json` | Yoga tantrico | Tantric and Hatha yoga: chakras, nadis, Svara yoga | Pūrṇānanda, *Ṣaṭ-cakra-nirūpaṇa*; Avalon, *The Serpent Power*; *Haṭha Yoga Pradīpikā*; *Śiva Svarodaya*; Frawley (planets and chakras, modern) | `ephemeris` |
| `rules/human-design.json` | Human Design | Synthetic system of 1987 (Ra Uru Hu): I Ching, astrology, chakras, Kabbalah | Ra Uru Hu, *The Definitive Book of Human Design*; *Rave I'Ching*; the I Ching | `human-design` |

Toggle schools from the chips in the top bar. With several active, a node's score is the mean
of the schools that can speak about it (consensus), and each school also gets its own tracks for
the correlations. Weights and saturations are provisional until the calibration
(`docs/plans/calibration.md`).
See [docs/signals-and-rules.md](docs/signals-and-rules.md) to add an influence or a school.
