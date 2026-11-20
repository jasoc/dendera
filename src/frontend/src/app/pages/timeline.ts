import { Component, computed, effect, inject, signal } from '@angular/core';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { JournalDay, METRICS, SignalTrack, TimelineResponse } from '../core/models';
import { KIND_GROUP, METRIC_NAME, qualityColor } from '../core/symbols';

interface Bar { x: number; w: number; h: number; color: string; opacity: number; title: string }
interface Row { key: string; label: string; bars: Bar[]; peak: number }
interface Group { name: string; rows: Row[] }

const W = 1000;
const KIND_COLOR: Record<string, string> = {
  'retrograde-count': 'var(--q-inward)', retrograde: 'var(--q-inward)', station: 'var(--q-pressure)',
  eclipse: 'var(--q-tension)', 'lunar-phase': 'var(--q-sensitivity)', 'lunar-distance': 'var(--q-sensitivity)',
  photoperiod: 'var(--q-depletion)', 'photoperiod-change': 'var(--q-depletion)', geomagnetic: 'var(--q-agitation)',
  pressure: 'var(--q-pressure)', 'pressure-change': 'var(--q-pressure)', 'temperature-swing': 'var(--q-tension)',
  humidity: 'var(--q-pressure)',
};
const RANGES = [
  { label: '2 settimane', back: 10, fwd: 4 },
  { label: '5 settimane', back: 30, fwd: 7 },
  { label: '3 mesi', back: 80, fwd: 10 },
  { label: '6 mesi', back: 170, fwd: 10 },
];

/** Oscilloscope view: every signal, every body zone and the journal on one time axis. */
@Component({
  selector: 'app-timeline',
  template: `
    <header class="toolbar">
      <div class="when">
        @for (r of ranges; track r.label; let i = $index) {
          <button class="btn" [class.primary]="range() === i" (click)="range.set(i)">{{ r.label }}</button>
        }
        <label class="check"><input type="checkbox" [checked]="showAll()" (change)="showAll.set(!showAll())" /> anche segnali deboli</label>
      </div>
      <div class="muted">{{ fromLabel() }} → {{ toLabel() }}</div>
    </header>

    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (loading()) { <p class="muted">Calcolo della timeline…</p> }

    @if (data()) {
      <section class="panel tl" [class.loading]="loading()">
        <div class="tl-row axis">
          <div class="tl-label"></div>
          <svg [attr.viewBox]="'0 0 ' + W + ' 18'" preserveAspectRatio="none" class="tl-svg">
            @for (t of ticks(); track t.x) {
              <line [attr.x1]="t.x" [attr.x2]="t.x" y1="12" y2="18" class="tick" />
            }
          </svg>
          <div class="tick-labels">
            @for (t of ticks(); track t.x) { <span [style.left.%]="t.x / 10">{{ t.label }}</span> }
          </div>
        </div>

        @for (g of groups(); track g.name) {
          <h3 class="tl-group">{{ g.name }}</h3>
          @for (row of g.rows; track row.key) {
            <div class="tl-row">
              <div class="tl-label" [title]="row.key">{{ row.label }}</div>
              <svg [attr.viewBox]="'0 0 ' + W + ' 20'" preserveAspectRatio="none" class="tl-svg">
                @for (b of row.bars; track $index) {
                  <rect [attr.x]="b.x" [attr.y]="20 - b.h" [attr.width]="b.w" [attr.height]="b.h"
                        [attr.fill]="b.color" [attr.fill-opacity]="b.opacity"><title>{{ b.title }}</title></rect>
                }
                <line [attr.x1]="nowX()" [attr.x2]="nowX()" y1="0" y2="20" class="now" />
              </svg>
            </div>
          }
        }
      </section>
    }
  `,
  styles: `
    .tl { overflow-x: auto; }
    .tl-group { margin: 18px 0 6px; font-size: 12px; text-transform: uppercase; letter-spacing: .12em; color: var(--gold); }
    .tl-row { display: grid; grid-template-columns: 220px 1fr; align-items: center; gap: 10px; min-width: 640px; position: relative; }
    .tl-row + .tl-row { border-top: 1px solid var(--line); }
    .tl-label { font-size: 12.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; color: var(--text); }
    .tl-svg { width: 100%; height: 20px; display: block; }
    .axis .tl-svg { height: 18px; }
    .tick { stroke: var(--muted); stroke-width: 1; vector-effect: non-scaling-stroke; }
    .now { stroke: var(--gold); stroke-width: 1.5; vector-effect: non-scaling-stroke; }
    .tick-labels { position: absolute; left: 230px; right: 0; top: -2px; height: 14px; pointer-events: none; }
    .tick-labels span { position: absolute; transform: translateX(-50%); font-size: 10.5px; color: var(--muted); white-space: nowrap; }
    @media (max-width: 700px) { .tl-row { grid-template-columns: 130px 1fr; } .tick-labels { left: 140px; } }
  `,
})
export class Timeline {
  private readonly api = inject(Api);
  private readonly store = inject(ProfileStore);

