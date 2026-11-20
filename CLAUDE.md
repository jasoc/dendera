# Dendera — project brief for Claude

Read this first. Every new chat session starts empty; this file is the only shared memory
of *why* this project exists and *how* it is built. Keep it up to date when architecture or
intent changes. Longer background lives in `docs/`.

## What Dendera is

Dendera is a platform to **observe signals from reliable sources** — the sky (ephemeris),
geomagnetism, weather, daylight, the Chinese medical calendar, Schumann resonance… — and
**interpret them through switchable schools**: Ptolemaic/medieval medical astrology,
biometeorology, Chinese medicine, Human Design. From a person's birth data (date, time,
place) it shows **in real time** how the influences land on that person — **in the sky**
(chart wheel), **in the body** (body map), over **time** (timeline of every signal), and
against a **journal** the person fills in daily. A **Scuole** page explains, in plain words,
every school and every influence the code implements.

It is *not* a horoscope app. No "tomorrow avoid talking to girls because Mercury...".
Output is **technical and descriptive**: "Saturn square natal Uranus, orb 0.13°, applying,
tension; correspondences: skeleton, knees, nervous system". An instrument panel, not a
fortune cookie.

Name: the **Temple of Hathor at Dendera** (Egypt), whose ceiling held the *Dendera
Zodiac* — the oldest known complete circular zodiac (12 signs, 36 decans, the planets) on
one sky map. That is what this app draws.

## Our stance — read before answering anything

We are exploring these traditions seriously: not newspaper horoscopes, but the old idea that
planetary, solar and environmental events have real emotional, social and physical resonances
on people, often felt without being noticed or believed. Trigger: weeks of collective,
unexplained stress and fatigue among us and the people around us (Oct–Nov 2026) that
coincided with many retrograde planets.

Rules of engagement:

- **Do not lecture** with "this is pseudoscience, there is no evidence". We know the
  mainstream position and explicitly do not want to hear it.
