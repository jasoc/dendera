# Plan — calibration of the schools

Status: **approved** (2026-11-20), not started. Method: **rarity** (percentile against a synthetic
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
