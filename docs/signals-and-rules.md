# Influences, schools, body — how to extend Dendera

Three words, used the same way in code, API, UI and docs:

- **Influence**: a signal source, an `ISignalProvider`. It produces facts (`Signal` samples) and
  never interprets them. Library, HTTP API or plain arithmetic, primary or derived data. Each
  one describes itself with an `InfluenceInfo` (name, what it measures, provider, link,
  license, `primary`/`derived`, the kinds it emits), which feeds the **Scuole** page.
- **School**: a rule file in `src/backend/rules/*.json` (`RuleSet` in code). It names the
  influences it reads (`requires`) and maps their signals onto the body. The relation between
  schools and influences is many-to-many. Each profile turns schools on and off (saved in the DB).
- **Body**: the fixed zone vocabulary (`BodyZones.cs` ↔ `body-map.ts`), the common output
  space every school writes into.

## Influences and the signal kinds they produce

| Influence (`Source`) | Kind | Key(s) | Intensity 0..1 | Main tags |
|---|---|---|---|---|
| `ephemeris` | `placement` | `placement:{body}` | always 1 | body, sign, house |
| | `retrograde` | `retrograde:{body}` | 1 (Lilly, *Christian Astrology* p. 115: retrograde −5) | body, sign |
| | `slow-motion` | `slow:{body}` | 0.4 when direct but slower than its mean (Lilly: slow −2, ratio 2/5) | body, sign |
| | `station` | `station:{body}` | 1 − \|speed\| / (10% of mean speed) | body, sign, motion |
| | `retrograde-count` | `retrograde-count` | retrograde planets / 8 | count |
| | `sky-aspect` | `sky:{a}:{aspect}:{b}` | cosine of orb fraction | bodyA, bodyB, aspect, quality, applying |
| | `transit-aspect` | `transit:{body}:{aspect}:{natalPoint}` | cosine of orb fraction | transitBody, natalPoint, natalSign, natalHouse, aspect, quality, applying |
| | `lunar-phase` | `lunar-phase` | \|cos(phase)\| (peaks at new and full) | phase, paksha (shukla/krishna), tithi (1–30) |
| | `lunar-distance` | `lunar-distance` | 1 at perigee, 0 at apogee | zone |
| | `photoperiod` | `photoperiod` | (12.5 h − daylight) / 4.5 h | — |
| | `photoperiod-change` | `photoperiod-change` | \|Δ minutes/day\| / 4 | direction |
| | `day-phase` | `day-phase` | always 1 | phase (day/night/dawn/dusk), light (day/night/twilight: Sun within ±6°) |
| | `eclipse` | `eclipse:solar`, `eclipse:lunar` | 1 at the peak, 0 at ±15 days | type, kind |
| | `decan` | `decan:{body}` | always 1 | body, sign, decan (1–3), face (Chaldean ruler) |
| | `fixed-star` | `star:{star}:{body}` | cosine within 1° | star, body, sign, royal |
| | `dignity` | `dignity:{body}` | Lilly's points / 5 (domicile 1, exaltation 0.8, detriment 1, fall 0.8) | body, sign, dignity, condition (dignified/debilitated) |
| | `solar-hour` | `solar-hour` | always 1 | hour (0–23 local solar), watch (02-06, 06-10…) |
| `planetary-hours` | `planetary-hour` | `planetary-hour` | always 1 (none in polar day/night) | ruler, dayRuler, hour (1–24), light |
| `lunar-stations` | `lunar-mansion` | `lunar-mansion` | always 1 | mansion, number (1–28) |
| | `nakshatra` | `nakshatra` | always 1 | nakshatra, number, pada, lord |
| | `sidereal-placement` | `sidereal:{graha}` | always 1 | body (7 planets, rahu, ketu), sign (sidereal, Lahiri), nakshatra |
| `life-times` (needs the natal chart) | `septennial` | `septennial` | always 1 | septennial, year |
| | `profection` | `profection` | always 1 | house, sign, lord |
| | `dasha` | `dasha` | always 1 | lord, subLord |
| `gfz` | `geomagnetic` | `kp` | (Kp − 4) / 5: 0.2 at Kp 5 = G1 (NOAA scale), 1 at G5 | level, status |
| `open-meteo` | `pressure` | `pressure` | (1013 − sea-level hPa) / 10 (Okuma 2015: 6–10 hPa below 1013) | — |
| | `pressure-change` | `pressure-change` | \|Δ24h\| / 10 hPa (Kimoto 2011: drops > 5 hPa) | direction (drop/rise) |
| | `temperature` | `temperature` | cold: (10 − °C) / 15, heat: (°C − 26) / 12 | level (cold/mild/heat) |
| | `temperature-swing` | `temperature-swing` | \|mean of last 24 h − mean of the 24 h before\| / 10 °C (Guo 2011: > 3 °C) | direction |
| | `humidity` | `humidity` | (RH − 60) / 40 | level (low/normal/high) |
| | `dryness` | `dryness` | (45 − RH) / 30, only below 45% | level |
| | `muggy` | `muggy` | grows with heat above 26 °C and humidity above 55% | level |
| | `wind` | `wind` | (gusts − 20) / 50 km/h | level (calm/breezy/windy/strong) |
| `noaa-swpc` | `solar-wind` | `solar-wind` | (km/s − 400) / 400 | level |
| | `imf-bz` | `imf-bz` | −Bz / 10 nT (south only) | direction |
| | `xray-flare` | `xray` | (log10 flux + 6) / 2: C1 0, M1 0.5, X1 1 | class |
| `air-quality` | `air-quality` | `air:{pollutant}` | value / (2 × WHO 2021 guideline) | pollutant, level |
| | `pollen` | `pollen:{type}` | grains / (2 × "high" count) | type |
| | `uv` | `uv` | UV index / 11 | level |
| `decumbiture` (journal onsets) | `illness-day` | `illness-day:{onset}` | always 1 | day, critical (Hippocrates 4, 7, 11, 14, 17, 20) |
| | `decumbiture-moon` | `decumbiture-moon:{onset}` | cosine within 6° | moonAspect, phase (crisis/judicial), sign, day |
| `schumann` (**derived**) | `schumann` | `schumann` | Tomsk relative index / 100 | level (latest point only) |
| `tcm-calendar` | `solar-term` | `solar-term:{element}` | always 1 | term, season, element |
| | `organ-clock` | `organ-clock:{organ}` | always 1 | organ, element |
| | `ganzhi` | `ganzhi` | always 1 | yearStem, yearBranch, dayStem, dayBranch, hourBranch, dayElement, yearElement |
| `human-design` | `hd-gate` | `hd-gate:{body}` | always 1 | body, gate, line, center, natalCenter, channel, channelCenter |

