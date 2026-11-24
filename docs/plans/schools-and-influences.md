# Plan — more influences, richer schools, new schools

Status: **partly done** (2026-11-21): steps 1–5 done for the pure influences, lineages and seven new schools; see "Done and left" at the end. Follows `docs/plans/calibration.md` (done): every new
number of a tradition gets a source next to it, every school gets its calibration (population
tables by the ritual, personal rarity at runtime).

## Handover (read first if you start from a fresh chat)

- Read `CLAUDE.md`, `docs/signals-and-rules.md`, `docs/plans/calibration.md` (how a score is a rarity,
  the ritual, the manifest of the fixtures, personal rarity).
- Rules of the house: never commit; code/docs in English, UI and rule texts in Italian; `Dendera.Core`
  pure; every step ends green (`dotnet test`, `npm run build`), and re-runs the ritual
  (`dotnet run --project Dendera.Api -- calibrate`) when a school or an influence changes; bump
  `ReferenceSet.SignalsVersion` when an influence changes its intensities or kinds.
- A new body layer means changing together: `BodyZones.cs`, `Lexicon.cs`, `body-map.ts` (2D anchors),
  `body-3d/anchors-3d.ts` (and `channels-3d.ts` for paths), `symbols.ts`, `index.html` icon names.
- As in the calibration plan, each step that brings a tradition's numbers starts with a **research pass**
  reported to the owner (author, work, chapter, page) before anything is applied.

## Principles

