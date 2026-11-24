import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, afterNextRender, computed, input, output, signal, viewChild } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FLOW_ORDER, FlowGraph, FlowItem, FlowKind, FlowRef } from '../../core/flow-graph';
import { qualityColor } from '../../core/symbols';

interface Box { item: FlowItem; x: number; y: number; h: number }
interface Band { d: string; from: FlowKind; color: string; width: number; refs: FlowRef[] }

const COLUMN_TITLE: Record<FlowKind, string> = { influence: 'Influenze', signal: 'Segnali', rule: 'Regole', node: 'Conseguenze' };
const KIND_COLOR: Record<FlowKind, string> = {
  influence: 'var(--c-influence)', signal: 'var(--c-signal)', rule: 'var(--c-rule)', node: 'var(--c-node)',
};

/**
 * Cause → effect as a flow: four columns (influences, signals, rules, consequences) joined by bands as wide as the
 * raw amount that passes. Hovering a box lights its paths; clicking it opens it.
 */
@Component({
  selector: 'app-flow-diagram',
  imports: [MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap scroller" #wrap>
      @if (layout(); as l) {
        <svg [attr.viewBox]="'0 -24 ' + l.w + ' ' + (l.h + 24)" [attr.width]="l.w" [attr.height]="l.h + 24" (mouseleave)="hover.set(null)">
          @for (k of order; track k; let i = $index) {
            <text class="head" [attr.x]="headX(i, l)" y="-10" [attr.fill]="kindColor[k]">{{ title[k] }}</text>
          }
          <defs>
            @for (g of gradients; track g.id) {
              <linearGradient [attr.id]="g.id" x1="0" x2="1" y1="0" y2="0">
                <stop offset="0" [attr.stop-color]="g.a" /><stop offset="1" [attr.stop-color]="g.b" />
              </linearGradient>
            }
          </defs>
          @for (b of l.bands; track $index) {
            <path class="band" [attr.d]="b.d" [attr.stroke]="b.color" [attr.stroke-width]="b.width"
                  [class.dim]="hover() && !lit(b.refs)" [class.lit]="hover() && lit(b.refs)" />
          }
          @for (b of l.boxes; track b.item.ref) {
            <g class="box" [class.dim]="hover() && !litRef(b.item.ref)" [class.focus]="b.item.ref === focus()"
               (mouseenter)="hover.set(b.item.ref)" (click)="open.emit(b.item.ref)">
              <rect [attr.x]="b.x" [attr.y]="b.y" [attr.width]="l.bw" [attr.height]="b.h" rx="2"
                    [attr.fill]="fill(b.item)" />
              <text [attr.x]="labelX(b, l)" [attr.y]="b.y + b.h / 2 - 2" [attr.text-anchor]="b.item.kind === 'node' ? 'start' : 'start'"
                    class="t1" [class]="'t1 k-' + b.item.kind">{{ fit(b.item.label, l.chars) }}</text>
              <text [attr.x]="labelX(b, l)" [attr.y]="b.y + b.h / 2 + 11" class="t2">
                @if (b.item.level !== null) { <tspan class="pc">{{ pct(b.item.level) }}</tspan> }
                {{ fit(b.item.sub, l.chars) }}</text>
            </g>
          }
        </svg>
      } @else {
        <p class="fd-empty">Nessuna catena attiva da mostrare.</p>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .wrap { width: 100%; }
    .heads { display: grid; margin: 0 0 6px; }
    .head { font-family: var(--font-title); font-size: 10px; letter-spacing: .16em; text-transform: uppercase; }
    svg { display: block; overflow: visible; }
    .band { fill: none; stroke-opacity: .32; transition: stroke-opacity .2s; }
    .band.dim { stroke-opacity: .06; }
    .band.lit { stroke-opacity: .7; }
    .box { cursor: pointer; transition: opacity .2s; }
    .box.dim { opacity: .3; }
    .box rect { filter: drop-shadow(0 0 4px rgba(0, 0, 0, .5)); }
    .box.focus rect { stroke: var(--text); stroke-width: 1; }
    .t1 { font-family: var(--font-ui); font-size: 12px; fill: var(--text); paint-order: stroke; stroke: #050b14; stroke-width: 3px; }
    .t1.k-influence { font-family: var(--font-title); font-size: 10px; letter-spacing: .1em; fill: var(--c-influence); text-transform: uppercase; }
    .t1.k-signal { fill: color-mix(in srgb, var(--c-signal) 55%, var(--text)); }
    .t1.k-rule { fill: color-mix(in srgb, var(--c-rule) 60%, var(--text)); }
    .t1.k-node { font-weight: 600; fill: var(--text); }
    .t2 { font-family: var(--font-ui); font-size: 10px; fill: var(--muted); paint-order: stroke; stroke: #050b14; stroke-width: 3px; }
    .t2 .pc { font-family: var(--font-mono); fill: var(--text); }
    .scroller { overflow-x: auto; }
    .fd-empty { color: var(--muted); font-size: 12px; }
  `,
})
export class FlowDiagram implements OnDestroy {
  readonly graph = input.required<FlowGraph>();
  readonly focus = input<FlowRef | null>(null);
  readonly open = output<FlowRef>();

  protected readonly order = FLOW_ORDER;
  protected readonly title = COLUMN_TITLE;
  protected readonly kindColor = KIND_COLOR;
  protected readonly hover = signal<FlowRef | null>(null);
  protected readonly width = signal(420);
  protected readonly gradients = [
    { id: 'g-inf-sig', a: '#45d4ff', b: '#ffb347' },
    { id: 'g-sig-rule', a: '#ffb347', b: '#a98bff' },
  ];

  private readonly wrap = viewChild<ElementRef<HTMLDivElement>>('wrap');
  private resize?: ResizeObserver;

  constructor() {
    afterNextRender(() => {
      const el = this.wrap()?.nativeElement;
      if (!el) return;
      // Never narrower than 860 px: below that the four columns crush their labels; the panel scrolls sideways.
      this.resize = new ResizeObserver(() => this.width.set(Math.max(860, el.getBoundingClientRect().width - 8)));
      this.resize.observe(el);
    });
  }

  ngOnDestroy() {
    this.resize?.disconnect();
  }

  /** Paths through the hovered box: every ref on them lights up. */
  private readonly litSet = computed(() => {
    const h = this.hover();
    if (!h) return new Set<FlowRef>();
    const kind = h.split(':')[0] as FlowKind;
    const set = new Set<FlowRef>();
    for (const p of this.graph().paths)
      if (p[kind] === h) for (const k of FLOW_ORDER) set.add(p[k]);
    return set;
  });
  protected litRef = (r: FlowRef) => this.litSet().has(r);
  protected lit = (refs: FlowRef[]) => refs.every((r) => this.litSet().has(r));

  protected readonly layout = computed(() => {
    const g = this.graph();
    if (!g.paths.length) return null;
    const w = this.width();
    const bw = 8;
    // The last column (the body) has its labels on the right, inside the drawing: leave room for them.
    const nodeLabel = 150;
    const colX = FLOW_ORDER.map((_, i) => Math.round((i * (w - bw - nodeLabel)) / 3));
    const lw = (w - bw - nodeLabel) / 3 - 18;
    const chars = Math.max(12, Math.floor(lw / 6.4));

    // Columns, ordered to limit crossings: nodes by value, then each column by the barycentre of what it feeds.
    const byKind = (k: FlowKind) => [...g.items.values()].filter((i) => i.kind === k);
    const pos = new Map<FlowRef, number>();
    const nodes = byKind('node').sort((a, b) => (b.level ?? 0) - (a.level ?? 0) || b.value - a.value);
    nodes.forEach((n, i) => pos.set(n.ref, i));
    const order = (k: FlowKind, next: FlowKind) => {
      const list = byKind(k);
      const bary = new Map<FlowRef, number>();
      for (const it of list) {
        let sum = 0, weight = 0;
        for (const p of g.paths) if (p[k] === it.ref) { sum += (pos.get(p[next]) ?? 0) * p.amount; weight += p.amount; }
        bary.set(it.ref, weight ? sum / weight : 0);
      }
      list.sort((a, b) => bary.get(a.ref)! - bary.get(b.ref)!);
      list.forEach((it, i) => pos.set(it.ref, i));
      return list;
    };
    const rules = order('rule', 'node');
    const signals = order('signal', 'rule');
    const influences = order('influence', 'signal');
    const columns: FlowItem[][] = [influences, signals, rules, nodes];

    const gap = 12;
    const minH = 22;
    const maxCount = Math.max(...columns.map((c) => c.length));
    const h = Math.max(220, maxCount * (minH + gap) + 120);
    const totals = columns.map((c) => c.reduce((s, it) => s + it.value, 0));
    const scale = Math.min(...columns.map((c, i) => (h - gap * (c.length - 1) - c.length * 2) / Math.max(totals[i], 1e-9)));

    const boxes = new Map<FlowRef, Box>();
    columns.forEach((col, ci) => {
      const heights = col.map((it) => Math.max(minH, it.value * scale));
      const used = heights.reduce((s, x) => s + x, 0) + gap * (col.length - 1);
      let y = (h - used) / 2;
      col.forEach((it, i) => {
        boxes.set(it.ref, { item: it, x: colX[ci], y, h: heights[i] });
        y += heights[i] + gap;
      });
    });

    // Bands per adjacent pair, aggregated, stacked inside each box in the order of the other end.
    const bands: Band[] = [];
    for (let ci = 0; ci < 3; ci++) {
      const a = FLOW_ORDER[ci], b = FLOW_ORDER[ci + 1];
      const agg = new Map<string, { from: FlowRef; to: FlowRef; v: number; q: string }>();
      for (const p of g.paths) {
        const k = `${p[a]}>${p[b]}`;
        const e = agg.get(k) ?? { from: p[a], to: p[b], v: 0, q: p.quality };
        e.v += p.amount;
        agg.set(k, e);
      }
      const edges = [...agg.values()].sort((x, y) => boxes.get(x.to)!.y - boxes.get(y.to)!.y);
      const outOff = new Map<FlowRef, number>();
      const inOff = new Map<FlowRef, number>();
      const ratio = (r: FlowRef) => boxes.get(r)!.h / Math.max(g.items.get(r)!.value * scale, 1e-9);
      for (const e of [...edges].sort((x, y) => boxes.get(x.from)!.y - boxes.get(y.from)!.y || boxes.get(x.to)!.y - boxes.get(y.to)!.y)) {
        const s = boxes.get(e.from)!, t = boxes.get(e.to)!;
        const thickness = Math.max(1, e.v * scale);
        const y0 = s.y + (outOff.get(e.from) ?? 0) + (thickness * ratio(e.from)) / 2;
        const y1 = t.y + (inOff.get(e.to) ?? 0) + (thickness * ratio(e.to)) / 2;
        outOff.set(e.from, (outOff.get(e.from) ?? 0) + thickness * ratio(e.from));
        inOff.set(e.to, (inOff.get(e.to) ?? 0) + thickness * ratio(e.to));
        const x0 = s.x + bw, x1 = t.x, mx = (x0 + x1) / 2;
        bands.push({
          d: `M${x0},${y0} C${mx},${y0} ${mx},${y1} ${x1},${y1}`,
          from: a,
          color: ci === 0 ? 'url(#g-inf-sig)' : ci === 1 ? 'url(#g-sig-rule)' : qualityColor(e.q),
          width: thickness,
          refs: [e.from, e.to],
        });
      }
    }
    return { w, h, bw, lw, chars, boxes: [...boxes.values()], bands };
  });

  protected labelX(b: Box, l: { bw: number }) {
    return b.x + l.bw + 6;
  }

  /** Cuts a label to what fits in its column. */
  protected fit(text: string, chars: number) {
    return text.length <= chars ? text : text.slice(0, chars - 1) + '…';
  }

  protected headX(i: number, l: { boxes: Box[] }) {
    const kind = FLOW_ORDER[i];
    return l.boxes.find((b) => b.item.kind === kind)?.x ?? 0;
  }

  protected fill(it: FlowItem) {
    return it.kind === 'node' ? qualityColor(it.quality) : KIND_COLOR[it.kind];
  }

  protected pct = (v: number) => `${Math.round(v * 100)}%`;
}
