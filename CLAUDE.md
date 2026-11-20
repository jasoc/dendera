# Dendera — project brief for Claude

Read this first. Every new chat session starts empty; this file is the only shared memory
of *why* this project exists and *how* it is built. Keep it up to date when architecture or
intent changes. Longer background lives in `docs/`.

## What Dendera is

Dendera takes a person's birth data (date, time, place), computes their natal chart, and
shows **in real time** how the current sky and other external influences (geomagnetic
activity, weather, daylight, lunar cycle, eclipses) "resonate" on that person — **in the
sky** (chart wheel), **in the body** (body map), over **time** (timeline of every signal),
and against a **journal** the person fills in daily.

It is *not* a horoscope app. No "tomorrow avoid talking to girls because Mercury...".
Output is **technical and descriptive**: "Saturn square natal Uranus, orb 0.13°, applying,
tension; correspondences: skeleton, knees, nervous system". An instrument panel, not a
fortune cookie.

Name: the **Temple of Hathor at Dendera** (Egypt), whose ceiling held the *Dendera
Zodiac* — the oldest known complete circular zodiac (12 signs, 36 decans, the planets) on
one sky map. That is what this app draws.

## The owner's stance — read before answering anything

The owner (Paride) is exploring astrology seriously: not newspaper horoscopes, but the old
idea that planetary/solar events have real emotional, social and physical resonances on
people, often felt without being noticed or believed. Trigger: weeks of collective,
unexplained stress and fatigue in his circle (Oct–Nov 2026) that coincided with many
retrograde planets.

Rules of engagement:

- **Do not lecture** with "this is pseudoscience, there is no evidence". He knows the
  mainstream position and explicitly does not want to hear it.
- When asked whether an idea "makes sense", judge it **as an ancient priest-astronomer
  would** (Babylonian / Egyptian / Hellenistic / medieval): is it coherent with the
  tradition, does it have precedent, does it fit the correspondence system?
- The project "creates the science" **empirically, Babylonian-style**: the *Astronomical
  Diaries* (~650 BC on) logged sky events next to weather, river levels, prices and city
  mood for centuries. Dendera does the same: signals + personal journal + correlation
  engine. Rules are hypotheses; the journal is the test. Keep this first-class.
- Verify sky claims with the ephemeris, not social media. Example: Oct–Nov 2026 peaked at
  5 of 8 planets retrograde (Venus, Mercury, Saturn, Uranus, Neptune) — a lot, not "all".

## Historical vocabulary we build on

- **Medical astrology / iatromathematics** (Ptolemy *Tetrabiblos*, Culpeper).
- **Melothesia** (sign → body): Aries head, Taurus throat, Gemini shoulders/arms/lungs,
  Cancer chest/stomach, Leo heart/spine, Virgo intestines, Libra kidneys/lower back,
  Scorpio pelvis, Sagittarius hips/thighs/liver, Capricorn knees/bones/skin, Aquarius
  calves/ankles/circulation, Pisces feet/lymph.
- **Planet → organ/system**: Sun heart/vitality, Moon fluids/stomach/sleep, Mercury
  nerves/lungs, Venus kidneys/throat, Mars muscles/blood, Jupiter liver, Saturn
  bones/skin; modern: Uranus nervous system, Neptune lymph/sleep, Pluto elimination.
- Medieval rule: don't operate on the body part ruled by the Moon's current sign (implemented
  as the `moon-sign` rule).
- **Retrograde**: apparent backward motion seen from Earth. **Stations** (speed ≈ 0) are
  traditionally the most potent moments; retrograde intensity grows near stations.
- Human Design was considered and rejected as a model: static (who you *are*) and an
  unreadable bodygraph. Dendera is dynamic (what is happening *now*) and readable.

## Repository layout

```
dendera/
  CLAUDE.md                 this file
  docs/                     vision, signals & rules guide
  scripts/                  dev env (run.sh + process-compose.yaml, dev-server, dev-spa, prod-test)
  Dockerfile, docker-compose.yml   single production container (API + built SPA)
  src/
    backend/                .NET 10 — solution Dendera.slnx, nuget.config, dotnet-tools.json (dotnet-ef)
      Dendera.Core/         PURE. No I/O, no ASP.NET, no EF. Deterministic, unit-tested.
        Astro/              ephemeris (Astronomy Engine), houses, aspects, natal chart
        Signals/            Signal model, SkySignalGenerator (pure), ISignalProvider
        Rules/              RuleSet (JSON model + validation), RuleEngine, BodyZones vocabulary
        Readings/           Reading (one instant), Timeline (tracks)
        Journal/            journal model + CorrelationEngine
        Lexicon.cs          Italian display names for every id
      Dendera.Sources/      IMPURE adapters: GfzKpProvider, OpenMeteoWeatherProvider, OpenMeteoGeocoder
      Dendera.Api/          ASP.NET Core minimal API + EF Core SQLite (profiles, journal);
                            also serves the built frontend from wwwroot/
      rules/                rule sets as versioned JSON ("theories"), copied to the API output
      tests/Dendera.Core.Tests/
    frontend/               Angular 22 (standalone, zoneless, signals), Node pinned in .node-version
      src/app/core/         models.ts (API contracts), api.ts, profile-store.ts, symbols.ts
      src/app/components/   sky-wheel.ts, body-map.ts (SVG)
      src/app/pages/        now, timeline, journal, insights (correlations), profile
```

## The signal pipeline (core idea)

