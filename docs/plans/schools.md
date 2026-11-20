# Plan — schools, influences, catalog page

Status: agreed with Paride on 2026-11-20, not started. Execute top to bottom; each step ends
green (`dotnet test` in `src/backend`, `npm run build` in `src/frontend`). Never `git commit`.
Read `CLAUDE.md` first. Dev runs in WSL: `./scripts/run.sh` (API :5180, UI :4200).

## Goal

Dendera is a platform to **observe every signal from reliable sources** (planets, weather,
geomagnetism, …) and **interpret it through switchable "schools"**. The UI must make it
obvious what the code implements and "what we are talking about": every school and every
influence has its own icon, a static catalog page explains them, and schools can be turned
on/off on the fly.

Vocabulary (use it everywhere — code, API, UI, docs):

- **Influence** = a signal source = an `ISignalProvider`. Facts, no interpretation.
  Library or API, primary or derived data.
- **School** = a rule set file (`src/backend/rules/*.json`): which influences it reads and how
  it maps their signals onto the body. Many-to-many with influences.
- **Body** = the fixed zone vocabulary (`BodyZones.cs` ↔ `body-map.ts`), the common output
  space every school speaks.

## Decisions already taken (do not re-ask)

1. `SkySignalGenerator` becomes an `ISignalProvider` like the others (no special path in `SignalHub`).
2. Weather / Kp / daylight rules move **out** of melothesia into a separate **Biometeorologia** school.
3. Four new/changed schools in this round: **Melotesia** (sky only), **Biometeorologia**
   (weather, Kp, light, Schumann), **Medicina cinese**, **Human Design**.
4. **Schumann** comes from a third-party feed derived from the Tomsk spectrograms; it must be
   shown everywhere as **derived / non-primary** data.
5. Active schools are stored **per profile in the DB** (journal correlations stay reproducible).
6. TCM organs are **mapped onto the existing body zones** (no new zones in this round); the
   TCM organ name travels as a tag and appears in the explanation text.

---

## Step 1 — Influence metadata and the sky as a provider

`Dendera.Core/Signals/Signal.cs`:

- Add `InfluenceInfo` record: `Id` (= `Source`), `Name` (Italian), `Description` (Italian, one
  or two sentences: what it measures), `Icon` (icon id, see step 7), `ProviderKind`
  (`library` | `api` | `computed`), `ProviderName` (e.g. "Astronomy Engine", "GFZ Potsdam Kp API"),
  `Url`, `License`, `Reliability` (`primary` | `derived`), `Kinds` (signal kinds it emits, with an
  Italian one-line description each).
- `ISignalProvider` gets `InfluenceInfo Info { get; }` (keep `Source`, or derive it from `Info.Id`).
- `SignalQuery` gets the per-profile context the sky needs: `NatalChart? Natal` and `TimeSpan Step`
  (frames are `From`, `From+Step`, … `To`).

Sky:

- Move the aspect catalogue out of the rule set: `AspectCatalog.Default` in `Astro/Aspects.cs`
  with today's 8 aspects and orbs (from `melothesia-classic.json`), plus the orb factors
  (transit 0.4, sky 0.5). Remove `aspects` / `orbs` from `RuleSet` and from `SkyContext`.
  A school that wants only Ptolemaic aspects filters on the `aspect` tag in its rules.
- New `EphemerisProvider : ISignalProvider` in `Dendera.Core/Signals` (pure: `Task.FromResult`)
  wrapping `SkySignalGenerator.Range/At`; `SampleValidity` = the query step. Info: library,
  Astronomy Engine (MIT), https://github.com/cosinekitty/astronomy, primary.
- `SignalHub`: drop the `SkySignalGenerator` dependency and the `Sky` list in `SignalBundle`;
  every provider goes through the same loop. `ActiveAt` keeps working via `SignalSet.LatestAt`
  with each provider's validity (sky samples sit exactly on the frames).
