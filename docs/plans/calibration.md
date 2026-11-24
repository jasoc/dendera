# Plan — calibration of the schools

Status: **done** (2026-11-21). Steps 1–6 implemented; results below. Method: **rarity** (percentile against a synthetic
reference "sky of always"), produced by a release ritual. Minor aspects are kept. Every number of
a tradition must end up with a source next to it in the rule file; every number of the instrument
comes from the ritual, versioned with the rules.

## Handover (read first if you start from a fresh chat)

- Read `CLAUDE.md` (project brief, stance, conventions) and `docs/signals-and-rules.md`
  (influences, schools, body model, node rules, consensus). Background: `docs/plans/schools.md`
  and `docs/plans/body-psyche-3d.md` (both done).
- Rules of the house: never `git commit`/`git push`; code and docs in English, UI and rule texts in
  Italian; `Dendera.Core` stays pure (no network, clock or DB: pass data in, seed randomness);
  every step ends green: `dotnet test` in `src/backend`, `npm run build` in `src/frontend`.
- Toolchain: .NET SDK in `~/.dotnet` (`export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`),
  Node 24.22.3 via nvm. Dev stack: `./scripts/run.sh` (API :5180, UI :4200); if it is already
  running, do not start a second one, it hot-reloads (restart it after large backend changes:
  `dotnet watch` can crash on many edits).
- Where things are today:
  - score formula: `src/backend/Dendera.Core/Rules/RuleEngine.cs` (`Build`: `1 − e^(−raw/Saturation)`,
    also per quality), `RuleSet.Saturation` in `Rules/RuleSet.cs`, the field `"saturation": 2.5`
    in every `src/backend/rules/*.json`;
  - consensus between schools: `Rules/SchoolEngine.cs` (`Combine`, `RuleSet.Covers`);
  - second pass (rules of kind `node`): `RuleEngine.Evaluate` turns first-pass node *scores* into
    signals; their `minIntensity` thresholds (0.2, Ke cycle 0.6) were set on today's scale;
  - aspects and orbs: `Dendera.Core/Astro/Aspects.cs` (`AspectCatalog`, `AspectFinder`);
  - UI wording "intensità dell'influsso": `src/frontend/src/app/components/zone-detail.ts` (lines
    ~30–35) and `src/frontend/src/app/pages/now.ts` (~93);
  - the schools loader: `src/backend/Dendera.Api/Services/RuleSetCatalog.cs`.
- Detail decisions already taken, so you do not have to guess:
  - **zero stays zero**: rarity = share of reference samples with raw **strictly lower** than the
    current raw, so a node nothing touches scores 0, not "its usual 40%";
  - **second pass on raw, not on rarity**: node signals of the second pass keep using today's
    internal scale (`1 − e^(−raw/2.5)`), so the existing `minIntensity` thresholds keep their
    meaning; rarity is applied to the final raw of every node, after the second pass;
  - **per-quality scores**: no tables per quality; quality score = node rarity × (raw of that
    quality / raw of the node);
  - consensus mean, timeline tracks and correlations simply consume the new scores.
- Still open (decide with the owner at step 3): Biometeorology tables per climate zone or per place.
- The owner wants to read the sources: in step 1 report each value with author, work, chapter (and
  page or section) before applying it.

## Why

Since the schools combine by consensus (mean of the schools that cover a node), several schools
no longer inflate a node. On the owner's profile (2026-11-20, five schools) the mean node score is
26%, one node of 77 is above 80% and twelve are above 50%. What is still off is **inside each
school**: Melotesia alone keeps the calves at 94% and the temperaments at 65–77% because it sums
many weak transits into one saturated number. The saturation `1 − e^(−raw/2.5)` is a hand-set scale
that says nothing about how *usual* a level is.

## What a score means after calibration: rarity

For each school and node the score is the **percentile of the current raw sum** within the
reference distribution of that school and node: "more solicited than in 87% of the moments of the
reference sky". 50% is an ordinary moment, 95% is rare. The UI wording changes from
"intensità dell'influsso" to **"rarità dell'influsso"**. No hand-set scale is left; the old
`1 − e^(−raw/saturation)` stays only as a fallback for a school without quantile tables.

Combination across schools (consensus mean) and per-quality scores work the same way on the
rarity values. This is a convention for reading, not a claim about the body: the journal stays the
test.

## The reference sky (synthetic, no personal data)

