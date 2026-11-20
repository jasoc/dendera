import { Component, computed, input } from '@angular/core';
import { NatalChart, PlacedPoint, Signal } from '../core/models';
import { BODY_GLYPH, SIGNS, SIGN_ELEMENT_COLOR, SIGN_GLYPH, SIGN_NAME, qualityColor } from '../core/symbols';

interface Glyph {
  id: string;
  lon: number;
  shown: number; // display longitude after collision spreading
  glyph: string;
  title: string;
  retro: boolean;
}

const R = { outer: 250, signIn: 214, transit: 193, mid: 172, natal: 150, houseNum: 118, aspect: 104 };

/**
 * The sky: zodiac band (outer), current planets (transits), natal planets (inner), houses,
 * and chords for the active transit-to-natal aspects. Ascendant on the left, as in a classical chart.
 */
@Component({
  selector: 'app-sky-wheel',
  template: `
    <svg viewBox="-260 -260 520 520" class="wheel" role="img" aria-label="Carta del cielo">
      <circle [attr.r]="R.outer" class="ring-bg" />
      @for (s of signs(); track s.id) {
        <path [attr.d]="s.path" [attr.fill]="s.color" class="sign-band"><title>{{ s.name }}</title></path>
        <text [attr.x]="s.gx" [attr.y]="s.gy" class="sign-glyph">{{ s.glyph }}</text>
      }
      <circle [attr.r]="R.signIn" class="ring" />
      <circle [attr.r]="R.mid" class="ring faint" />
      <circle [attr.r]="R.aspect" class="ring" />

      @for (c of cusps(); track $index) {
        <line [attr.x1]="c.x1" [attr.y1]="c.y1" [attr.x2]="c.x2" [attr.y2]="c.y2"
              [class.axis]="c.axis" class="cusp" />
        <text [attr.x]="c.nx" [attr.y]="c.ny" class="house-num">{{ $index + 1 }}</text>
      }

      @for (a of aspectLines(); track a.key) {
        <line [attr.x1]="a.x1" [attr.y1]="a.y1" [attr.x2]="a.x2" [attr.y2]="a.y2"
              [attr.stroke]="a.color" [attr.stroke-width]="a.width" [attr.stroke-opacity]="a.opacity"
              class="aspect"><title>{{ a.title }}</title></line>
      }

      @for (g of natalGlyphs(); track g.id) {
        <line [attr.x1]="pt(g.lon, R.mid).x" [attr.y1]="pt(g.lon, R.mid).y"
              [attr.x2]="pt(g.lon, R.mid - 7).x" [attr.y2]="pt(g.lon, R.mid - 7).y" class="tick natal" />
        <text [attr.x]="pt(g.shown, R.natal).x" [attr.y]="pt(g.shown, R.natal).y" class="planet natal">
          {{ g.glyph }}<title>{{ g.title }}</title>
        </text>
      }

      @for (g of skyGlyphs(); track g.id) {
        <line [attr.x1]="pt(g.lon, R.signIn).x" [attr.y1]="pt(g.lon, R.signIn).y"
              [attr.x2]="pt(g.lon, R.signIn - 8).x" [attr.y2]="pt(g.lon, R.signIn - 8).y" class="tick sky" />
        <text [attr.x]="pt(g.shown, R.transit).x" [attr.y]="pt(g.shown, R.transit).y" class="planet sky"
              [class.retro]="g.retro">
          {{ g.glyph }}<title>{{ g.title }}</title>
        </text>
        @if (g.retro) {
          <text [attr.x]="pt(g.shown, R.transit).x + 9" [attr.y]="pt(g.shown, R.transit).y + 8" class="rx">℞</text>
        }
      }
    </svg>
  `,
  styles: `
    :host { display: block; }
    .wheel { width: 100%; height: auto; font-family: var(--font-symbols); }
    .ring-bg { fill: var(--panel-2); }
    .ring { fill: none; stroke: var(--line-strong); stroke-width: 1; }
    .ring.faint { stroke: var(--line); stroke-dasharray: 2 4; }
    .sign-band { fill-opacity: 0.16; stroke: var(--line); stroke-width: 0.6; }
    .sign-glyph { font-size: 17px; fill: var(--text); text-anchor: middle; dominant-baseline: central; }
    .cusp { stroke: var(--line); stroke-width: 0.8; }
    .cusp.axis { stroke: var(--gold); stroke-width: 1.6; }
    .house-num { font-size: 10px; fill: var(--muted); text-anchor: middle; dominant-baseline: central; font-family: var(--font-ui); }
    .tick { stroke-width: 1.2; }
    .tick.sky { stroke: var(--text); }
    .tick.natal { stroke: var(--gold); }
    .planet { font-size: 17px; text-anchor: middle; dominant-baseline: central; cursor: default; }
    .planet.sky { fill: var(--text); }
    .planet.natal { fill: var(--gold); font-size: 15px; }
    .planet.retro { fill: var(--q-inward); }
    .rx { font-size: 9px; fill: var(--q-inward); text-anchor: middle; dominant-baseline: central; }
    .aspect { stroke-linecap: round; }
  `,
})
export class SkyWheel {
  readonly sky = input.required<PlacedPoint[]>();
  readonly natal = input<NatalChart | null>(null);
  readonly signals = input<Signal[]>([]);