- When asked whether an idea "makes sense", judge it **within its own school's tradition**:
  is it coherent with that tradition, does it have precedent, does it fit its correspondence
  system? (Medical astrology by Ptolemy and Culpeper, Chinese medicine by the *Neijing*,
  Human Design by Ra Uru Hu's own system…)
- Dendera separates **observation** from **interpretation**. Influences only report facts,
  from sources that are cited and marked primary or derived; schools are hypotheses that
  can be switched on and off; the **journal + correlation engine** is the test of every
  school. Keep that loop first-class.
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
- **Biometeorology**: effects of weather, light and geomagnetism on the body (Hippocrates'
  *Airs, Waters, Places*, seasonal affective disorder, Chizhevsky's heliobiology).
- **Chinese medicine (TCM)**: the twelve zang-fu and their meridians, the five phases and their
  seasons (from the 24 solar terms), the 24-hour organ clock, the six climatic excesses, the seven
  emotions, the five Shen, the vital substances, the Sheng and Ke cycles. It writes into its own
  nodes (`zf-*`, Shen, substances); the five-element table is in `rules/tcm.json` ("tables").
- **Tantric yoga**: seven chakras (Ṣaṭ-cakra-nirūpaṇa), the nadis Ida (lunar), Pingala (solar),
  Sushumna (opens at sandhya, dawn and dusk), Svara yoga (Shiva Swarodaya) for day/night and the
  lunar fortnight; planets in the chakras after Frawley (a modern synthesis, said so in the file).
- **Human Design**: once rejected as a static model, now a school like the others because
  Dendera reads it dynamically: transiting bodies in the 64 gates, their center, open vs
  defined natal centers (conditioning), channels completed by a transit. The bodygraph
  centers are mapped onto body zones.

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
        Astro/              ephemeris (Astronomy Engine), houses, AspectCatalog, natal chart
        Signals/            Signal, InfluenceInfo, ISignalProvider; pure influences:
                            EphemerisProvider (wraps SkySignalGenerator), TcmCalendarProvider,
                            HumanDesignProvider (+ HumanDesign mandala/centers/channels)
        Rules/              RuleSet = school (JSON model + validation), RuleEngine (one school),
                            SchoolEngine (combine schools), NatalBaselines, BodyZones vocabulary
        Readings/           Reading (one instant), Timeline (tracks)
        Journal/            journal model + CorrelationEngine
        Lexicon.cs          Italian display names for every id (also kinds and tag names)
      Dendera.Sources/      IMPURE influences: GfzKpProvider, OpenMeteoWeatherProvider, SchumannProvider;
                            OpenMeteoGeocoder
      Dendera.Api/          ASP.NET Core minimal API + EF Core SQLite (profiles with active schools,
                            journal); SignalHub, RuleSetCatalog, InfluenceStatusBoard;
                            also serves the built frontend from wwwroot/
      rules/                schools as versioned JSON (melothesia, biometeorology, tcm, human-design, tantric-yoga),
                            copied to the API output
      tests/Dendera.Core.Tests/   (references Core and Sources; Influences.cs mirrors Program.cs)
    frontend/               Angular 22 (standalone, zoneless, signals) + Angular Material 3, Node pinned in .node-version
      src/_theme-colors.scss  M3 palettes generated from the seeds (gold, violet, blue, indigo)
      src/app/core/         models.ts (API contracts), api.ts, profile-store.ts (active schools, toggle),
                            catalog-store.ts, icons.ts (id → Material Symbols name), symbols.ts
      src/app/components/   sky-wheel.ts, body-map.ts (SVG, fallback), body-3d/ (three.js), zone-detail.ts, icon.ts
      src/app/pages/        now, timeline, journal, insights (correlations), schools (catalog), profile
```

## The signal pipeline (core idea)

Vocabulary, used everywhere (code, API, UI, docs):

- **Influence** = a signal source = an `ISignalProvider` with an `InfluenceInfo` (Italian name
  and description, icon, provider kind `library`/`api`/`computed`, provider name, URL,
  license, reliability `primary`/`derived`, emitted kinds). Facts, no interpretation.
- **School** = a rule file (`src/backend/rules/*.json`, `RuleSet` in code): which influences it
  reads (`requires`) and how it maps their signals onto the body. Many-to-many with influences.
- **Body** = the body model, the common output space every school speaks: fixed nodes in ten
  layers (`BodyZones.Nodes`): **soma** (places, incl. brain and the sense organs), **systems**
  (whole body, incl. the five-element tissues), **temperament** (Galen's four humors), **emotions**
  (the seven of Chinese medicine), **mind** (the three medieval cells of the brain + mood),
  **zangfu** (the twelve organs of TCM, each with its meridian), **spirits** (the five Shen),
  **substances** (Qi, Xue, Yin, Yang, Jing, Jinye), **chakras** (seven) and **nadis** (Ida,
  Pingala, Sushumna). Traditional structure, modern Italian words. The vocabulary is shared and never switched off; the *relations* into it are rules,
  so they belong to schools and switch off with them.

Every influence emits the **same object**, a `Signal` sample: `Key` (track id:
`retrograde:mercury`, `transit:saturn:square:sun`, `kp`), `Kind` (`retrograde`,
`transit-aspect`, `geomagnetic`… see `SignalKinds`), `Source` (= influence id), `At`,
`Intensity` 0..1, optional `Value`/`Unit`, `Tags` (body, sign, aspect, natalPoint, organ,
gate, center…), stable `Label`, instant `Detail`.

```
 EphemerisProvider (pure)      ─┐
 TcmCalendarProvider (pure)    ─┤
 HumanDesignProvider (pure)    ─┼─► Signal[] ─► per school: RuleEngine(school, its natal baseline)
 GfzKpProvider (HTTP)          ─┤        │         └─► SchoolEngine: 1 − Π(1 − score) per zone (+ BySchool split)
 OpenMeteoWeatherProvider (HTTP)┤        │
 SchumannProvider (HTTP, derived)┘       └─► daily series (signals, combined zones, per-school zones)
                                              + journal ─► CorrelationEngine ─► findings
```

- **New influence = new `ISignalProvider`** registered in `Dendera.Api/Program.cs` (and in
  `tests/…/Influences.cs`). The sky is a provider like any other: `SignalHub` runs every
  provider in the same loop with a `SignalQuery` (frames From/Step/To, location, natal chart).
  Frame-aligned (pure) providers have `SampleValidity = 0`. A failing provider never breaks a
  reading (`SignalHub` reports its status; the UI shows source chips with influence icons and
  a "derivata" badge for derived data).
- **Aspects are facts**, not school data: `AspectCatalog.Default` (8 aspects; transit orb ×0.4,
  sky orb ×0.5, major only). Schools filter on the `aspect` tag if they want fewer.
- **Schools are data**: correspondences (symbol → zones), natal amplification, saturation,
  `icon`, `tradition`, `references`, `requires`, `baseline` (`melothesia` | `human-design` |
  `none`), and rules (`when` kind/tags/minIntensity, `"*"` = any tag value → `effect`
  zones / zonesFrom tags, weight, quality, tagWeights, Italian text template with `{tag}`
  placeholders). Validated at load: unknown zones, unknown baseline, unregistered required
  influences, rules on kinds no required influence emits — all fail fast.
- **Zone score** per school = `1 - exp(-raw / saturation)`, raw = Σ weight × intensity × tagWeight ×
  (1 + natalAmplification × natalBaseline[zone]). **Combined** = the **mean of the schools that
  cover the node** (`RuleSet.Covers`: zones its rules or correspondences can reach), zeros
  included: consensus, so more schools never inflate a node; a single school is exactly its own
  score. Combined natal baseline = max over those schools. Weights, orbs and saturations are
  provisional until the calibration (`docs/plans/calibration.md`).
- **Natal baseline**: zones emphasized by the birth chart, per school strategy (melothesia:
  luminaries and Ascendant weigh most; human-design: defined centers); shown as dashed gold
  rings on the body map.
- **Active schools** are stored per profile (`Profile.Schools`, comma-separated) so journal
  correlations stay reproducible; `PUT /api/profiles/{id}/schools` is the fast toggle,
  `?schools=a,b` overrides without saving. `GET /api/catalog` = influences (+ last status),
  schools (rules, correspondences), zones.
- **Second pass (node rules)**: after a school's rules ran on the signals, each node it activated
  becomes an internal signal of kind `node` (intensity = score, tag `quality` = its dominant
  quality) and the same school's rules with `when.kind = "node"` run once on them — e.g. TCM
  "liver sollecitato → anger". Per school, one pass only (no chains, no cycles).
- **Per-quality scores**: `ZoneActivation.QualityScores` saturates each quality like the zone
  score; schools combine per quality by the same consensus mean. The UI calls the score
  "intensità dell'influsso": how much the signals press on a node, not how the person feels.
- **Body nodes** are a fixed vocabulary: `Dendera.Core/Rules/BodyZones.cs` ↔
  `src/frontend/src/app/components/body-map.ts` (2D ANCHORS) ↔
  `components/body-3d/anchors-3d.ts` (3D anchors) and `channels-3d.ts` (meridian and nadi paths).
  Change them together.
- **3D body of light** (Adesso): three.js, `components/body-3d/`. The figure is one signed
  distance field (`body-shape.ts`, the same list generates the GLSL and the TS used for particles),
  raymarched with a golden fresnel rim; every node is a volumetric light (analytic gaussian along
  the ray) coloured by its dominant quality, as bright as its score; systems tint the aura. Zooming
  out shows the sky (`sky-3d.ts`): ecliptic and signs, planets at real longitudes, aspects, transit
  beams to the organs they touch, Kp aurora, Schumann wave. No WebGL → the 2D body map.
- **Correlations**: per (journal metric × signal / combined zone / per-school zone track) Pearson r
  on daily max intensity, seeded permutation p-value (1000), Benjamini–Hochberg q across all
  pairs. Per-school tracks (`zone:{school}:{zone}`) exist only with 2+ schools active and let
  the journal compare schools. Evidence: strong (q<0.05, |r|≥0.3), hint (q<0.2, |r|≥0.2).
  Min 10 journal days.

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
  API for older); Schumann index from schumannresonancelive.com (CC0, **derived** from the
  Tomsk TSU spectrograms, relative 0–100 scale, last 24 h only). Cached in memory
  (15–60 min). Days without a sample count as intensity 0.
- **Human Design**: gate 41 at 302° (2°00' Aquarius), 64 gates × 6 lines; design = when the
  Sun was 88° before the natal Sun (bisection). **TCM calendar**: solar terms from the Sun's
  longitude (Lichun 315°), organ clock from local mean solar time (UTC + lon/15).
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
- UI: **Material Design 3** (Angular Material, dark theme in `styles.scss`: gold primary, violet
  secondary, indigo night surfaces) with a spiritual feel (Cinzel headings, starry background).
  Use Material components (`matButton`, chips, form fields, slide toggles, expansion panels) and
  the `--mat-sys-*` tokens; legacy `--gold`/`--panel`/`--muted` variables map onto them.
  Icons are **Material Symbols** (rounded): the backend sends icon ids (`school-tcm`, `ephemeris`),
  `core/icons.ts` maps them to symbol names. `src/index.html` loads only the used glyphs
  (`icon_names=`, sorted list): add every new name there too, or it renders as plain text.
- UI vocabulary: **Segnali** (facts, grouped by influence, tertiary-coloured "facts" kicker) vs
  **Conseguenze** (what the active schools make of them on the body, primary-coloured kicker).
  Every signal shows its influence icon and the icons of the schools reading it.
- Never `git commit` / `git push`: we commit ourselves. No AI attribution anywhere.

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

- More signals: solar flares / solar wind (NOAA SWPC), Open-Meteo air quality and pollen.
- Persist Schumann (and other short-history) samples in SQLite to build history.
- Lagged correlations (0–3 days); automatic per-person weight tuning.
- Dedicated TCM organ zones on the body map if the mapping proves too coarse.
- Decans, fixed stars, profections / time-lord techniques (Hellenistic).
- Friends/circle mode: compare journals of several people against the same sky.
- Public multi-user instance (auth, isolation, DB-backed signal cache, i18n).