- Register it in `Program.cs` like the others. Fill `Info` for `GfzKpProvider` (api, GFZ Potsdam,
  https://kp.gfz.de, CC BY 4.0, primary) and `OpenMeteoWeatherProvider` (api, Open-Meteo,
  https://open-meteo.com, CC BY 4.0, primary).
- Tests: the readings/timeline computed through the provider path equal the old ones
  (`PipelineTests`).

## Step 2 — School model and multi-school engine

`Dendera.Core/Rules/RuleSet.cs` — new fields on `RuleSet` (keep the class name; "school" is the
UI word, mention it in the doc comment):

- `Icon` (icon id), `Tradition` (Italian, short: "Astrologia medica tolemaica e medievale"),
  `References` (list of strings: Ptolemy *Tetrabiblos*, Culpeper…), `Requires` (influence ids),
  `Baseline` (natal baseline strategy id: `"melothesia"` = today's sign+body logic,
  `"human-design"` = defined centers, `"none"`).
- Validation at load (in `RuleSetCatalog`, since only the API knows the registered providers):
  every `Requires` id is a registered influence; every rule's `when.kind` is emitted by one of
  the required influences (`Info.Kinds`). Fail fast like unknown zones do today.

Engine (pure, Core):

- `SchoolEngine.Evaluate(IReadOnlyList<RuleSet> schools, signals, natalBaselines)`: run
  `RuleEngine.Evaluate` per school, then combine per zone:
  `score = 1 - Π(1 - score_school)` (stays 0..1, one school alone = its own score).
  `ZoneActivation` gains `BySchool` (school id → score) and each `ZoneContribution` gains
  `SchoolId`. Combined natal baseline per zone = max over active schools.
- `Reading.RuleSetId` → `SchoolIds`. Timeline: combined zone tracks (`zone:{zone}`) **plus**
  per-school zone tracks (`zone:{school}:{zone}`) so the correlation engine compares schools
  against the journal for free (this closes the roadmap item "rule-set comparison").
- Tests: combination formula, contributions carry the school, single-school result equals
  `RuleEngine.Evaluate`.

## Step 3 — Profile schools in the DB, API

- `Profile.RuleSetId` → `Profile.Schools` (string, comma-separated ids). EF migration
  `ProfileSchools` (from `src/backend`: `dotnet tool restore`, then
  `dotnet ef migrations add ProfileSchools --project Dendera.Api --output-dir Data/Migrations`);
  migrate existing rows: `melothesia-classic` → `melothesia,biometeorology`.
- `ProfileContextFactory.Create(profile, schools?)` loads the list (unknown ids ignored; empty →
  all schools). `ProfileInput`/`ProfileDto`: `schools: string[]`.
- `PUT /api/profiles/{id}/schools` with `{ "schools": [...] }` (the fast toggle).
- `reading`, `timeline`, `correlations`: replace `?ruleset=` with optional `?schools=a,b`
  (override without saving).
- `GET /api/catalog`: `{ influences: InfluenceInfo[] + last status, schools: [{ id, name,
  description, tradition, icon, references, requires, baseline, rules: [{ id, kind, tags,
  minIntensity, zones (explicit + resolved description of zonesFrom), weight, quality, text }],
  correspondences }], zones: [{ id, label, systemic }] }`. Replace `/api/rulesets`.
- Rename the correlations comment "the Babylonian diary" to something neutral.

## Step 4 — Split melothesia, add Biometeorologia

- `rules/melothesia.json` (id `melothesia`, icon `school-melothesia`, requires `["ephemeris"]`,
  baseline `melothesia`): today's correspondences and the sky rules: transit-to-natal,
  retrograde, station, many-retrogrades, sky-aspect, moon-sign, sun-sign, full-moon, new-moon,
  perigee, eclipse-season.
- `rules/biometeorology.json` (id `biometeorology`, Italian name "Biometeorologia", icon
  `school-biometeorology`, tradition: human biometeorology — effects of weather, light and
  geomagnetism on the body; requires `["ephemeris", "gfz", "open-meteo", "schumann"]`,
  baseline `none`): light-deficit, light-change, geomagnetic, pressure-drop, pressure-rise,
  low-pressure, temperature-swing, humidity, plus the Schumann rule (step 6).
- Delete `melothesia-classic.json`.

## Step 5 — Medicina cinese (TCM)

New pure influence `TcmCalendarProvider` (Core, `computed`, primary — it is arithmetic on the
ephemeris and the clock, no external data):

- `solar-term`: the 24 *jieqi* are 15° steps of the Sun's ecliptic longitude; *Lichun* (start of
  spring) is at 315°. Tags: `term` (pinyin id, Italian name in `Lexicon`), `season`
  (spring/summer/late-summer/autumn/winter), `element` (wood/fire/earth/metal/water).
  Late summer (earth) = the ~18 days before each of the four "Li" terms (Sun 297–315°, 27–45°,
  117–135°, 207–225°); otherwise spring 315–45° wood, summer 45–135° fire, autumn 135–225° metal,
  winter 225–315° water (minus the earth windows). Intensity 1.
- `organ-clock`: local solar time = UTC + longitude/15 h; 2-hour slots: 03–05 lung,
  05–07 large-intestine, 07–09 stomach, 09–11 spleen, 11–13 heart, 13–15 small-intestine,
  15–17 bladder, 17–19 kidney, 19–21 pericardium, 21–23 triple-burner, 23–01 gallbladder,
  01–03 liver. Tags `organ`, `element`. Intensity 1 inside the slot.

Weather additions in `OpenMeteoWeatherProvider` (needed by the six climatic excesses): `wind`
(`wind_speed_10m`, gusts), `temperature` (absolute, tag `level` cold/heat), and low humidity
(tag `level` low/high on `humidity`, or a `dryness` kind). Update its `Info.Kinds`.