  protected readonly W = W;
  protected readonly ranges = RANGES;
  protected readonly range = signal(1);
  protected readonly showAll = signal(false);
  protected readonly data = signal<TimelineResponse | null>(null);
  protected readonly journal = signal<JournalDay[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly from = computed(() => new Date(this.data()?.timeline.from ?? Date.now()).getTime());
  private readonly to = computed(() => new Date(this.data()?.timeline.to ?? Date.now()).getTime());
  private x = (t: number) => ((t - this.from()) / Math.max(1, this.to() - this.from())) * W;

  protected readonly fromLabel = computed(() => new Date(this.from()).toLocaleDateString('it-IT'));
  protected readonly toLabel = computed(() => new Date(this.to()).toLocaleDateString('it-IT'));
  protected readonly nowX = computed(() => this.x(Date.now()));

  protected readonly ticks = computed(() => {
    const days = (this.to() - this.from()) / 86400_000;
    const every = days > 100 ? 30 : days > 40 ? 7 : days > 15 ? 3 : 1;
    const out: { x: number; label: string }[] = [];
    const d = new Date(this.from());
    d.setHours(0, 0, 0, 0);
    for (let i = 0; d.getTime() <= this.to(); i++, d.setDate(d.getDate() + 1)) {
      if (d.getTime() < this.from() || i % every !== 0) continue;
      out.push({ x: this.x(d.getTime()), label: d.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' }) });
    }
    return out;
  });

  protected readonly groups = computed<Group[]>(() => {
    const d = this.data();
    if (!d) return [];
    const stepMs = d.stepHours * 3600_000;
    const all = this.showAll();

    const zones: Row[] = d.timeline.zones
      .filter((t) => all || t.peak > 0.15)
      .sort((a, b) => b.peak - a.peak)
      .map((t) => this.row(t, stepMs, (s) => qualityColor(s.detail || null)));

    const byGroup = new Map<string, Row[]>();
    for (const t of d.timeline.signals) {
      const aspect = t.kind === 'transit-aspect' || t.kind === 'sky-aspect';
      if (!all && (aspect ? t.peak < 0.75 : t.peak < 0.05)) continue;
      // The Moon's aspects last hours: hide them unless asked, they would drown the slow signals.
      const lunarAspect = t.key.startsWith('transit:moon:') || t.key.startsWith('sky:moon:') || (t.kind === 'sky-aspect' && t.key.endsWith(':moon'));
      if (!all && lunarAspect) continue;
      const g = KIND_GROUP[t.kind] ?? 'Altro';
      const color = KIND_COLOR[t.kind] ?? qualityColor(t.tags['quality']);
      byGroup.set(g, [...(byGroup.get(g) ?? []), this.row(t, stepMs, () => color)]);
    }

    const journalRows = this.journalRows();
    const groups: Group[] = [{ name: 'Corpo (zone)', rows: zones }];
    for (const name of ['Cielo', 'Transiti sul tema natale', 'Aspetti nel cielo', 'Luna e luce', 'Sole e magnetosfera', 'Meteo']) {
      const rows = byGroup.get(name);
      if (rows?.length) groups.push({ name, rows });
    }
    if (journalRows.length) groups.push({ name: 'Diario', rows: journalRows });
    return groups;
  });

  constructor() {
    effect(() => {
      const p = this.store.current();
      const r = RANGES[this.range()];
      if (p) void this.load(p.id, r.back, r.fwd);
    });
  }

  private async load(id: number, back: number, fwd: number) {
    this.loading.set(true);
    this.error.set(null);
    const now = Date.now();
    const from = new Date(now - back * 86400_000);
    const to = new Date(now + fwd * 86400_000);
    const step = back + fwd > 100 ? 12 : 6;
    try {
      const [tl, journal] = await Promise.all([
        this.api.timeline(id, from.toISOString(), to.toISOString(), step),
        this.api.journal(id, from.toISOString().slice(0, 10), to.toISOString().slice(0, 10)),
      ]);
      this.data.set(tl);
      this.journal.set(journal);
    } catch (e) {
      this.error.set('Impossibile caricare la timeline. Il backend è avviato?');
      console.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  private row(t: SignalTrack, stepMs: number, color: (s: SignalTrack['samples'][number]) => string): Row {
    const w = Math.max(0.6, (stepMs / Math.max(1, this.to() - this.from())) * W);
    return {
      key: t.key,
      label: t.label,
      peak: t.peak,
      bars: t.samples
        .filter((s) => s.intensity > 0.01)
        .map((s) => ({
          x: this.x(new Date(s.at).getTime()),
          w,
          h: 4 + 16 * s.intensity,
          color: color(s),
          opacity: 0.35 + 0.65 * s.intensity,
          title: `${new Date(s.at).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' })} · ${s.detail || ''} · ${Math.round(s.intensity * 100)}%`,
        })),
    };
  }

  private journalRows(): Row[] {
    const days = this.journal();
    if (!days.length) return [];
    const w = (86400_000 / Math.max(1, this.to() - this.from())) * W * 0.85;
    return METRICS.map((m) => ({
      key: `journal:${m}`,
      label: METRIC_NAME[m],
      peak: 1,
      bars: days
        .filter((d) => d[m] != null)
        .map((d) => {
          const v = d[m] as number;
          return {
            x: this.x(new Date(d.date + 'T00:00:00').getTime()),
            w,
            h: 4 * v,
            color: 'var(--gold)',
            opacity: 0.85,
            title: `${d.date} · ${METRIC_NAME[m]} ${v}/5`,
          };
        }),
    })).filter((r) => r.bars.length > 0);
  }
}
