# Vision

## Why Dendera exists

In October–November 2026 the owner and his circle of friends lived weeks of stress,
fatigue and a "block in the head" that nobody could explain: life was giving them
everything they needed, yet they could not enjoy it. Afterwards social media pointed out
that many planets had been retrograde at the same time. The feeling was strong and shared,
and nobody had a tool to look at it properly.

Dendera is that tool: an instrument that shows what the sky (and the Earth's own
environment) is doing **right now**, how it lands on a specific person's chart and body,
and — crucially — whether it actually matches how that person feels, day after day.

## What it is not

- Not a horoscope. No advice of the kind "avoid X today".
- Not a static personality chart (that is what most natal-chart sites do). Dendera is
  about *the moment* and *the trend*: even the Human Design school reads the current
  transits through the gates and centers, not only the birth design.
- Not a black box. Every activation on the body map can be traced back to the signals and
  the rules that produced it.

## The method: Babylonian diaries

For roughly six centuries the astronomers of Babylon kept *Astronomical Diaries*: night
after night they recorded planetary positions next to the weather, the level of the
Euphrates, the price of barley, epidemics and the mood of the city. They did not write
horoscopes; they collected data to find correspondences.

Dendera follows the same method:

1. **Signals** — every influence becomes a uniform signal (sky, Moon, light, geomagnetism,
   weather).
2. **Schools** — each school (medical astrology, biometeorology, Chinese medicine, Human
   Design…) maps signals to body zones and qualities with its own correspondences. Schools
   are explicit, versioned JSON files and can be switched on and off at any time.
3. **Journal** — the person records energy, mood, sleep, focus, stress, gut, head and body
   on a 1–5 scale, in ten seconds a day.
4. **Correspondences** — the correlation engine tests every signal and every body zone
   against the journal, with permutation tests and false-discovery control, and reports
   which ones actually move *this* person.

Rules that never find a match in the journal can be down-weighted or dropped; new schools
can be written and compared. The traditional knowledge is the starting hypothesis, the
journal is the judge.

## Two layers of "sky"

- **Celestial**: planetary positions, aspects among planets, transits to the natal chart,
  retrogrades and stations, lunar phase and distance, eclipses.
- **Terrestrial resonances of the sky**: geomagnetic activity (the Sun's wind hitting the
  magnetosphere), photoperiod (how much light the Sun gives at your latitude), barometric
  pressure and temperature swings. Ancient priests read the sky as one system with the
  Earth; Dendera does too.

## Where it goes next

Self-hosted first, for one person and their friends. Later a public instance where anyone
can enter their birth data, keep a private journal, and — if they want — contribute
anonymised data to a shared diary: a modern, collective version of the Babylonian archive.