Notes:

- **Aspects** are facts of the ephemeris influence, not of a school: `AspectCatalog.Default`
  in `Astro/Aspects.cs` (5 Ptolemaic + 3 minor). Orbs belong to the **planets** (al-Bīrūnī, *Book of
  Instruction* §§ 436–437; Lilly, *Christian Astrology* p. 107: Sun 15°, Moon 12°, Jupiter and Saturn 9°,
  Mars 8°, Venus and Mercury 7°, others 5°). Sky aspects (major only): sum of the two moieties (half orbs);
  transits: the transiting planet's moiety only; minor aspects: half of that. Sources of every number:
  `docs/plans/calibration.md`, step 1.
  A school that wants only Ptolemaic aspects filters on the `aspect` tag in its rules.
- **Frame-aligned influences** (ephemeris, tcm-calendar, human-design) sample exactly on the
  query frames and have `SampleValidity = 0`, so a vanished aspect does not linger into the
  next frame. External ones keep a sample valid for a few hours (`SampleValidity`).
- **Schumann** is derived data: a third party digitises the Tomsk State University
  spectrograms and partly models them into a 0–100 score, not a physical unit. The feed only
  holds the last 24 hours, so older days have no samples and count as 0 in correlations.
- **TCM calendar**: 24 solar terms in 15° steps of the Sun from Lichun (315°); late summer
  (earth) is 297–315°, 27–45°, 117–135° and 207–225°. The organ clock uses local mean solar
  time = UTC + longitude / 15 h, two hours per organ from 03:00 (lung).