The sky is the same for everyone; only the natal-dependent parts (transits, Human Design, natal
baseline) vary by person. So the reference is built from **synthetic charts**, never from real
users:

- **Charts**: ~500 synthetic births, deterministic (seeded), spread over 1930–2010, all hours, and
  latitudes 0–60° N/S (houses and daylight depend on latitude).
- **Time**: ~50 000 **random instants over 1900–2100** (seeded), not a dense series over a few
  years. What matters is covering the long cycles (see below), not the number of points.
- **Weather and Kp**: Kp history from GFZ (1932 →), weather from the Open-Meteo archive (1940 →),
  downloaded once into fixtures for a set of reference places. **Biometeorology is relative to the
  local climate**: a summer day in Catania and one in Oslo are compared with their own climate, so
  its tables are per climate zone (or per place, cached on first use: to be decided in step 3).
- **Schumann**: the feed only has 24 hours, so it cannot be calibrated; it keeps its fixed
  threshold until samples are persisted (follow-up).

## How precision grows with data

- The error of a percentile shrinks like 1/√(effective number of samples), so going from 10 000 to
  50 000 samples halves it, and beyond that the gain is small. Tails (95th, 99th percentile) need
  more samples than the median.
- The samples are not independent: Saturn stays in a transit for months, so two instants a day
  apart say almost the same thing. **The span of time counts more than the sampling rate**: the
  reference must cover the long cycles: Moon 27–29 d, Sun 1 y, lunar nodes and eclipses 18–19 y,
  Jupiter 12 y, Saturn 29 y, Uranus 84 y; Neptune (165 y) and Pluto (248 y) are covered only in
  part by 200 years, which is accepted and written in the report.
- More synthetic charts improve the transit-to-natal part (every chart meets the sky at different
  points); 500 is a starting point, the report shows how stable the quantiles are when it doubles.
- The ephemeris (Astronomy Engine) is valid well beyond 1900–2100, so the sky is not the limit;
  weather and Kp history are (≈ 80–90 years).

## The ritual (release step, not per user)

- `dotnet run --project Dendera.Api -- calibrate` (no network: fixtures only) computes, for every
  school × node, the reference distribution and writes **quantile tables**
  (`rules/<school>.quantiles.json`: 101 quantiles per node), plus `docs/calibration-report.md`
  (per school and layer: how many nodes the school reaches, stability of the quantiles, coverage of
  the cycles).
- Run it whenever a school or an influence changes; in CI on a release branch.
- The tables are versioned next to the rule file and carry a hash of the rule file and of the
  reference set. If a school changes and its table is stale, the API logs it and the catalog marks
  the school "da ricalibrare", using the fallback scale meanwhile. A unit test fails on a stale
  table, so it cannot ship by accident.
- What needs a new ritual: anything that changes how much raw sum a school produces (rules,
  weights, tagWeights, minIntensity, correspondences, natalAmplification, new kinds read) or how an
  influence computes intensity (orbs, Kp mapping, weather thresholds). Texts, descriptions,
  references, tables, icons do not.

## Parameters of the traditions → sources (by hand, never by the ritual)

The table lists, for each parameter, the texts proposed as sources. They are **not verified yet**:
step 1 reads them and fills in the exact values with chapter and page, recorded next to each number
in the rule files. Until then the values are indicative (from memory of the tradition), not
citations.

