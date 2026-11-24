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
  traditionally the most potent moments (own `station` signal). Retrograde and slow motion follow
  Lilly's debilities (*Christian Astrology* p. 115: retrograde −5, slow −2 → intensity 1 and 0.4).
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
- **Other schools** (2026-11, `docs/plans/schools-and-influences.md`): **Astrologia tradizionale**
  (houses as life domains, profections, Lilly's dignities), **Jyotish** (sidereal zodiac, Kalapurusha,
  dashas), **Ayurveda** (doshas by hour, ritu, age, weather), **Medicina galenica** (humours by season,
  age, weather), **Cabala ermetica** (Sephiroth on Adam Kadmon, Agrippa III.10), **Antroposofia**
  (Steiner's four members, septennials, planetary organs), **Cronobiologia** (modern control group).
- **Lineages**: every school declares its family (`lineage`) and the families it borrows from
  (`parents`), from `Rules/Lineages.cs`. Agreement on a node between independent lineages is a real
  crossing of sources; inside one family it is the same idea counted twice. The zone detail says which.
  A school counts with its own lineage, or with a parent lineage only when a school of that lineage is
  active — one step, never chained (Human Design borrows from four families and would merge them all).
  `?consensus=lineage` (toggle on Scuole, kept in the browser) averages one vote per family instead of one
  per school; Corrispondenze sums the per-school findings by family.

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
                            HumanDesignProvider (+ HumanDesign mandala/centers/channels),
                            TraditionalTime.cs: PlanetaryHoursProvider, LunarStationsProvider (mansions,
                            nakshatras, sidereal placements), LifeTimesProvider (septennials, profections, dashas);
                            DecumbitureProvider (illness onsets from the journal: Hippocratic days, Culpeper's Moon)
        Rules/              RuleSet = school (JSON model + validation), RuleEngine (one school),
                            SchoolEngine (combine schools), NatalBaselines, BodyZones vocabulary
        Readings/           Reading (one instant), Timeline (tracks)
        Journal/            journal model + CorrelationEngine
        Calibration/        reference set, climate zones, quantile tables (rarity), hashes
        Lexicon.cs          Italian display names for every id (also kinds and tag names)
      Dendera.Sources/      IMPURE influences: GfzKpProvider, OpenMeteoWeatherProvider, SchumannProvider,
                            NoaaSpaceWeatherProvider (solar wind, Bz, X-ray flares), OpenMeteoAirQualityProvider
                            (PM2.5, PM10, O3, NO2 vs WHO 2021, pollen, UV); OpenMeteoGeocoder
      Dendera.Api/          ASP.NET Core minimal API + EF Core SQLite (profiles with active schools,
                            journal); SignalHub, RuleSetCatalog, InfluenceStatusBoard;
                            also serves the built frontend from wwwroot/; Calibration/ = the ritual
                            (`calibrate`, `fetch-reference` commands)
      rules/                twelve schools as versioned JSON (melothesia, biometeorology, tcm, human-design,
                            tantric-yoga, traditional-astrology, jyotish, ayurveda, galenic, kabbalah, steiner,
                            chronobiology)
                            + their <id>.quantiles.json (written by the ritual), copied to the API output
      calibration/          manifest.json = content hashes of the ritual's reference fixtures (Kp, hourly weather per
                            climate zone, 1991–2020); the *.csv.gz themselves are gitignored, `fetch-reference` downloads them
      tests/Dendera.Core.Tests/   (references Core and Sources; Influences.cs mirrors Program.cs)
    frontend/               Angular 22 (standalone, zoneless, signals) + Angular Material 3, Node pinned in .node-version
      src/_theme-colors.scss  M3 palettes generated from the seeds (gold, violet, blue, indigo)
      src/app/core/         models.ts (API contracts), api.ts, profile-store.ts (active schools, toggle),
                            catalog-store.ts, icons.ts (id → Material Symbols name), symbols.ts
      src/app/components/   sky-wheel.ts, body-map.ts (SVG, fallback), body-3d/ (three.js), icon.ts,
                            now/ (Adesso's floating panels: layer-panel.ts, chain-panel.ts)
      src/app/core/diary-hints.ts  strong journal correspondences active now → hints on Adesso
      src/app/pages/        now, timeline, journal, insights (correlations), schools (catalog), profile
```

## The signal pipeline (core idea)

Vocabulary, used everywhere (code, API, UI, docs):

- **Influence** = a signal source = an `ISignalProvider` with an `InfluenceInfo` (Italian name
  and description, icon, provider kind `library`/`api`/`computed`, provider name, URL,
  license, reliability `primary`/`derived`, emitted kinds). Facts, no interpretation.
- **School** = a rule file (`src/backend/rules/*.json`, `RuleSet` in code): which influences it
  reads (`requires`) and how it maps their signals onto the body. Many-to-many with influences.
- **Body** = the body model, the common output space every school speaks: fixed nodes in fourteen
  layers (`BodyZones.Nodes`): **soma** (places, incl. brain and the sense organs), **systems**
  (whole body, incl. the five-element tissues), **temperament** (Galen's four humors), **emotions**
  (the seven of Chinese medicine), **mind** (the three medieval cells of the brain + mood),
  **zangfu** (the twelve organs of TCM, each with its meridian), **spirits** (the five Shen),
  **substances** (Qi, Xue, Yin, Yang, Jing, Jinye), **chakras** (seven), **nadis** (Ida,
  Pingala, Sushumna), **domains** (the twelve houses as areas of life), **doshas** (Vata, Pitta,
  Kapha), **sephiroth** (ten, with 3D anchors on the Tree) and **members** (Steiner's four). Traditional structure, modern Italian words. The vocabulary is shared and never switched off; the *relations* into it are rules,
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
 GfzKpProvider (HTTP)          ─┤        │         └─► SchoolEngine: consensus mean per zone (+ BySchool split)
 OpenMeteoWeatherProvider (HTTP)┤        │
 SchumannProvider (HTTP, derived)┘       └─► daily series (signals, combined zones, per-school zones)
                                              + journal ─► CorrelationEngine ─► findings
```

- **New influence = new `ISignalProvider`** registered in `Dendera.Api/Program.cs` (and in
  `tests/…/Influences.cs`, the ritual's list in `CalibrationRitual.cs` and, if pure, `PureSignals`
  in `Calibration/ReferenceSampler.cs`, or calibration and personal rarity will not see it). The sky is a provider like any other: `SignalHub` runs every
  provider in the same loop with a `SignalQuery` (frames From/Step/To, location, natal chart).
  Frame-aligned (pure) providers have `SampleValidity = 0`. A failing provider never breaks a
  reading (`SignalHub` reports its status; the UI shows source chips with influence icons and
  a "derivata" badge for derived data).
- **Aspects are facts**, not school data: `AspectCatalog.Default` (8 aspects). Orbs are per
  planet (al-Bīrūnī/Lilly): sky = sum of the two moieties, major only; transit = the transiting
  planet's moiety; minor aspects half. Schools filter on the `aspect` tag if they want fewer.
- **Every rule carries** `name` (Italian, cause first: "Pianeta retrogrado → i suoi organi"), `about`
  (what it says, in its tradition) and `source` (author, work, chapter, or "convenzione dello
  strumento"); a test fails on a rule without them. The UI shows the name and the source, the `text`
  template only for the instant detail.
- **Schools are data**: correspondences (symbol → zones), natal amplification, saturation,
  `icon`, `tradition`, `references`, `requires`, `baseline` (`melothesia` | `human-design` |
  `none`), and rules (`when` kind/tags/minIntensity, `"*"` = any tag value → `effect`
  zones / zonesFrom tags, weight, quality, tagWeights, Italian text template with `{tag}`
  placeholders). Validated at load: unknown zones, unknown baseline, unregistered required
  influences, rules on kinds no required influence emits — all fail fast.
- **Zone score** per school = **rarity**: the share of reference moments with a raw sum strictly
  lower than now ("più sollecitato che nell'N% dei tuoi momenti"; 50% ordinary, 95% rare; **zero
  stays zero**). For schools that read only pure influences the reference is **personal**
  (`PersonalReference`: 4000 moments over the 90 years from the person's birth, on their chart, at
  their current place; built on first use in ~2 s, cached by `PersonalReferenceCache`), so zones a
  chart always emphasises (generational natal points) do not sit at 90%+ forever — that constant
  sensitivity is the natal baseline. Weather schools use their climate table. raw = Σ weight × intensity × tagWeight × (1 + natalAmplification ×
  natalBaseline[zone]). The reference distribution is a quantile table per school and node
  (`rules/<school>.quantiles.json`, 101 quantiles), global for sky-only schools, per **climate
  zone** for schools that read the local weather (`ClimateZones`: the person's place → nearest of
  seven reference places). These population tables are the fallback for sky schools.
  `ProfileContextFactory` hands each school its table (`RuleSet.Rarity`). Without an up-to-date table the school falls back to
  `1 - exp(-raw / saturation)` and the catalog shows it "da ricalibrare". **Combined** = the **mean of the schools that
  cover the node** (`RuleSet.Covers`: zones its rules or correspondences can reach), zeros
  included: consensus, so more schools never inflate a node; a single school is exactly its own
  score. Combined natal baseline = max over those schools. Numbers of the traditions carry their
  source in the rule files (orbs, motion, HD, biometeorology); the others are marked as
  conventions of the instrument (`docs/plans/calibration.md`).
- **Calibration ritual** (release step; the population tables, the climate tables and the report), from `src/backend`:
  `dotnet run --project Dendera.Api -- calibrate` (offline, deterministic, ~40 s) writes the
  quantile tables and `docs/calibration-report.md`. Reference: 500 synthetic births and 50 000
  random instants 1900–2100 (seeded, no personal data); weather schools: 20 000 three-hour slots
  per climate zone 1991–2020 from the fixtures in `src/backend/calibration` (Kp from GFZ, weather
  from the Open-Meteo archive, ~13 MB). They are **not committed**: run `-- fetch-reference` once
  (network) on a fresh clone. Only `calibration/manifest.json` (SHA-256 of each file's content) is
  committed; the ritual refuses files that differ from it and the reference hash is built from it.
  If the sources change their data, `fetch-reference` says so; `--update-manifest` accepts it, then
  re-run the ritual and commit manifest and tables together. **Re-run it**
  when anything changes a school's raw sums (rules, weights, tagWeights, minIntensity,
  correspondences, natalAmplification, saturation, requires) and **bump
  `ReferenceSet.SignalsVersion`** first when an influence changes how it computes intensity or
  what it emits (orbs, Kp mapping, weather thresholds, new kinds). Texts, names, references,
  tables and icons need nothing. `CalibrationTests` fails on a stale table, so it cannot ship.
  Schumann is not in the reference (24 h feed only).
- **Natal baseline**: zones emphasized by the birth chart, per school strategy (melothesia:
  luminaries and Ascendant weigh most; human-design: defined centers); shown as dashed gold
  rings on the body map.
- **Journal** has an optional `illnessOnset` (local time a malaise began that day): the
  decumbiture influence follows it for 30 days (`SignalQuery.IllnessOnsets`).
- **Active schools** are stored per profile (`Profile.Schools`, comma-separated) so journal
  correlations stay reproducible; `PUT /api/profiles/{id}/schools` is the fast toggle,
  `?schools=a,b` overrides without saving. `GET /api/catalog` = influences (+ last status),
  schools (rules, correspondences), zones.
- **Second pass (node rules)**: after a school's rules ran on the signals, each node it activated
  becomes an internal signal of kind `node` (intensity = the internal scale `1 − e^(−raw/saturation)`,
  not the rarity, so node rules' `minIntensity` keep their meaning; tag `quality` = its dominant
  quality) and the same school's rules with `when.kind = "node"` run once on them — e.g. TCM
  "liver sollecitato → anger". Per school, one pass only (no chains, no cycles).
- **Per-quality scores**: `ZoneActivation.QualityScores` = node rarity × (raw of the quality / raw
  of the node); schools combine per quality by the same consensus mean. The UI calls the score
  "rarità dell'influsso": how unusual the pressure of the signals on a node is, not how the person feels.
- **Body nodes** are a fixed vocabulary: `Dendera.Core/Rules/BodyZones.cs` ↔
  `src/frontend/src/app/components/body-map.ts` (2D ANCHORS) ↔
  `components/body-3d/anchors-3d.ts` (3D anchors) and `channels-3d.ts` (meridian and nadi paths).
  Change them together.
- **Adesso** is a full-screen stage: the 3D body and its sky fill the page under the top bar
  (`--topbar-h`); a time pill on top, the school picker widget (all / none) top left; floating
  panels: left, the body by layers (pick a node, or show only one layer's lights); right, cause →
  effect (`components/now/`): tabs **Ora** (the default: each strong consequence → when it is
  above an ordinary moment, from the ±48 h timeline the page loads once → its causes, each with
  its influence, the rules that carried it and its own active window; then the signals that move
  the body most, `core/episodes.ts`), **Flusso** (a Sankey of influences → signals → rules → body
  parts, `flow-diagram.ts`, or the same chains as a tree, `cause-tree.ts`), **Teorie** (the same cause landing on the same node through schools of different
  families, now and in the catalog), **Influenze**, **Cielo**; a selection opens a typed detail
  (consequence with its course over ±48 h, `time-trace.ts`; rule with its source and what it does
  now or why it is silent; signal; influence) and a full-screen mode. The four types have one colour
  and one shape each everywhere (`--c-influence` cyan hexagon, `--c-signal` amber square, `--c-rule`
  violet diamond, `--c-node` quality-coloured dot). **Everything is in the URL** (`at`, `sel` =
  `node:…|rule:school/id|signal:…|influence:…`, `tab`, `fv`, `layer`, `iso`, `full`, `sheet`, `pos`):
  deep links, and the browser's back equals the panel's back. Graph and theories:
  `core/flow-graph.ts`. Strong journal correspondences active now become hints. On phones the
  panels are bottom sheets opened from a dock.
- **3D body of light**: three.js, `components/body-3d/`. The figure is one signed
  distance field (`body-shape.ts`, the same list generates the GLSL and the TS used for particles), a
  moderately muscular classical build drawn symmetric at rest and posed in contrapposto by `pose()`,
  which is applied to the primitives, the anchors and the meridian/nadi paths alike (change the rest
  model, never the posed numbers). It is **alive**: `motionAt(t)` (breathing chest, sway, head
  looking around, arms swinging) drives uniforms; the shader evaluates the SDF through the inverse
  transforms per group (`core`, `chest`, `head`, `armL`, `armR`), particles move in their vertex
  shader, lights and channels follow on the CPU (`animate`, `groupAt`). Hologram palette (cyan rim,
  deep blue, scan lines). Free 360° orbit, slow auto-rotation when idle, zoom out for the sky;
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
  Tomsk TSU spectrograms, relative 0–100 scale, last 24 h only); space weather from NOAA SWPC
  (solar wind and Bz 24 h, GOES X-rays 7 days); air quality, pollen (Europe) and UV from the
  Open-Meteo air quality API (CAMS). Cached in memory (15–60 min). Days without a sample count as
  intensity 0. **Short-memory feeds are stored**: Schumann and NOAA are wrapped in
  `PersistentProvider` (SQLite table `Samples`) and `SampleCollector` asks them for the last day
  every hour while the API runs, so their history grows. Schumann, NOAA, air quality and the
  decumbiture are not in the calibration reference (no history long enough): when they fire, a
  weather school reads a little rarer than it is.
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
- UI: **Material Design 3** (Angular Material, dark theme in `styles.scss`), styled as a starship
  console: signal cyan primary, indigo secondary, amber tertiary on deep navy (palettes from
  `ng generate @angular/material:theme-color`, seeds in `_theme-colors.scss`), a faint HUD grid,
  glass panels with cyan hairlines. Type: **Orbitron** for titles (wide, bold, uppercase), **Exo 2**
  for text, **Share Tech Mono** for numbers (`--font-title`, `--font-ui`, `--font-mono`).
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
`npm run build` writes the SPA into `Dendera.Api/wwwroot`, which can crash a running `dotnet watch`:
restart the api process afterwards (`process-compose process restart api -U -u <socket>`).
Toolchain pins: `global.json` (.NET SDK), `src/frontend/.node-version` (Node, exact).

```bash
./scripts/run.sh             # API (:5180, dotnet watch) + Angular dev server (:4200) under process-compose
./scripts/run.sh --stop      # stop the stack; ./scripts/dev-server.sh / dev-spa.sh run one side alone
./scripts/run.sh --background  # start without the TUI; DENDERA_HOST=0.0.0.0 (.env) serves on every interface
cd src/backend && dotnet test
./scripts/prod-test.sh       # build the production image and run it (docker-compose.yml, :8080)
```

Production = **one container** (root `Dockerfile`, same pattern as the sibling `lodge`
project): Node stage builds the SPA into `Dendera.Api/wwwroot`, SDK stage publishes the API,
aspnet runtime serves both; SQLite at `/data/dendera.db` (volume). Base images are pinned by
digest. `DENDERA_VERSION` build arg → InformationalVersion → `/api/health`.

## Roadmap after MVP

Next big plan: `docs/plans/schools-and-influences.md` (new pure influences, richer schools, new
schools with declared lineages so agreement between independent traditions can be read). Highlights:

- More signals: solar flares / solar wind (NOAA SWPC), Open-Meteo air quality and pollen.
- Persist Schumann (and other short-history) samples in SQLite to build history.
- Lagged correlations (0–3 days); automatic per-person weight tuning.
- Dedicated TCM organ zones on the body map if the mapping proves too coarse.
- Decans, fixed stars, profections / time-lord techniques (Hellenistic).
- Friends/circle mode: compare journals of several people against the same sky.
- Public multi-user instance (auth, isolation, DB-backed signal cache, i18n).
