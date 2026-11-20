# Plan — the body as a living model: psyche layer, 3D body, zoom out to the sky

Status: **in progress** (2026-11-20). Extended the same day: full TCM vocabulary (zang-fu with
meridians, seven emotions, five Shen, substances, senses, tissues, Sheng/Ke), chakras and nadis with
the Yoga tantrico school, consensus mean between schools. Calibration: `calibration.md`. All four decisions taken. Done: steps 1–6 (body model, second
pass, schools, API, zone detail, 3D body of light in Adesso with zoom-out sky). Not done: step 7
(time playback in 3D), README screenshots, and checking the 3D in a real browser (shaders compile
only at runtime).
Execute top to bottom once agreed; each step ends green (`dotnet test` in `src/backend`,
`npm run build` in `src/frontend`). Never `git commit`. Read `CLAUDE.md` first.

## Goal

Today the body is a flat list of 22 zones (15 places + 7 "systemic" ones such as mood, sleep,
vitality) and every consequence is a zone score with a quality colour. Paride wants the body to
be a much more **fundamental heuristic**: organs, the brain, and *inside* them emotions,
feelings, mood and temperament, all linked, so that influences and signals weigh on a whole
person. Then he wants to **see** it: a large, beautiful, artistic 3D body with its emotions
inside, and by zooming out, the sky around it with signals, influences, nuances and energy waves.

Two things change:

1. **The model** (backend, every school): from flat zones to a layered *body model* with links.
2. **The view** (frontend): a 3D scene, body inside, sky outside, on top of the existing 2D
   panels, which stay as the readable, accessible "instrument" view.

## Where we are (2026-11-20)

- Zone score per school = `1 − e^(−Σ contributions / saturation)`; schools combine with
  `1 − Π(1 − score)`. Since today each **quality** (tension, flow, fusion, agitation, …) also
  has its own saturated score per zone (`ZoneActivation.QualityScores`), shown as shaded bars
  in the zone detail.
- Zones are *where*, qualities are *how*. "Mood" is a zone, not a quality: this is the confusion
  the psyche layer resolves.

## The body model (proposal)

Four layers, one graph. Nodes have ids like today's zones; edges say "this organ houses / feeds
this feeling". Every school can write into any node; the 3D scene draws nodes inside the body.

| Layer | Nodes (proposal) | Tradition it comes from |
|---|---|---|
| **Soma** (organs, places) | today's 15 local zones + **brain** (split from head) | Melothesia, anatomy |
| **Systems** | nervous system, circulation, skeleton & skin, lymph, sleep, vitality | Galen, modern physiology |
| **Humors / temperament** | sanguine, choleric, melancholic, phlegmatic | Hippocrates, Galen, medieval medicine: planets ↔ humors (Saturn melancholic, Mars choleric, Jupiter sanguine, Moon/Venus phlegmatic) |
| **Emotions** | anger, joy, worry (pensiveness), grief, fear (+ fright) | *Huangdi Neijing*: the five emotions each housed in an organ (liver, heart, spleen, lungs, kidneys) |
| **Mind** | imagination, reason, memory (the three cells of the brain) + mood, focus | Medieval ventricle doctrine (Avicenna, Albertus Magnus): imagination front, reason middle, memory back |

Edges (shared by all schools, `rules/body-model.json`): liver→anger, heart→joy,
spleen→worry, lungs→grief, kidneys→fear; brain→imagination/reason/memory; humors ↔ organs
(blood/heart-liver, yellow bile/gallbladder→liver zone, black bile/spleen→stomach-gut, phlegm/brain-lungs).

