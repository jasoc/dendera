# Dendera

Real-time astrological resonance instrument: your natal chart, the current sky, solar and
terrestrial signals, mapped onto the body and tested against your own daily journal.

Named after the Temple of Hathor at Dendera, home of the oldest known circular zodiac.

- **Adesso**: chart wheel (natal and current sky, active transits), body map, active signals.
- **Timeline**: every signal, body zone and journal metric on one time axis.
- **Diario**: 10 seconds a day (energy, mood, sleep, focus, stress, gut, head, body).
- **Corrispondenze**: which signals actually move *you* (permutation tests and FDR control).

## Run

Requirements: .NET 10 SDK, Node 24.

```bash
cd src/backend
dotnet run --project Dendera.Api        # http://localhost:5180
```

```bash
cd src/frontend
npm install
npm start                               # http://localhost:4200
```

Single process: `npm run build` in `src/frontend` writes the app into the API's `wwwroot`,
then `dotnet run --project Dendera.Api` serves everything on :5180.

Tests: `dotnet test` in `src/backend`.

See [CLAUDE.md](CLAUDE.md) for architecture and [docs/](docs/) for the vision and the
signal/rule guide.

## Data sources

- Ephemeris: [Astronomy Engine](https://github.com/cosinekitty/astronomy) (MIT)
- Geomagnetic Kp index: [GFZ Potsdam](https://kp.gfz.de) (CC BY 4.0)
- Weather and geocoding: [Open-Meteo](https://open-meteo.com) (CC BY 4.0)