`rules/tcm.json` (id `tcm`, "Medicina tradizionale cinese", icon `school-tcm`, requires
`["tcm-calendar", "open-meteo"]`, baseline `none`). Organ → zone (decision 6):
liver→liver, gallbladder→liver, heart→heart, small-intestine→gut, pericardium→heart,
triple-burner→circulation+lymph, spleen→stomach+gut, stomach→stomach, lung→lungs,
large-intestine→gut, kidney→kidneys, bladder→kidneys+pelvis. Rules (texts in Italian, naming
the TCM organ, e.g. "Stagione del Legno: Fegato e Vescica biliare"):

- season-element: `solar-term` → the element's organ pair (wood liver/gallbladder, fire
  heart/small-intestine (+pericardium, triple-burner), earth spleen/stomach, metal
  lung/large-intestine, water kidney/bladder).
- organ-clock: the organ in its 2-hour peak, low weight.
- six excesses from weather: wind → head, nervous-system, liver; cold → kidneys, skeleton,
  circulation; heat → heart, mood; damp (high humidity) → stomach, gut, lymph; dryness (low
  humidity) → lungs, throat, skeleton (skin).

Tests: Sun at 315° → lichun/wood; 03:30 local solar → lung; a weather sample triggers its excess.

## Step 6 — Schumann (Biometeorologia)

`SchumannProvider` in `Dendera.Sources` (api, **derived**): `GET
https://schumannresonancelive.com/api/data.php` (JSON, CC0, no key, ~4-minute refresh). Shape
seen on 2026-11-20:

```json
{ "latest": { "station": "Tomsk TSU (56.5N 85.0E)", "lastModified": "..." },
  "activity": { "score": 52, "level": "elevated", "updated": "2026-11-20T19:03:17+00:00",
                "series": [ { "t": "20", "s": 54 }, … 24 hourly points, last = current UTC hour ],
                "source": "Tomsk TSU spectrogram analysis" } }
```

- Map `series` to the 24 UTC hours ending at `activity.updated`'s hour. Kind `schumann`,
  key `schumann`, value = score (0–100, Tomsk relative scale, **not** a physical unit),
  intensity = score/100, tag `level`. Label "Risonanza di Schumann (indice derivato)".
  Cache 15 min. Only the last 24 h exist: older dates yield no samples (count as 0 in
  correlations — document this; persisting samples for history is a follow-up).
- Info: Reliability `derived`, description says it is digitised from the Tomsk State University
  spectrograms by a third party and partly modelled.
- Rule in `biometeorology.json`: `schumann` minIntensity ~0.6 → nervous-system, sleep, mood,
  quality agitation.
- Test: parse a fixture of the JSON above.

## Step 7 — Human Design

New pure influence `HumanDesignProvider` (Core, `computed`, primary — derived from the
ephemeris; the system itself is documented, not the data):

- Rave mandala: 64 gates of 5.625°, 6 lines of 0.9375° each. Gate 41 starts at 302° (2°00'
  Aquarius); going forward in longitude the order is:
  41 19 13 49 30 55 37 63 22 36 25 17 21 51 42 3 27 24 2 23 8 20 16 35 45 12 15 52 39 53 62 56
  31 33 7 4 29 59 40 64 47 6 46 18 48 57 32 50 28 44 1 43 14 34 9 5 26 11 10 58 38 54 61 60.
  **Verify against a public reference before coding** and test: 0° Aries is in gate 25;
  302.0° → gate 41 line 1.
- Centers (9) → gates: Head 64 61 63 · Ajna 47 24 4 17 43 11 · Throat 62 23 56 35 12 45 33 8
  31 20 16 · G 7 1 13 25 46 2 15 10 · Heart/Ego 21 40 26 51 · Sacral 34 5 14 29 59 9 3 42 27 ·
  Solar plexus 36 22 37 6 49 55 30 · Spleen 48 57 44 50 32 28 18 · Root 53 60 52 19 39 41 58 38
  54 (64 gates total — assert it in a test).
- Channels (36): 1-8 2-14 3-60 4-63 5-15 6-59 7-31 9-52 10-20 10-34 10-57 11-56 12-22 13-33
  16-48 17-62 18-58 19-49 20-34 20-57 21-45 23-43 24-61 25-51 26-44 27-50 28-38 29-46 30-41
  32-54 34-57 35-36 37-40 39-55 42-53 47-64 (verify too).