- **Human Design**: gate 41 starts at 302° (2°00' Aquarius), 64 gates of 5.625°, 6 lines of
  0.9375°. The natal design adds the positions from when the Sun was 88° earlier.

## Adding an influence

1. Implement `ISignalProvider`: pure ones in `Dendera.Core/Signals` (no I/O, `Task.FromResult`),
   impure ones in `Dendera.Sources` (cache HTTP calls with `IMemoryCache`).
   - `Info`: an `InfluenceInfo` with Italian name and description and the kinds it emits.
     Mark third-party processed data as `Reliabilities.Derived`.
   - `SampleValidity`: how long a sample stays active; zero for frame-aligned sources.
   - `GetAsync(SignalQuery)`: samples between `From − SampleValidity` and `To`, at the query
     location. `query.Frames()` gives the evaluation instants, `query.Natal` the birth chart.
2. Add the kind constant to `SignalKinds`, Italian names to `Lexicon.cs` (`Kinds`, tag values),
   a group in `symbols.ts` (`KIND_GROUP`) and a colour in `pages/timeline.ts` (`KIND_COLOR`).
3. Register it in `Dendera.Api/Program.cs` (and in `tests/…/Influences.cs`, which validates the
   school files in tests). Map its icon id to a Material Symbol in `src/frontend/src/app/core/icons.ts`.
4. Read it from a school: add its id to `requires` and write rules for its kinds.

A signal does nothing on the body until a rule matches it, but it always appears on the
timeline and in the correlation engine.

## Writing a school

`src/backend/rules/melothesia.json` is the reference. Shape:

```jsonc
{
  "id": "my-school", "version": "1.0.0", "name": "…", "description": "…",  // Italian, plain words
  "icon": "school-my-school",                 // id mapped to a Material Symbol in src/frontend/src/app/core/icons.ts
  "tradition": "…",                           // Italian, short
  "references": ["Tolomeo, Tetrabiblos", "…"],
  "requires": ["ephemeris"],                  // influence ids; every rule kind must be emitted by one of them
  "baseline": "melothesia",                   // natal emphasis: melothesia | human-design | none
  "correspondences": { "virgo": ["gut", "solar-plexus"], "mercury": ["nervous-system"] },
  "natalAmplification": 0.5,
  "saturation": 2.5,                          // fallback scale only (no up-to-date quantile table) and second pass
  "rules": [
    {
      "id": "transit-to-natal",
      "when": { "kind": "transit-aspect", "tags": { "aspect": ["square", "opposition"] }, "minIntensity": 0.2 },
      "effect": {
        "zonesFrom": ["transitBody", "natalPoint", "natalSign"],   // tag values mapped through correspondences
        "zones": ["vitality"],                                       // explicit zones
        "weight": 1.0,
        "quality": null,                                             // null = take the signal's "quality" tag
        "tagWeights": { "transitBody": { "saturn": 1.0, "moon": 0.4, "*": 0.5 } }
      },
      "text": "{transitBody} in {aspect} con {natalPoint} natale"   // {tag}, {label}, {detail}
    }
  ]
}
```

In `when.tags`, `"*"` accepts any value (the tag only has to be present), e.g.
`{ "channel": ["*"] }`.

Validation at startup (fail fast): unknown zones, duplicate rule ids, unknown baseline
strategy, `requires` naming an influence that is not registered, rules on a kind that none of
the required influences emits.

Valid nodes (`BodyZones.cs`), by layer:
- soma — brain, head, eyes, ears, nose, mouth, throat, shoulders, lungs, heart, stomach,
  solar-plexus, liver, gut, kidneys, pelvis, thighs, knees, calves, feet;
- systems — nervous-system, circulation, skeleton, skin, sinews, muscles, lymph, sleep, vitality;
- temperament — sanguine, choleric, melancholic, phlegmatic;
- emotions — anger, joy, worry, pensiveness, grief, fear, fright;
- mind — imagination, reason, memory, mood;
- zangfu — zf-liver, zf-gallbladder, zf-heart, zf-small-intestine, zf-pericardium, zf-triple-burner,
  zf-spleen, zf-stomach, zf-lung, zf-large-intestine, zf-kidney, zf-bladder;
- spirits — shen, hun, po, yi, zhi;
- substances — qi, xue, yin, yang, jing, jinye;
- chakras — muladhara, svadhisthana, manipura, anahata, vishuddha, ajna, sahasrara;
- nadis — ida, pingala, sushumna.

Schools may also carry `"tables"` (title, columns, rows): reference data of the tradition shown on
the Scuole page (e.g. the five-element correspondences).

**Relations between nodes** are rules of kind `node`, evaluated in a second pass on the nodes
the same school activated (intensity = the node's internal score `1 − e^(−raw/saturation)`, not its
rarity, `quality` tag = its dominant quality):

```jsonc
{ "id": "emotion-anger",
  "when": { "kind": "node", "tags": { "node": ["liver"] }, "minIntensity": 0.2 },
  "effect": { "zones": ["anger"], "weight": 0.6 },          // quality null = the organ's quality
  "text": "Fegato sollecitato ({detail}): la Collera abita il Fegato" }
```

One pass only: node rules never see nodes produced by other node rules. A school's relations
only read that school's own activations, so switching a school off removes its relations too.

Known qualities (colours in the UI): tension, flow, fusion, inward, agitation, depletion,
pressure, sensitivity, neutral. Any other string works but is drawn grey.

## What a score means: rarity

A school's score on a node is its **rarity**: the share of reference moments in which that school
puts a strictly lower raw sum on that node. For schools that read only the sky and the calendars the
reference is personal: the 90 years from the person's birth on their own chart (`PersonalReference`),
so a score reads "more solicited than in N% of your moments". 50% is an ordinary moment, 95% a rare
one; a node nothing touches is 0, not "its usual". Per-quality scores split the rarity by each
quality's share of the raw sum. The reference distributions are quantile tables
(`rules/<school>.quantiles.json`) written by the calibration ritual
(`dotnet run --project Dendera.Api -- calibrate`, see CLAUDE.md and `docs/calibration-report.md`).
Schools reading the local weather have one table per climate zone; the person's place picks the
nearest reference place. A school whose table is missing or stale uses `1 − e^(−raw/saturation)`
and shows "da ricalibrare". Any change to a rule file's rules, weights or correspondences needs
the ritual again (a unit test enforces it).

## Several schools at once

Each active school is evaluated on its own (`RuleEngine`, its own natal baseline), then
`SchoolEngine` combines them per node by consensus: the mean of the scores of the schools that
cover that node (`RuleSet.Covers`), zeros included. A single school gives exactly its own score. `ZoneActivation.BySchool` keeps the split and
each contribution carries its `SchoolId`. The combined natal baseline is the maximum across schools.

The timeline has combined zone tracks (`zone:{zone}`) and, with two or more schools,
per-school tracks (`zone:{school}:{zone}`). The correlation engine tests them all against the
journal, so you can see which school describes you best.

The active schools are stored on the profile (`PUT /api/profiles/{id}/schools`).
`?schools=a,b` on `/reading`, `/timeline` and `/correlations` overrides them without saving.
`GET /api/catalog` lists influences (with their last status), schools (rules,
correspondences, references) and zones.