  protected readonly R = R;
  private readonly asc = computed(() => this.natal()?.ascendant ?? 0);

  pt(lon: number, r: number) {
    const theta = ((180 + lon - this.asc()) * Math.PI) / 180;
    return { x: r * Math.cos(theta), y: -r * Math.sin(theta) };
  }

  protected readonly signs = computed(() =>
    SIGNS.map((id, i) => {
      const a = this.pt(i * 30, R.outer), b = this.pt(i * 30 + 30, R.outer);
      const c = this.pt(i * 30 + 30, R.signIn), d = this.pt(i * 30, R.signIn);
      const g = this.pt(i * 30 + 15, (R.outer + R.signIn) / 2);
      // Increasing longitude is counter-clockwise on screen: sweep-flag 0 on the outer arc.
      const path = `M${a.x},${a.y} A${R.outer},${R.outer} 0 0 0 ${b.x},${b.y} L${c.x},${c.y} A${R.signIn},${R.signIn} 0 0 1 ${d.x},${d.y} Z`;
      return { id, path, color: SIGN_ELEMENT_COLOR[i % 4], glyph: SIGN_GLYPH[id], name: SIGN_NAME[id], gx: g.x, gy: g.y };
    }),
  );

  protected readonly cusps = computed(() => {
    const n = this.natal();
    if (!n) return [];
    return n.cusps.map((lon, i) => {
      const p1 = this.pt(lon, R.aspect), p2 = this.pt(lon, R.signIn);
      const next = n.cusps[(i + 1) % 12];
      const mid = lon + (((next - lon + 360) % 360) / 2);
      const num = this.pt(mid, R.houseNum);
      return { x1: p1.x, y1: p1.y, x2: p2.x, y2: p2.y, nx: num.x, ny: num.y, axis: i === 0 || i === 9 };
    });
  });

  protected readonly skyGlyphs = computed(() => spread(this.sky().map(toGlyph), 7.5));
  protected readonly natalGlyphs = computed(() => spread((this.natal()?.points ?? []).map(toGlyph), 7));

  protected readonly aspectLines = computed(() => {
    const natal = this.natal();
    const sky = new Map(this.sky().map((p) => [p.id, p]));
    if (natal) {
      const points = new Map(natal.points.map((p) => [p.id, p]));
      return this.signals()
        .filter((s) => s.kind === 'transit-aspect')
        .map((s) => line(this, s, sky.get(s.tags['transitBody'])?.longitude, points.get(s.tags['natalPoint'])?.longitude))
        .filter((l) => l !== null);
    }
    return this.signals()
      .filter((s) => s.kind === 'sky-aspect')
      .map((s) => line(this, s, sky.get(s.tags['bodyA'])?.longitude, sky.get(s.tags['bodyB'])?.longitude))
      .filter((l) => l !== null);
  });
}

function line(w: SkyWheel, s: Signal, a: number | undefined, b: number | undefined) {
  if (a === undefined || b === undefined) return null;
  const p = w.pt(a, R.aspect), q = w.pt(b, R.aspect);
  return {
    key: s.key,
    x1: p.x, y1: p.y, x2: q.x, y2: q.y,
    color: qualityColor(s.tags['quality']),
    width: 0.8 + 2.2 * s.intensity,
    opacity: 0.25 + 0.7 * s.intensity,
    title: `${s.label} · ${s.detail}`,
  };
}

function toGlyph(p: PlacedPoint): Glyph {
  return {
    id: p.id,
    lon: p.longitude,
    shown: p.longitude,
    glyph: BODY_GLYPH[p.id] ?? p.id,
    title: `${p.label} ${p.position}${p.house ? ` · casa ${p.house}` : ''}${p.retrograde ? ' · retrogrado' : ''}`,
    retro: p.retrograde,
  };
}

/** Pushes glyphs apart so they don't overlap (keeps true longitude for the tick). */
function spread(glyphs: Glyph[], minSep: number): Glyph[] {
  const g = [...glyphs].sort((a, b) => a.lon - b.lon);
  for (let pass = 0; pass < 4; pass++) {
    for (let i = 0; i < g.length; i++) {
      const next = g[(i + 1) % g.length];
      if (next === g[i]) continue;
      let gap = next.shown - g[i].shown;
      if (i === g.length - 1) gap += 360;
      if (gap < minSep) {
        const push = (minSep - gap) / 2;
        g[i].shown -= push;
        next.shown += push;
      }
    }
  }
  return g;
}