- Natal: personality = birth positions; design = positions when the Sun was 88° of longitude
  before the natal Sun (search ~85–95 days back, bisection on the Sun's longitude). Bodies:
  Sun, Earth (= Sun + 180°), Moon, North node, South node (+180°), Mercury … Pluto.
  Defined centers = centers joined by a channel whose two gates are both active (personality ∪ design).
- Signals: `hd-gate` per transiting body (key `hd-gate:{body}`): tags `body`, `gate`, `line`,
  `center`, `natalCenter` (`defined`/`open`), `channel` (when the transit gate completes a
  channel with a natal gate). Needs `SignalQuery.Natal` (step 1).
- Baseline strategy `human-design`: defined centers' zones.

`rules/human-design.json` (id `human-design`, icon `school-human-design`, requires
`["human-design"]`, baseline `human-design`). Center → zones: Head→head; Ajna→head,
nervous-system; Throat→throat; G→liver, circulation; Heart/Ego→heart, stomach; Sacral→pelvis,
vitality; Solar plexus→solar-plexus, kidneys, mood; Spleen→lymph; Root→kidneys, pelvis.
Rules: transit gate in a center (body weights: Sun/Earth highest); stronger + quality
`sensitivity` ("condizionamento") when the center is open in the natal chart; strongest when
the transit completes a channel. Tradition text: "Sistema sintetico del 1987 (Ra Uru Hu):
I Ching, astrologia, chakra, Kabbalah".

## Step 8 — Frontend

- `core/icons.ts`: inline SVG icon registry (24×24, stroke `currentColor`, style of the existing
  logo: thin gold lines). One icon per school (`school-melothesia`: zodiac man / figure in a
  circle; `school-biometeorology`: cloud + barometer needle; `school-tcm`: five-phase pentagon or
  yin-yang; `school-human-design`: the nine-center bodygraph silhouette) and per influence
  (`ephemeris`: orbit; `gfz`: field lines / aurora; `open-meteo`: cloud; `schumann`: sine wave
  between two arcs; `tcm-calendar`: 24-spoke wheel; `human-design`: hexagram). An `<app-icon name>`
  component renders them.
- `models.ts` / `api.ts`: catalog types, `schools: string[]` on profile, `setSchools`, drop
  `ruleset`/`rulesets`.
- `ProfileStore`: `schools` signal from the current profile; `toggle(id)` → PUT, then pages reload
  (their data loads must depend on the signal).
- Top bar: school toggle chips (icon + short name, active/inactive state) next to the nav.
- New page **Scuole** (`/scuole`, nav entry): static catalog. Per school: icon, name, tradition,
  description, references, required influences (chips with icons), rules table (when → zones,
  weight, quality, text), correspondences. Per influence: icon, name, what it measures, provider
  kind (libreria / API / calcolato), provider name + link, license, **badge "fonte primaria" vs
  "derivata"**, kinds emitted, live status. It must be readable by someone who knows no
  astrology: one plain sentence per item.
- Adesso page: each explanation line shows its school icon; the zone detail shows the
  per-school split (`BySchool`). Source chips use influence icons; `schumann` shows the
  "derivata" badge.
- Timeline / Corrispondenze: per-school zone tracks labelled with the school icon/name.
- Profile page: replace the rule-set `<select>` with school checkboxes.
- Update `KIND_GROUP` in `symbols.ts` for the new kinds (solar-term, organ-clock, hd-gate,
  schumann, wind, temperature).
- `Lexicon.cs`: Italian names for every new id (schools, influences, kinds, TCM organs,
  elements, solar terms, HD centers).

## Step 9 — Docs

- `CLAUDE.md`: rewrite "The owner's stance" without the Babylonian insistence — the app is a
  platform to observe signals from reliable sources and interpret them through switchable
  schools, with the journal as the test. Keep: no lecturing; judge an idea within its own
  school's tradition; verify sky facts with the ephemeris. Update the pipeline section
  (influences / schools / body vocabulary, sky as a provider, multi-school combination,
  catalog endpoint, per-school tracks) and the repository layout.
- `README.md`: a **Sources** table — for every influence: what it measures, provider (library /
  API / computed), name + link, license, primary/derived — and a **Schools** table — rule file,
  tradition, references, influences required. Note the Schumann limitations (derived,
  last 24 h only).
- `docs/signals-and-rules.md`: align with the new vocabulary.

## Step 10 — Verify

- `cd src/backend && dotnet test`; `cd src/frontend && npm run build`.
- `./scripts/run.sh`, open http://localhost:4200: Scuole page renders every school and influence
  with icons and badges; toggling a chip changes the body map and the explanations; a profile
  reload keeps the choice; `/api/catalog` validates.
- `./scripts/prod-test.sh` once: the container still builds and serves.

## Follow-ups (not in this round)

- Persist Schumann (and other short-history) samples in SQLite to build history for correlations.
- Lagged correlations (0–3 days).
- NOAA SWPC solar wind / flares influence; Open-Meteo air quality and pollen.
- Dedicated TCM organ zones on the body map if the mapping proves too coarse.