1. **A school must move in time.** Dendera shows how influences land *now*; a school that is only a fixed
   map symbol → organ lights the same nodes forever. Every new school must read at least one signal that
   changes (sky, calendar, weather, the person's age…).
2. **Observation stays separate from interpretation.** New facts go into influences (cited, primary or
   derived); new readings go into schools.
3. **Overlap is data, if lineages are declared.** Several schools landing on the same node is the
   interesting cross-check the app is for — but only when the schools are independent. Paracelsus and
   Steiner took their planet → organ map from the Ptolemaic tradition: their agreement with Melotesia is
   one idea counted three times, not three confirmations. So every school declares its **lineage**
   (`lineage`: e.g. `hellenistic-astrology`, `galenic`, `chinese-classics`, `vedic`, `tantric`,
   `hermetic`, `modern-science`, `human-design`) and its **parents** (lineages it borrows from). The UI
   shows agreement **between independent lineages** separately from agreement inside one lineage.
4. **The journal is the test.** More schools = more per-school tracks for the correlation engine: the
   point is to see which tradition describes a person best, not to add light.
5. **Weight against genealogy, not against overlap.** The consensus mean already keeps many schools
   from inflating a node; lineage-aware consensus (one vote per lineage, then the mean) is an option
   to decide in step 5.

## A. Influences (facts)

| Influence | Kind(s) | Source | Pure? | Notes |
|---|---|---|---|---|
| **Planetary hours** | `planetary-hour` (ruler, day/night, hour 1–12) | Chaldean order; day of the week; sunrise/sunset of the place (we have the Sun's altitude) | yes | Medieval medicine timed remedies by them (to cite: e.g. Picatrix; Lilly CA I) |
| **Lunar mansions** | `lunar-mansion` (1–28) | Arabic manāzil / Indian nakṣatra (27) | yes | Two independent lineages on the same Moon: a natural cross-check |
| **Decans and faces** | `decan` per body (36) | Egyptian decans, Ptolemaic faces (Chaldean order) | yes | The Dendera ceiling itself; Melotesia enrichment |
| **Fixed stars** | `fixed-star` conjunctions (body/natal point, orb 1°) | Royal stars and Behenian stars (Agrippa) with precessed positions | yes | Short list, cited per star |
| **Tattva (Svara yoga)** | `tattva` (akasha, vayu, tejas, apas, prithvi) | Shiva Swarodaya: from sunrise, cycles of the five tattvas | yes | Exact durations to verify in the text |
| **Ganzhi calendar** | `ganzhi` (year/month/day/hour stem and branch) | Chinese sexagenary calendar | yes | TCM enrichment (Ziwu liuzhu, Wu yun liu qi) |
| **Wu yun liu qi** | `five-movements`, `six-qi` (yearly host and guest qi) | Suwen 66–74 (the "seven great treatises") | yes | Yearly climate of TCM, fixed by the year's stem/branch |
| **Septennials** | `life-cycle` (age, septennial, phase within it) | Steiner (and earlier: Solon, the ages of man) | yes, per person | First personal-time influence; profections come with it (below) |
| **Profections** | `profection` (year's lord, activated house and sign) | Hellenistic: Vettius Valens, Ptolemy IV.10 | yes, per person | Annual time-lord; monthly profections too |
| **Dashas (Vimshottari)** | `dasha` (maha/antar lord) | Parashara, BPHS | yes, per person | Sidereal Moon at birth; Jyotish school |
| **Solar wind, flares** | `solar-wind`, `x-ray-flare`, `proton-event` | NOAA SWPC (JSON, free) | no | Roadmap; history for the ritual: OMNI (NASA, from 1963) |
| **Air quality, pollen** | `air-quality` (PM2.5, PM10, O3, NO2), `pollen` | Open-Meteo air quality API | no | Roadmap; Europe pollen only; history from 2013 for the ritual |
| **Schumann persisted** | same `schumann` | our own SQLite samples | no | Roadmap; once months exist it joins the ritual |
| **UV index, sunshine** | `uv`, `sunshine` | Open-Meteo | no | Light for biometeorology beyond day length |

Influences that need a person (septennials, profections, dashas) are computed from the natal chart like
transits are: pure, in the Core.

## B. Enrich the schools we have

- **Melotesia** — decans and faces (zones of the sign's thirds), fixed stars on luminaries/Ascendant,
  planetary hours (the hour's ruler on its organs), lunar mansions; Culpeper's decumbiture (the Moon's
  aspects from the onset of an illness: an event the journal could record).
- **Yoga tantrico** — the five **vayu** (prana, apana, samana, udana, vyana) as a layer; the **tattva**
  cycle (Shiva Swarodaya) from sunrise; nakshatra of the Moon on the chakras; mantra/deity per chakra as
  reference tables. Keeps Frawley's planet → chakra as the declared modern part.
- **Medicina cinese** — ganzhi day and hour (Ziwu liuzhu "open points"), yearly wu yun liu qi, the 12
  divergent and 8 extraordinary vessels (as paths in 3D, optional); TCM excesses with real thresholds
  (cited, like biometeorology now).
- **Biometeorologia** — solar wind and flares, air quality, pollen, UV; foehn/sirocco detection from
  wind direction and humidity; Schumann once persisted.
- **Human Design** — Rave cycles (Saturn return ~29, Uranus opposition ~42, Chiron return ~50) from the
  person's age; the transit of the 64 gates through the year (the "Rave New Year"); variables only if
  sources are clear.

## C. New schools — recommended

Ordered by what they add to the comparison (independent lineage, dynamic input), not by popularity.

1. **Astrologia tradizionale (ellenistica e medievale)** — yes, alongside Melotesia, not instead.
   Melotesia reads the sky *on the body*; classical astrology reads it on the **person's life and
   temperament**: houses as life domains, sect, dignities (Lilly's table, already cited), profections and
   time-lords, solar return. It needs a new layer **domini** (the 12 houses: life, resources, siblings…)
   plus the existing temperament and mind layers. Same lineage as Melotesia (declared), different output:
   its value is the journal, not the cross-check. Sources: Valens, Ptolemy, Dorotheus, Abū Ma'shar, Bonatti, Lilly.
2. **Jyotish (astrologia vedica)** — the strongest cross-check of Western astrology: **sidereal**
   zodiac (Lahiri ayanamsha), nakshatras, dashas, its own body map (Kalapurusha: signs and houses on the
   body, Parashara) and graha → dosha/dhatu. Same sky, different zodiac and lineage: when Melotesia and
   Jyotish agree on a node, that is a real crossing. Sources: BPHS, Varahamihira (Brihat Jataka).
3. **Ayurveda** — the independent medical tradition to set against TCM and Galenic medicine: dosha
   clock of the day (Kapha 6–10, Pitta 10–14, Vata 14–18 and again at night), ritu (six seasons) and
   ritucharya, weather on the doshas, agni. Reads calendar, sun and weather: dynamic, calibrated per
   climate like TCM. New layers: dosha (3), dhatu (7), possibly srotas. Sources: Charaka Samhita
   (Sutrasthana 6 on seasons), Ashtanga Hridaya (Sutrasthana 1–3).
4. **Medicina galenica e ippocratica** — the four humours already exist as a layer, but only Melotesia
   writes into them through the planets. Galenic medicine moves them by **season, age, time of day and the
   six non-naturals** (air, food, sleep, motion, evacuation, passions): Hippocrates *Nature of Man* 7
   (seasons ↔ humours), Galen, Avicenna's *Canon* I. Independent of astrology (declare it: later
   astrological medicine borrowed it). Weather and calendar → humours; the journal could record the
   non-naturals (sleep, food) as inputs.
5. **Cabala ermetica (Adam Kadmon)** — the ten Sephiroth as a layer, the seven planets on their
   Sephiroth (Agrippa, *De occulta philosophia* III; Golden Dawn), the paths of the Tree on the signs and
   elements. Dynamic through the planets (dignity, retrograde, aspects). Hermetic lineage, parent:
   astrology — declared.
6. **Anatomia occulta di Steiner (con Paracelso)** — one school, not two: the planetary organs are the
   Ptolemaic ones (declared parent), so the value is in what is new: the four members (physical,
   etheric, astral, I) as a layer, the **septennials** of the person's life, the tria prima (Sulphur,
   Mercury, Salt) of Paracelsus as a layer, the weekday planets. Sources: Steiner, *Occult Physiology*
   (GA 128), *Spiritual Science and Medicine* (GA 312); Paracelsus, *Opus paramirum*.
7. **Cronobiologia (scienza moderna)** — the modern lineage, as a control: circadian phase from light and
   the hour (melatonin, cortisol), chronotype from the journal's sleep, social jet lag, seasonal light.
   If an old school and chronobiology agree with the journal, that is the interesting comparison.
   Sources: reviews in chronobiology (to research in step 1).

Not recommended as a school: **doctrine of signatures** — it maps plants and minerals, not time; it can
live as reference tables (e.g. in Melotesia or Paracelsus: herbs per planet, Culpeper's *Herbal*).
**Theosophy (Blavatsky)** — the seven principles add little that is dynamic beyond Steiner; can be a
reference table in the Steiner school.

## D. Comparing sources in the UI

- `lineage` and `parents` in every rule file (validated), shown on the Scuole page as a small genealogy.
- Zone detail: "concordano N tradizioni indipendenti" vs "N scuole della stessa tradizione".
- Optional lineage-aware consensus (one vote per lineage), switchable, to compare with the plain mean.
- Insights: per-school correlations grouped by lineage, so the journal can say which tradition fits.

## Steps

1. Research pass for the influences of A that are pure (planetary hours, mansions, decans, fixed stars,
   tattva, ganzhi, wu yun liu qi, septennials, profections, dashas): sources with chapter and page; owner
   reviews.
2. Implement those influences (Core, tests, `SignalsVersion` bump, ritual).
3. `lineage`/`parents` in the school model + UI genealogy + "independent lineages agree" in the zone
   detail.
4. Enrich the existing schools (B), one school per step, each with its research pass and ritual.
5. New schools (C) in this order: Astrologia tradizionale, Jyotish, Ayurveda, Galenic medicine, Cabala,
   Steiner–Paracelsus, Cronobiologia — each: research pass → new layers (if any) → rule file → ritual →
   journal metrics if it needs new inputs.
6. External influences of A (NOAA SWPC, air quality/pollen, UV, Schumann persisted) with fixtures and
   manifest entries for the ritual.
7. Docs: CLAUDE.md, `docs/signals-and-rules.md`, Scuole page texts.

## Step 1 — sources of the pure influences (2026-11-21)

Same convention as the calibration plan: **verified** = checked against a text that quotes the source;
**from the tradition, to check** = standard values recalled, written in the code as such until checked
against the page. Every number below is also written next to its code.

- **Planetary hours** — Chaldean order Saturn, Jupiter, Mars, Sun, Venus, Mercury, Moon; the day belongs to
  the planet of its first hour, from sunrise (Vettius Valens, *Anthology* I; Picatrix II; Lilly, *Christian
  Astrology* I, the table of planetary hours): 12 unequal hours from sunrise to sunset and 12 from sunset to
  sunrise. Weekday rulers Sun, Moon, Mars, Mercury, Jupiter, Venus, Saturn (Sunday → Saturday). Sunrise and
  sunset at −0.833° (standard refraction and the Sun's radius). *From the tradition, to check: chapter numbers.*
- **Lunar mansions (manāzil)** — 28 equal mansions of 12°51′26″ from 0° Aries (tropical), names after
  Agrippa, *De occulta philosophia* II.33 (from Picatrix I.4). *To check: Agrippa's spellings.*
- **Nakshatra** — 27 of 13°20′ on the **sidereal** zodiac, Lahiri ayanamsha (Indian Calendar Reform
  Committee, 1955): 23°51′ at J2000, +50.29″ a year (linear: a few arcminutes over 1900–2100); 4 padas of
  3°20′; Vimshottari lords in the order Ketu, Venus, Sun, Moon, Mars, Rahu, Jupiter, Saturn, Mercury
  (Parashara, *Brihat Parashara Hora Shastra*, Vimshottari dasha chapter). *To check: chapter number.*
- **Decans and faces** — 36 of 10°; faces ruled in Chaldean order starting with Mars at 0° Aries
  (Ptolemy's "faces", Tetrabiblos I.24 mentions them as Chaldean; Lilly's table of essential dignities,
  CA p. 104, gives face +1). *Verified in part: Lilly's table (skyscript), face +1.*
- **Fixed stars** — the four royal stars (Aldebaran, Regulus, Antares, Fomalhaut) and Agrippa's fifteen
  Behenian stars (*De occulta philosophia* II.47–52, after Hermes): J2000 tropical longitudes from standard
  tables, precessed at 50.29″/year; conjunction orb 1° (convention; the tradition varies). *To check: the
  longitudes against a catalogue (e.g. Robson, Fixed Stars and Constellations in Astrology, 1923).*
- **Ganzhi** — sexagenary day count: index = (JDN + 49) mod 60 (0 = jiazi), checked on 1 Jan 2000 = wuwu
  (54); the day changes at 23:00 local solar time (start of the zi hour); hour branch every two hours from
  23:00; year from Lichun (Sun at 315°), index (year − 4) mod 60. *Verified: the 2000-01-01 anchor.*
- **Septennials** — seven-year periods of life (Solon, elegy on the ten ages; Steiner, *The Education of the
  Child*, 1907). Age from the birth instant.
- **Profections** — annual: the Ascendant's sign advances one sign a year, its lord is the year's lord
  (Vettius Valens, *Anthology* IV; Ptolemy IV.10 for the time-lords; whole-sign houses). Traditional
  rulers (no outer planets).
- **Vimshottari dasha** — 120 years: Ketu 7, Venus 20, Sun 6, Moon 10, Mars 7, Rahu 18, Jupiter 16, Saturn 19,
  Mercury 17; the first period starts at the birth nakshatra's lord, its balance is the part of the
  nakshatra the Moon has still to cross; antardashas in the same order from the mahadasha lord, length
  proportional (BPHS). Year = 365.25 days (some schools use 360: convention, said so).
- **Tattva (Svara yoga)** — deferred: the durations and order differ between Shiva Swarodaya as edited by
  Rama Prasad (*Nature's Finer Forces*, 1890) and the later tattva-tide tables; needs a reading of the text
  before it is coded.
- **Wu yun liu qi** — deferred to the TCM enrichment step (Suwen 66–74 need a careful reading).

## Open decisions (owner)

- Order of the new schools (proposed above) and whether all seven are wanted.
- New body layers: domains (houses), dosha/dhatu, sephiroth, the four members, tria prima, vayu — the
  body view gets crowded; maybe layers per school in the 3D view.
- Lineage-aware consensus: default or only a comparison view.
- Journal inputs for schools that need them (non-naturals: food, sleep times; illness onset for
  decumbiture).

## Done and left (2026-11-21)

Done:
- Pure influences: **planetary hours**, **lunar stations** (mansions, nakshatras, sidereal placements of the nine
  grahas), **life times** (septennials, profections, Vimshottari dashas); in the ephemeris: **decans**, **fixed
  stars**, **dignity** (domicile, exaltation, detriment, fall), **solar hour**; in the TCM calendar: **ganzhi**.
  `SignalsVersion` 3. Tests in `TraditionalTimeTests`.
- `lineage` / `parents` in every school (validated), shown on Scuole; the zone detail says whether the schools that
  see a node above 50% belong to independent traditions or to one family.
- Enriched: Melotesia (planetary hour, fixed stars, profection), Yoga tantrico (nakshatra and dasha lords on the
  chakras), MTC (Najia: the day's stem on its organ).
- New schools: Astrologia tradizionale, Jyotish, Ayurveda, Medicina galenica, Cabala ermetica, Antroposofia
  (Steiner–Paracelso; tria prima as a table), Cronobiologia. New layers: domains, doshas, sephiroth (3D anchors),
  members. Ritual re-run (12 tables); personal reference ~5 s for all twelve on first use.
- Check (profile 2, all twelve schools): 80 of 106 nodes touched; over 8 random instants 2000–2040, 58% of the
  touched nodes under 50% and 5 at 90%+.

Left:
- External influences: NOAA SWPC (solar wind, flares), Open-Meteo air quality, pollen, UV; persisted Schumann
  (with fixtures and manifest entries for the ritual).
- Tattva (Svara yoga) and wu yun liu qi: need a reading of the texts first.
- Enrichments not done: Melotesia decumbiture (needs an illness-onset journal entry), TCM extraordinary vessels,
  Human Design Rave cycles, biometeorology foehn/sirocco.
- Lineage-aware consensus (one vote per lineage) as an option; Insights grouped by lineage.
- 2D body map anchors for the new layers (they show as gauges); 3D anchors only for the sephiroth.
- The "to check" citations above (page numbers, star longitudes, Najia source, chronobiology reviews) and every
  weight marked "convention" in the rule files.

## Citation check (2026-11-21)

- **Verified**: Agrippa, *De occulta philosophia* II.33 ("Of the twenty eight Mansions of the Moon"), II.47
  ("Of the Images of the fixed Behenian Stars"; the earlier "II.47–52" was wrong), III.10 (the ten Sephiroth) and
  III.13 ("Of the members of God, and of their influence on our members", now cited by Cabala); Hippocrates,
  *Prognostic* 20 (critical days 4, 7, 11, 14, 17, 20, then 34, 40, 60); Cajochen et al., *Current Biology* 23(15),
  2013; the fixed-star J2000 longitudes, against 2024–2026 published positions (differences = precession).
- **Still to check**: the Najia stem → organ rhyme (甲胆乙肝丙小肠…): attributed to the Ziwu liuzhu literature
  (He Ruoyu 1153, Xu Feng 1439), edition not found; Culpeper's judicial days at 45°/135° in *Astrological Judgement
  of Diseases* (crises at square and opposition are standard, the octiles need the text); Czeisler & Gooley 2007;
  BPHS chapter numbers (editions number them differently); Agrippa I.32 for the stars' stones and herbs.

## Done and left, second round (2026-11-21)

Done: NOAA SWPC (solar wind, Bz, X-ray flares) and Open-Meteo air quality (WHO 2021 guidelines), pollen (UK Met
Office "high" for grass and birch, convention for the rest) and UV (WHO index), read by Biometeorologia 1.3.0;
sample store in SQLite (`Samples`, migration `SampleStoreAndDecumbiture`) for Schumann and NOAA, collected hourly;
decumbiture (journal field "Inizio di un malessere"), read by Melotesia 2.6.0 (Culpeper's Moon) and Medicina galenica
1.1.0 (Hippocratic critical days); consensus "one vote per family" (`?consensus=lineage`, toggle on Scuole; families
by one-step parents, `SchoolEngine.Families`), lineage summary on Corrispondenze; citation check above.

Left out on purpose (ambiguous sources): tattva, wu yun liu qi, TCM extraordinary vessels (Linggui bafa vs Feiteng
bafa), Human Design Rave cycles (no body mapping in Ra's texts). Not in the calibration reference: Schumann, NOAA,
air quality, decumbiture (history too short or event-driven).