Every influence — planetary or not — becomes the **same object**, a `Signal` sample:
`Key` (track id: `retrograde:mercury`, `transit:saturn:square:sun`, `kp`), `Kind`
(`retrograde`, `transit-aspect`, `geomagnetic`… see `SignalKinds`), `Source`, `At`,
`Intensity` 0..1, optional `Value`/`Unit`, `Tags` (body, sign, aspect, natalPoint,
quality…), stable `Label`, instant `Detail`.

```
 SkySignalGenerator (pure, Core) ─┐
 GfzKpProvider (HTTP)            ─┼─► Signal[] ─► RuleEngine(RuleSet, natal baseline) ─► zone activations
 OpenMeteoWeatherProvider (HTTP) ─┘        │                                               (+ explanations)
                                           └─► daily series + journal ─► CorrelationEngine ─► findings
```

- **New influence = new `ISignalProvider`** registered in `Dendera.Api/Program.cs`.
  Nothing else changes. A failing provider never breaks a reading (`SignalHub` reports
  its status; the UI shows source chips).
- **Rules are data**: `src/backend/rules/*.json` define correspondences (symbol → zones),
  aspects (angle, orb, quality), orb factors, natal amplification, saturation, and rules
  (`when` kind/tags/minIntensity → `effect` zones / zonesFrom tags, weight, quality,
  tagWeights, Italian text template with `{tag}` placeholders). Different schools =
  different files. Unknown zones fail at load.
- **Zone score** = `1 - exp(-raw / saturation)`, raw = Σ weight × intensity × tagWeight ×
  (1 + natalAmplification × natalBaseline[zone]).
- **Natal baseline**: zones emphasized by the birth chart (luminaries and Ascendant weigh
  most); shown as dashed gold rings on the body map.
- **Body zones** are a fixed vocabulary: `Dendera.Core/Rules/BodyZones.cs` ↔
  `src/frontend/src/app/components/body-map.ts` (ANCHORS). Change both together.
- **Correlations**: per (journal metric × signal/zone track) Pearson r on daily max
  intensity, seeded permutation p-value (1000), Benjamini–Hochberg q across all pairs.
  Evidence: strong (q<0.05, |r|≥0.3), hint (q<0.2, |r|≥0.2). Min 10 journal days.

## Key technical choices

- **Ephemeris**: Astronomy Engine (C#, MIT, ~1′, no data files). Swiss Ephemeris rejected
  for now (AGPL/commercial licensing for a future public instance). Tropical, geocentric,
  true ecliptic of date. Speed = finite difference over ±12h. Lunar node = mean node.
- **Houses**: Placidus (default; semi-arc iteration, falls back to Porphyry above 66°),
  Whole Sign, Porphyry. Ascendant on the left in the wheel.
- **Birth time zone**: profile stores an explicit UTC offset (minutes); the UI pre-fills it
  from the place's IANA zone via `/api/timezone/offset`, the user can override (historical
  DST on Windows can be wrong).
- **External data** (free, no keys): Kp from GFZ Potsdam (`kp.gfz.de`, CC BY 4.0, history
  + nowcast); weather + geocoding from Open-Meteo (forecast API for last ~85 days, archive
  API for older). Cached in memory (30–60 min). Days without a sample count as intensity 0.
- **DB**: SQLite via EF Core **migrations** (`Data/Migrations`); `Database.Migrate()` at
  startup. Add a migration for every schema change:
  `dotnet ef migrations add <Name> --project Dendera.Api --output-dir Data/Migrations`
  (run from `src/backend`; `dotnet tool restore` first).
- No auth (single user, self-hosted). A public instance needs auth, per-user isolation,
  rate limiting, and a DB-backed cache for external sources.

## Conventions

- Code, comments, identifiers, docs: **English**. User-facing UI text and rule texts:
  **Italian** (names in `Lexicon.cs` and `symbols.ts`; i18n deferred).
- Core stays pure: anything needing the network, the clock or the DB does not belong in
  `Dendera.Core` — pass the data in. Inject time (`TimeProvider`), seed randomness.
- Frontend: standalone components, signals for all async state (the app is zoneless —
  plain field mutation after an `await` will not re-render).
- Never `git commit` / `git push`: Paride commits himself. No AI attribution anywhere.

## Running

Dev runs on Linux / WSL (the Windows build of process-compose has no detached mode).
Toolchain pins: `global.json` (.NET SDK), `src/frontend/.node-version` (Node, exact).

```bash
./scripts/run.sh             # API (:5180, dotnet watch) + Angular dev server (:4200) under process-compose
./scripts/run.sh --stop      # stop the stack; ./scripts/dev-server.sh / dev-spa.sh run one side alone
cd src/backend && dotnet test
./scripts/prod-test.sh       # build the production image and run it (docker-compose.yml, :8080)
```

Production = **one container** (root `Dockerfile`, same pattern as the sibling `lodge`
project): Node stage builds the SPA into `Dendera.Api/wwwroot`, SDK stage publishes the API,
aspnet runtime serves both; SQLite at `/data/dendera.db` (volume). Base images are pinned by
digest. `DENDERA_VERSION` build arg → InformationalVersion → `/api/health`.

## Roadmap after MVP

- More signals: solar flares / solar wind (NOAA SWPC), Schumann resonance, more weather.
- Decans, fixed stars, profections / time-lord techniques (Hellenistic).
- Rule-set comparison against the journal; automatic per-person weight tuning.
- Friends/circle mode: compare journals of several people against the same sky.
- Public multi-user instance (auth, isolation, DB-backed signal cache, i18n).