**Organ → feeling links are rules of a school** (decision 2), not automatic shared edges. They
belong to the school whose tradition defines them (e.g. the five emotions housed in the organs are
Medicina cinese; the humors are Galenic medicine) and will be few. Mechanism: a **second pass**.
After the schools have produced zone/node scores from the signals, each score becomes an internal
signal of kind `node` (tags `node`, `quality`), and rules can match it:
`{ "when": { "kind": "node", "tags": { "node": ["liver"] }, "minIntensity": 0.3 }, "effect": { "zones": ["anger"] } }`.
One pass only (no cycles: rules on `node` signals cannot feed other `node` rules). A school
switched off takes its links with it. Schools can still target feelings directly from signals
(e.g. TCM's "excess of Heat agitates the Heart and the Shen").

**Journal** (not done yet, follow-up): the 8 metrics map onto nodes (mood→mood, stress→fear+worry, energy→vitality,
sleep→sleep, focus→reason, gut→gut, head→head+brain, body→skeleton). The correlation engine then
tests feelings, not only zones.

**Qualities stay** as the *tone* (colour) of every consequence; each node gets per-quality scores
like zones do today.

## Steps

1. **Body model in the Core**: `BodyModel` (layers, nodes, edges, Italian labels) loaded from
   `rules/body-model.json`, validated; `BodyZones` becomes the node list. `RuleEngine` unchanged
   per node; new second pass after `SchoolEngine`: node scores → internal `node` signals → rules of
   kind `node` (pure, tested: a liver activation yields anger only when the school owning that rule is
   active; one pass, no cycles).
2. **Schools write the new layers**: Melotesia (planets → humors, Moon → mood), Biometeorologia
   (light deficit → melancholy, Kp → fright/agitation), Medicina cinese (five emotions explicitly),
   Human Design (Ajna → reason, Solar plexus → emotional wave, Spleen → fear). Lexicon names.
3. **API**: `/api/catalog` returns the model (layers, nodes, edges); reading zones gain `layer`;
   timeline tracks per node; journal metric ↔ node map.
4. **Zone detail**: shows "why" per node: contributions straight from signals vs contributions
   from organs (second pass), grouped by quality as today.
5. **3D body in Adesso** (decision 4: it **replaces the 2D body panel** of Adesso; `three.js`, one
   standalone Angular component, lazy-loaded with the page). The sky wheel stays as its own widget,
   titled **"Cielo"**. The zone detail panel stays beside the 3D view:
   - an ethereal translucent figure (glass / light shader, fresnel rim in gold), organs as soft
     emissive volumes at the anchors already used by the 2D map, intensity = score, colour =
     dominant quality, slow pulse;
   - feelings as nebulae / particle auras inside and around their organ, humors as four coloured
     currents through the body, the mind as three lights in the head;
   - natal sensitivity as golden rings; click/tap a node → the same zone-detail panel.
6. **Zoom out to the sky**: one continuous camera dolly. The body shrinks to the centre of a
   celestial sphere: ecliptic ring with the 12 signs (Dendera-zodiac style), planets at their real
   longitudes, retrogrades drawn as loops, aspect lines between planets, transits as beams from a
   planet to the organ it touches; geomagnetic activity as an aurora shell, Schumann as a standing
   wave between ground and ionosphere, weather as atmosphere particles, the Moon's phase lighting
   the scene. Every visual element = a signal; hover shows its influence and the schools reading it.
7. **Time**: scrub (same controls as Adesso) and play a day / week as an animation.
8. **Docs**: CLAUDE.md (body model, propagation, 3D), signals-and-rules.md (writing node rules),
   README screenshots.

Performance: three.js (≈ 150 kB gz) is lazy-loaded with Adesso; with "reduced motion" or no WebGL
the old 2D body map is the fallback.

## Open decisions (for Paride)

1. ~~Feelings vocabulary~~ **decided**: traditional structure (Neijing, Galen, the cells of the
   brain) with modern Italian words and one-line descriptions.
2. ~~Propagation organ → feeling~~ **decided**: explicit rules of the owning school (second pass).
3. ~~3D figure~~ **decided**: an ethereal **body of light**, procedural, no anatomy file. Paride's
   requirements: beautiful at first glance and **unmistakably a human body** (clear human
   silhouette, proportions, posture); **no "skeleton with plastic organs" look**. So: a smooth,
   luminous, translucent figure (soft volume, rim light, inner glow and drifting particles), organs
   and feelings as diffuse light *inside* it, not as solid objects; nothing anatomical or medical.
4. ~~Where the 3D goes~~ **decided**: it replaces the 2D body in Adesso; the sky wheel becomes its own "Cielo" widget.
