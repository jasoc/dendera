# Signals and rules — how to extend Dendera

## Signal kinds produced today

| Kind | Key(s) | Source | Intensity 0..1 | Main tags |
|---|---|---|---|---|
| `placement` | `placement:{body}` | ephemeris | always 1 | body, sign, house |
| `retrograde` | `retrograde:{body}` | ephemeris | 0.5, rising to 1 near the station | body, sign |
| `station` | `station:{body}` | ephemeris | 1 − \|speed\| / (10% of mean speed) | body, sign, motion |
| `retrograde-count` | `retrograde-count` | ephemeris | retrograde planets / 8 | count |
| `sky-aspect` | `sky:{a}:{aspect}:{b}` | ephemeris | cosine of orb fraction | bodyA, bodyB, aspect, quality, applying |
| `transit-aspect` | `transit:{body}:{aspect}:{natalPoint}` | ephemeris | cosine of orb fraction | transitBody, natalPoint, natalSign, natalHouse, aspect, quality, applying |
| `lunar-phase` | `lunar-phase` | ephemeris | \|cos(phase)\| (peaks at new and full) | phase |
| `lunar-distance` | `lunar-distance` | ephemeris | 1 at perigee, 0 at apogee | zone |
| `photoperiod` | `photoperiod` | ephemeris | (12.5 h − daylight) / 4.5 h | — |
| `photoperiod-change` | `photoperiod-change` | ephemeris | \|Δ minutes/day\| / 4 | direction |
| `eclipse` | `eclipse:solar`, `eclipse:lunar` | ephemeris | 1 at the peak, 0 at ±15 days | type, kind |
| `geomagnetic` | `kp` | GFZ | (Kp − 2) / 6 | level, status |
| `pressure` | `pressure` | Open-Meteo | (1015 − hPa) / 25 | — |
| `pressure-change` | `pressure-change` | Open-Meteo | \|Δ24h\| / 12 hPa | direction (drop/rise) |
| `temperature-swing` | `temperature-swing` | Open-Meteo | \|Δ24h\| / 10 °C | direction |
| `humidity` | `humidity` | Open-Meteo | (RH − 60) / 40 | — |

Orbs come from the rule set: transit orb = aspect orb × `orbs.transit` (0.4), sky orb =
aspect orb × `orbs.sky` (0.5, major aspects only).

## Adding a new signal source

1. In `Dendera.Sources`, implement `ISignalProvider`:
   - `Source`: short id (shown in the UI chips).
   - `SampleValidity`: how long a sample stays "active" when asking for an instant.
   - `GetAsync(SignalQuery)`: return samples between `From − SampleValidity` and `To`
     at the query location. Cache HTTP calls (`IMemoryCache`).
2. Add the kind constant to `SignalKinds` (Core) and, if useful, a group/colour in
   `src/frontend/src/app/core/symbols.ts` (`KIND_GROUP`) and `pages/timeline.ts` (`KIND_COLOR`).
3. Register it in `Dendera.Api/Program.cs` (`AddHttpClient` + `AddTransient<ISignalProvider, …>`).
4. Write rules for the new kind in `src/backend/rules/*.json`.

A signal does nothing on the body until a rule matches it, but it always appears on the
timeline and in the correlation engine.

## Writing a rule set

`src/backend/rules/melothesia-classic.json` is the reference. Shape:

```jsonc
{
  "id": "my-school", "version": "1.0.0", "name": "…", "description": "…",
  "correspondences": { "virgo": ["gut", "solar-plexus"], "mercury": ["nervous-system"] },
  "aspects": [ { "id": "square", "angle": 90, "orb": 7, "quality": "tension", "major": true } ],
  "orbs": { "transit": 0.4, "sky": 0.5 },
  "natalAmplification": 0.5,
  "saturation": 2.5,
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

Valid zones (`BodyZones.cs`): local — head, throat, shoulders, lungs, heart, stomach,
solar-plexus, liver, gut, kidneys, pelvis, thighs, knees, calves, feet; systemic —
nervous-system, circulation, skeleton, lymph, sleep, mood, vitality.

Known qualities (colours in the UI): tension, flow, fusion, inward, agitation, depletion,
pressure, sensitivity, neutral. Any other string works but is drawn grey.

Every `*.json` in `rules/` is loaded at startup; the profile chooses which one to use, and
`?ruleset=<id>` on `/reading` and `/correlations` lets you compare schools.