| Parameter | Today | To verify in the sources |
|---|---|---|
| Orbs of aspects (sky) | per aspect, 2–8°, × 0.5 | Planet-based orbs ("moieties"): Lilly, *Christian Astrology* (1647), I, ch. 19 (Sun ≈ 15–17°, Moon ≈ 12°, Saturn/Jupiter ≈ 9–12°, Mars ≈ 7°, Venus/Mercury ≈ 7°); Al-Bīrūnī, *Book of Instruction* (1029) §§ 497–498 |
| Orbs of transits to the natal chart | same × 0.4 | Practice of transits (1–3°; Moon excluded or only conjunctions): to be documented from a traditional source (e.g. Abū Ma'shar on revolutions, Bonatti) |
| Which aspects | 5 Ptolemaic + 3 minor (kept) | Ptolemy, *Tetrabiblos* I.13 for the five; Kepler for the minor ones |
| Weight of the planets in transits | hand-set (Saturn 1 … Moon 0.4) | Ptolemy III and Lilly: slow planets as "significant" over long periods; to be argued from sources |
| Retrogrades and stations | hand-set intensity curve | Lilly I, ch. 19 ("debilities"), stations as the strongest moments: shape of the curve to be documented |
| TCM organ clock, seasons, excesses | weights 0.25–0.7 | Neijing: relative importance of season vs climate vs hour (Suwen 2, 5, 22) |
| HD weights per body | Sun/Earth 1 | Ra Uru Hu: Sun/Earth ≈ 70% of the "neutrino" weight; the other bodies share the rest |
| Biometeorology thresholds | Kp ≥ 2, pressure Δ12 hPa, etc. | Literature of human biometeorology (e.g. reviews in *Int. J. Biometeorology*): thresholds at which effects are reported (Kp ≥ 5 storms, Δp > 10 hPa / 24 h, temperature swings) |
| Schumann threshold | index ≥ 60 | derived data: keep conservative, document the scale |

With rarity, weights still matter: they decide how contributions *add up* inside a node (what is
rare is a moment when the heavy rules fire together), so they keep their place in the sources.

## Step 1 — research results (2026-11-20, to review)

How to read this: **verified** = value read in a text that quotes the source with its page or
section (for the astrology, Deborah Houlding's articles on skyscript.co.uk, which cite the
editions); not a scan of the original, so the owner may still want to check the page.
**Not found** = no source gives a number; the value stays a convention of the instrument and is
marked as such in the rule file. "Today" is what the code does now.

### A. Orbs of aspects — verified

The tradition gives orbs **per planet**, not per aspect. Two planets are in aspect when their
distance from the exact angle is within the **sum of their moieties** (half orbs).

| Planet | al-Bīrūnī orb | moiety | Lilly, wider list |
|---|---|---|---|
| Sun | 15° | 7½° | 17° |
| Moon | 12° | 6° | 12½° |
| Mercury | 7° | 3½° | 7° |
| Venus | 7° | 3½° | 8° |
| Mars | 8° | 4° | 7½° (text) / 8° (table) |
| Jupiter | 9° | 4½° | 12° |
| Saturn | 9° | 4½° | 10° |
| Uranus, Neptune, Pluto, angles, other points | 5° | 2½° | — |

- al-Bīrūnī, *The Book of Instruction in the Elements of the Art of Astrology* (Ghazna, 1029),
  trans. R. Ramsay Wright (Luzac, 1934), §§ 436–437 (also 446, 490); the list is said to come
  from Porphyry (3rd c.).
- Lilly, *Christian Astrology* (1647), book I, p. 107: gives both lists and says he uses "sometimes
  the one and sometimes the other"; in practice he leans to al-Bīrūnī's.
- Moieties summed: Claude Dariot, *Ad astrorum iudicia facilis introductio* (Latin, 1557; title as cited by Houlding to check), English by
  Fabian Wither (c. 1583). Example: Mercury–Venus 3½ + 3½ = 7°; Sun–Moon 7½ + 6 = 13½°.
- The 5° for outer planets and points is the modern extension recorded by Houlding ("may also be
  applied to angles and all other non-luminous points"); Ptolemy gives 5° for the Ascendant.
- Source of all the above: D. Houlding, "The Classical Origin and Traditional Use of Aspects" and
  "Traditional Orb Tables", skyscript.co.uk.

Today: per aspect (conj./opp. 8°, square/trine 7°, sextile 5°, quincunx 3°, semisquare and
sesquiquadrate 2°) × 0.5 in the sky. **Proposal for step 2**: sky orb = moiety(A) + moiety(B)
(al-Bīrūnī list); minor aspects (no traditional orb) get the same sum × ½, written as convention.
Sky aspects would widen a lot (Sun–Moon 13½° instead of 4°): with rarity this is no longer a
problem of saturation, but it changes which aspects are "on".

### B. Orbs of transits to the natal chart — not found as a number

No text read gives a separate orb for transits. The traditional practice applies the same moieties
(transiting planet's moiety + natal point's moiety). That would give e.g. Saturn on the natal Sun
4½ + 7½ = 12°, which makes slow transits last for a year or more. Options (owner's call):
1. tradition literally: sum of moieties, as for the sky;
2. transiting planet's moiety only (Saturn 4½°, Sun 7½°, outer 2½°), natal point as a point;
3. today's per-aspect × 0.4 (1–3°), written as convention of the instrument.

### C. Which aspects — partly verified

- Five Ptolemaic aspects: Ptolemy, *Tetrabiblos* I.13 ("Of the aspects of the signs", Loeb ed.,
  F. E. Robbins, 1940). Chapter number from the Loeb arrangement, not re-read this time.
- Houlding: most minor aspects "were not recognised in ancient astrology"; only semisextile and
  quincunx (inconjunct) have a classical history.
- Kepler introduced new aspects on harmonic grounds: *De fundamentis astrologiae certioribus*
  (written 1601, printed 1602), thesis on the aspects, and *Harmonices mundi* (1619), book IV.
  Confirmed in secondary sources: quintile, biquintile, sesquiquadrate (135°). **Not found**: the
  semisquare (45°) and the chapter numbers of book IV. Decision taken: minor aspects stay.

### D. Weight of the planets in transits — not found as a number

No text gives numeric weights. What exists: Lilly's scores (*Christian Astrology* p. 115) measure
the *condition* of a planet, not how much it weighs in a transit; the orbs (A) measure its "light",
and they rank the luminaries first, Saturn and Jupiter next, Mars, Venus, Mercury last — the
opposite of today's weights (Saturn 1 … Moon 0.4), which follow the modern idea that slow planets
weigh more because they last. Options:
1. weight = orb / 15 (Sun 1, Moon 0.8, Jupiter and Saturn 0.6, Mars 0.53, Venus and Mercury 0.47,
   outer 0.33): traditional, cited (A);
2. keep today's weights, written as convention of the instrument (with rarity, a long transit is
   already common and scores lower; a fast one is rarer).

### E. Retrogrades and stations — partly verified

- Lilly, *Christian Astrology* p. 115, table of accidental fortitudes and debilities: **direct +4,
  swift in motion +2, slow in motion −2, retrograde −5** (not for Sun and Moon). The table has no
  entry for "stationary".
- So the tradition gives a ratio (retrograde 5, slow 2) but no shape of a curve around stations.
  **Proposal**: retrograde intensity = 5/5 = 1.0 flat; slow motion (|speed| < mean, direct) = 2/5
  = 0.4 as a new signal; station = the existing signal, documented as convention (station = where
  slow motion is extreme). Today: retrograde 0.5 + 0.5 × station factor; station when
  |speed| < 10 % of mean.

### F. Chinese medicine — sources verified as texts, no numbers

- *Huangdi Neijing Suwen* ch. 2 (四氣調神大論, the four seasons), ch. 5 (陰陽應象大論, the six
  qi and the five phases), ch. 22 (藏氣法時論, each organ's course through the seasons, the
  ten-day stems and the hours of the day). Translation: P. U. Unschuld, H. Tessenow,
  *Huang Di Nei Jing Su Wen* (University of California Press, 2011).
- The texts give an **order** (season frames the year, the day frames the hours: ch. 22 says
  an organ's disease improves, worsens and is quiet by season, by day and by hour), no weights.
  Today the order already holds (season 0.6 > climate excess 0.3–0.7 > hour 0.25).
- The 24-hour organ clock as a fixed table (子午流注) is a later development, not the *Neijing*:
  **not found** with a citation in this pass; to cite from the later texts (Jin–Yuan / Ming
  acupuncture manuals) or to say "later tradition".
- **Proposal**: keep the order, weights as convention; write the chapters next to each rule.

### G. Human Design — verified in the school's own sources

- Jovian Archive (Ra Uru Hu's publisher), "Personality Sun: Filter of the Neutrino Ocean": the
  Sun produces about 70 % of the neutrinos crossing the solar system, and "the Sun/Earth is 70 %
  of who you are". Primary teaching: Ra Uru Hu, *Rave I'Ching* (1989) and lectures.
- 70 % to Sun + Earth, 30 % shared by the other 11 activations (Moon, nodes, Mercury … Pluto):
  per body Sun 0.35, Earth 0.35, others 0.027. **Proposal** in relative weights: Sun 1, Earth 1,
  every other body 0.08 (today: nodes 0.6, Moon 0.4, others 0.5). Strong change: transits of
  the Moon and planets through the gates would almost vanish next to the Sun/Earth.

### H. Biometeorology — verified (peer-reviewed literature)

| Signal | Literature | Today |
|---|---|---|
| Kp | NOAA SWPC Space Weather Scales: Kp 5 = G1 storm … Kp 9 = G5. Effects on heart attacks and strokes reported on storm days (scoping review, *Cureus* 2025, 36 studies; *Int. J. Biometeorol.* 58:799, 2014, Moscow hospital admissions — author to check) | intensity (Kp − 2)/6, rule fires at Kp ≥ 2.6 |
| Pressure drop | Kimoto et al., *Intern. Med.* 50, 2011 (PMID 21921370): migraine more frequent when day-to-day pressure is > 5 hPa lower. Okuma et al., *SpringerPlus* 4, 2015, doi 10.1186/s40064-015-1592-4: migraine most frequent 6–10 hPa below 1013 hPa | Δ24h / 12, fires at Δ ≥ 1.8 hPa; level (1015 − p)/25, fires at p < 1010 |
| Temperature swing | Guo et al., *PLoS ONE* 6, 2011 (PMC3032790): drop > 3 °C between neighbouring days (daily mean) raises mortality, cardiovascular +25 % in Los Angeles | Δ24h / 10, fires at 2 °C |
| Light (SAD) | Rosenthal et al., *Arch. Gen. Psychiatry* 41:72–80, 1984 (the syndrome); "Prevalence of seasonal affective disorder at four latitudes", 1990 (PMID 2326393): more winter SAD further north; no threshold in hours of light | (12.5 − h)/4.5 |

**Proposal**: Kp intensity 0 below Kp 4, (Kp − 4)/5 above (G1 at 0.2, G5 at 1); pressure change
fires from 5 hPa, full at 10 hPa; pressure level fires from 1007 hPa (6 below 1013), full at
1003; temperature swing on daily means, fires from 3 °C. Light: no number in the sources,
convention. With per-climate tables (step 3) a summer day in Catania is compared with Catania.

### I. Schumann — document the scale

Derived index from schumannresonancelive.com (CC0), relative 0–100 scale computed from the
Tomsk State University spectrograms; no published threshold. Keep ≥ 60 as a conservative
convention, written in the rule file, until samples are persisted and the ritual can calibrate it.

### Owner's decisions on step 1 (2026-11-20)

1. Sky orbs: al-Bīrūnī moieties summed (A); minor aspects: the same sum × ½ (convention).
2. Transit orbs: B.2, the transiting planet's moiety only; the natal point is a point.
3. Planet weights in transits: D.1, weight = orb / 15.
4. Retrogrades and stations: proposal E (Lilly's 5 and 2).
5. Human Design: 70 / 30 split (G).
6. Biometeorology thresholds (H) accepted; step 3: quantile tables **per climate zone**.

### Step 2 — applied (2026-11-20)

- `Astro/Aspects.cs`: `AspectSpec` has no orb any more; `AspectCatalog.PlanetOrb/Moiety/SkyOrb/TransitOrb`,
  `MinorOrbFactor = 0.5`; `AspectFinder.Find` takes the orb in degrees.
- `SkySignalGenerator`: retrograde intensity 1.0, new kind `slow-motion` (0.4, planets direct below their
  mean speed); station unchanged (convention).
- `melothesia.json` 2.3.0: transit weights = orb / 15; new rule `slow-motion`; citations in comments.
- `human-design.json` 1.2.0: Sun 1, Earth 1, every other body 0.08.
- `biometeorology.json` 1.2.0 + providers: Kp (Kp − 4)/5, rule from Kp 5; Open-Meteo now reads
  `pressure_msl` (sea level, needed for the 1013 hPa reference) instead of surface pressure; pressure level
  (1013 − p)/10 from 1007; change |Δ|/10 from 5 hPa; temperature swing on 24 h means from 3 °C.
- `tcm.json` 2.1.0: Suwen chapters and "later tradition" for the organ clock, in comments.
- `tantric-yoga.json` untouched: its retrograde rule now reads intensity 1 (flat).
- Not done: the parameter table above is not rewritten; the citations live in step 1 and in the files.

### Steps 3–6 — applied (2026-11-21)

- Step 3: `Dendera.Core/Calibration` (`ReferenceSet`, `ClimateZones`, `NodeQuantiles`, `SchoolCalibration`,
  `CalibrationHash`). Climate zones (owner's decision: per zone): Palermo Csa, Milano Cfa, Londra Cfb,
  Varsavia Dfb, Madrid BSk, Il Cairo BWh, Singapore Af; a place maps to the nearest one. Weather schools
  (those requiring `open-meteo`: Biometeorology, TCM) sample sky and weather at the same real moment,
  1991–2020 (WMO normal), so seasons and weather stay coupled. Fixtures: `src/backend/calibration` (~13 MB),
  gitignored and downloaded by `fetch-reference` (owner's decision 2026-11-21); only `manifest.json` with
  their content hashes is committed, and the ritual refuses fixtures that do not match it.
- Step 4: `Dendera.Api/Calibration` (`calibrate`, `fetch-reference`), tables in `rules/*.quantiles.json`,
  `docs/calibration-report.md`. Two runs give byte-identical files. Worst stability between two halves of
  the reference: 3.3 percentile points (Melotesia, feet). `CalibrationTests` fails on a stale table;
  the catalog marks it "da ricalibrare" (`CatalogSchool.Calibration`).
- Step 5: engine scores = rarity (fallback scale without a table); zone detail and Adesso say "rarità
  dell'influsso" and "più sollecitato che nell'N% dei momenti".
- Check on the owner's profile (all five schools): over 16 random instants 2000–2040, on average 52% of
  the touched nodes are under 50% and 3.8 of ~55 are at 90% or more (criterion met). On four instants of
  Oct–Nov 2026 only 27–38% are under 50% and 4–7 nodes are at 90%+: by this reference, that period was
  more solicited than usual for this chart.

### Follow-up (2026-11-21): personal rarity and natal weights

- Found on two profiles born 2001 and 2003: calves always at 90%+. Cause: Uranus, Neptune and the MC in
  Aquarius (generational), so every transit to them lands on Aquarius' zones, and the population reference
  compares the chart with everyone.
- Owner's decision: both fixes. Melotesia 2.4.0 weighs natal targets (Ptolemy's aphetic places, Tetrabiblos
  III.10: Sun, Moon, Ascendant, MC 1; other classical planets 0.6; Uranus, Neptune, Pluto, node 0.3,
  conventions). Sky schools now score against a **personal reference** (`PersonalReference`, 4000 moments
  over 90 years from birth, ~2 s on first use, cached); weather schools keep the climate tables; the ritual's
  global tables are the fallback.
- Check: profile 1 (Melotesia only) calves average 59% over 12 random instants 2000–2040 (≥90% in 3 of 12),
  71–97% in Oct–Nov 2026; profile 2 calves 40–42% in 2019 and 2031, 71–86% in Oct–Nov 2026. With all five
  schools on profile 2, 26–45% of the touched nodes are under 50% in Oct–Nov 2026.

## Steps

1. Research pass: read the sources, fill the table with values and citations; we review.
2. Apply the traditional values to the rule files and the aspect catalogue (orbs per planet need a
   small change in `AspectFinder`: orb = f(planet A, planet B) instead of per aspect).
3. Reference set: synthetic charts generator (seeded), random instants, Kp/weather fixtures
   (decide per climate zone vs per place for Biometeorology).
4. The ritual: `calibrate` command, quantile tables, report, hashes, stale-table test and catalog flag.
5. Engine: score = percentile from the tables (fallback scale when missing); UI wording
   "rarità dell'influsso"; zone detail explains "più sollecitato che nell'N% dei momenti".
6. Docs: CLAUDE.md (meaning of a score, the ritual, when to re-run).

## Done when

- Every value of the traditions table has a citation in this file and next to it in the rule file.
- `calibrate` runs offline and deterministically (same inputs, byte-identical tables) and writes
  `rules/<school>.quantiles.json` for every school plus `docs/calibration-report.md`.
- On the owner's profile with all schools active, the node scores spread out: at any instant
  about half of the touched nodes are under 50%, and 90%+ appears only on a few nodes.
- A stale table (rule file changed, ritual not re-run) fails a unit test and shows "da ricalibrare"
  in the catalog; the fallback scale is used meanwhile.
- The UI says "rarità dell'influsso" and the zone detail explains it in one sentence.
- Tests and builds green; CLAUDE.md and `docs/signals-and-rules.md` describe the new meaning.

## Decisions (2026-11-20)

1. Method: rarity (percentile per school and node) instead of a saturation scale.
2. Reference: synthetic charts only, long random span (1900–2100); no personal charts.
3. Minor aspects (quincunx, semisquare, sesquiquadrate): kept.
4. Calibration is a release ritual, versioned with the rules, not a per-user computation.
