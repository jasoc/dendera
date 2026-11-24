import { SchoolPicker } from '../components/school-picker';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { ConsensusStore } from '../core/consensus-store';
import { JournalDay, METRICS, SignalTrack, TimelineResponse } from '../core/models';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { KIND_GROUP, METRIC_NAME, qualityColor } from '../core/symbols';
import { CatalogStore, schoolOfTrack } from '../core/catalog-store';
import { Icon } from '../components/icon';

interface Bar { x: number; w: number; h: number; color: string; opacity: number; title: string }
interface Reader { id: string; name: string; icon: string }
interface Row { key: string; label: string; bars: Bar[]; peak: number; icon?: string; iconTitle?: string; readers?: Reader[] }
type Section = 'consequences' | 'facts' | 'journal';
interface Group {
  section: Section;
  name: string;
  icon?: string;
  note?: string;
  derived?: boolean;
  readers?: Reader[];
  rows: Row[];
}

const SECTION: Record<Section, { label: string; icon: string; note: string }> = {
  consequences: {
    label: 'Conseguenze',
    icon: 'consequences',
    note: 'Come le scuole attive interpretano i segnali sul corpo (punteggio della zona, colore = qualità dominante).',
  },
  facts: {
    label: 'Segnali',
    icon: 'facts',
    note: 'I fatti, raggruppati per influenza (la fonte). Le icone a destra: le scuole attive che leggono quel tipo di segnale.',
  },
  journal: { label: 'Diario', icon: 'journal', note: 'Come sei stato tu, giorno per giorno (1–5).' },
};
const EPHEMERIS_ORDER = ['Transiti sul tema natale', 'Cielo', 'Aspetti nel cielo', 'Luna e luce'];

const W = 1000;
const KIND_COLOR: Record<string, string> = {
  'retrograde-count': 'var(--q-inward)', retrograde: 'var(--q-inward)', 'slow-motion': 'var(--q-inward)', station: 'var(--q-pressure)',
  eclipse: 'var(--q-tension)', 'lunar-phase': 'var(--q-sensitivity)', 'lunar-distance': 'var(--q-sensitivity)',
  photoperiod: 'var(--q-depletion)', 'photoperiod-change': 'var(--q-depletion)', geomagnetic: 'var(--q-agitation)',
  pressure: 'var(--q-pressure)', 'pressure-change': 'var(--q-pressure)', 'temperature-swing': 'var(--q-tension)',
  humidity: 'var(--q-pressure)', dryness: 'var(--q-depletion)', wind: 'var(--q-agitation)',
  temperature: 'var(--q-tension)', schumann: 'var(--q-agitation)', 'solar-term': 'var(--q-sensitivity)',
  'organ-clock': 'var(--q-flow)', 'hd-gate': 'var(--q-fusion)',
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
  imports: [Icon, MatButtonToggleModule, MatSlideToggleModule, MatTooltipModule, SchoolPicker],
  template: `
    <header class="toolbar">
      <app-school-picker />
      <div class="when">
        <mat-button-toggle-group [value]="range()" (change)="range.set($event.value)" hideSingleSelectionIndicator aria-label="Periodo">
          @for (r of ranges; track r.label; let i = $index) {
            <mat-button-toggle [value]="i">{{ r.label }}</mat-button-toggle>
          }
        </mat-button-toggle-group>
        <mat-slide-toggle [checked]="showAll()" (change)="showAll.set($event.checked)">anche segnali deboli</mat-slide-toggle>
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

        @for (g of groups(); track g.section + g.name; let i = $index) {
          @if (i === 0 || groups()[i - 1].section !== g.section) {
            <div class="tl-section">
              <span class="kicker" [class.consequences]="g.section === 'consequences'" [class.facts]="g.section !== 'consequences'">
                <app-icon [name]="section[g.section].icon" [size]="15" /> {{ section[g.section].label }}
              </span>
              <span class="muted">{{ section[g.section].note }}</span>
            </div>
          }
          <h3 class="tl-group">
            @if (g.icon) { <app-icon [name]="g.icon" [size]="18" /> }
            <span>{{ g.name }}</span>
            @if (g.derived) { <span class="badge-derived">fonte derivata</span> }
            @if (g.note) { <small class="muted">{{ g.note }}</small> }
          </h3>
          @for (row of g.rows; track row.key) {
            <div class="tl-row">
              <div class="tl-label" [matTooltip]="row.iconTitle ? row.iconTitle + ' · ' + row.label : row.label" matTooltipShowDelay="500">
                @if (row.icon) { <app-icon [name]="row.icon" [size]="14" class="tl-icon" /> }
                <span class="tl-text">{{ row.label }}</span>
                @if (row.readers?.length) {
                  <span class="readers">
                    @for (r of row.readers; track r.id) { <app-icon [name]="r.icon" [size]="13" /> }
                  </span>
                }
              </div>
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
    .tl-section { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin: 26px 0 4px; padding-top: 14px; border-top: 1px solid var(--line-strong); font-size: 12px; }
    .tl-section:first-of-type { border-top: none; padding-top: 0; }
    .tl-section .kicker { margin: 0; }
    .tl-group { display: flex; align-items: center; gap: 8px; margin: 16px 0 6px; font-size: 13px; color: var(--text); }
    .tl-group app-icon { color: var(--gold); }
    .tl-group small { font-weight: 400; font-size: 11.5px; }
    .tl-row { display: grid; grid-template-columns: 240px 1fr; align-items: center; gap: 10px; min-width: 640px; position: relative; }
    .tl-row + .tl-row { border-top: 1px solid var(--line); }
    .tl-label { display: flex; align-items: center; gap: 5px; font-size: 12.5px; color: var(--text); min-width: 0; }
    .tl-text { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; flex: 1; }
    .when { gap: 14px; }
    .tl-svg { width: 100%; height: 20px; display: block; }
    .tl-icon { color: var(--mat-sys-tertiary); }
    .axis .tl-svg { height: 18px; }
    .tick { stroke: var(--muted); stroke-width: 1; vector-effect: non-scaling-stroke; }
    .now { stroke: var(--gold); stroke-width: 1.5; vector-effect: non-scaling-stroke; }
    .tick-labels { position: absolute; left: 250px; right: 0; top: -2px; height: 14px; pointer-events: none; }
    .tick-labels span { position: absolute; transform: translateX(-50%); font-size: 10.5px; color: var(--muted); white-space: nowrap; }
    @media (max-width: 700px) { .tl-row { grid-template-columns: 130px 1fr; } .tick-labels { left: 140px; } }
  `,
})
export class Timeline {
  private readonly api = inject(Api);
  private readonly store = inject(ProfileStore);
  protected readonly consensus = inject(ConsensusStore);
  private readonly catalog = inject(CatalogStore);

  protected readonly W = W;
  protected readonly section = SECTION;
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

    // Consequences: combined zone tracks, then per-school tracks (only with 2+ schools), labelled by school.
    const zoneRow = (t: SignalTrack) => this.row(t, stepMs, (s) => qualityColor(s.detail || null));
    const zones: Row[] = d.timeline.zones
      .filter((t) => !schoolOfTrack(t.key) && (all || t.peak > 0.15))
      .sort((a, b) => b.peak - a.peak)
      .map(zoneRow);
    const perSchool = new Map<string, Row[]>();
    for (const t of d.timeline.zones) {
      const school = schoolOfTrack(t.key);
      if (!school || (!all && t.peak <= 0.15)) continue;
      perSchool.set(school, [...(perSchool.get(school) ?? []), { ...zoneRow(t), label: t.label.split(' · ')[0] }]);
    }

    const active = this.store.schools();
    const groups: Group[] = [];
    if (zones.length) {
      groups.push({
        section: 'consequences',
        name: active.length > 1 ? 'Sul corpo · tutte le scuole attive insieme' : 'Sul corpo',
        icon: 'consequences',
        rows: zones,
      });
    }
    for (const [school, rows] of perSchool) {
      const info = this.catalog.school(school);
      groups.push({
        section: 'consequences',
        name: `Secondo ${info?.name ?? school}`,
        icon: info?.icon,
        rows: rows.sort((a, b) => b.peak - a.peak),
      });
    }

    // Facts: one group per influence, in catalog order; the ephemeris is split by theme.
    const bySource = new Map<string, Map<string, Row[]>>();
    for (const t of d.timeline.signals) {
      const aspect = t.kind === 'transit-aspect' || t.kind === 'sky-aspect';
      if (!all && (aspect ? t.peak < 0.75 : t.peak < 0.05)) continue;
      // The Moon's aspects last hours: hide them unless asked, they would drown the slow signals.
      const lunarAspect = t.key.startsWith('transit:moon:') || t.key.startsWith('sky:moon:') || (t.kind === 'sky-aspect' && t.key.endsWith(':moon'));
      if (!all && lunarAspect) continue;
      // The organ clock repeats every day and the Moon crosses a gate every few hours: noise at this scale.
      if (!all && (t.kind === 'organ-clock' || t.key === 'hd-gate:moon')) continue;
      const color = KIND_COLOR[t.kind] ?? qualityColor(t.tags['quality']);
      const info = this.catalog.influence(t.source);
      const row: Row = {
        ...this.row(t, stepMs, () => color),
        icon: info?.icon,
        iconTitle: info?.name,
        readers: this.readersOf(t.kind, active),
      };
      const sub = t.source === 'ephemeris' ? KIND_GROUP[t.kind] ?? 'Altro' : '';
      const subs = bySource.get(t.source) ?? new Map<string, Row[]>();
      subs.set(sub, [...(subs.get(sub) ?? []), row]);
      bySource.set(t.source, subs);
    }
    const order = this.catalog.influences().map((i) => i.info.id);
    for (const [source, subs] of [...bySource.entries()].sort((a, b) => rank(order, a[0]) - rank(order, b[0]))) {
      const info = this.catalog.influence(source);
      const subNames = [...subs.keys()].sort((a, b) => rank(EPHEMERIS_ORDER, a) - rank(EPHEMERIS_ORDER, b));
      for (const sub of subNames) {
        groups.push({
          section: 'facts',
          name: (info?.name ?? source) + (sub ? ` · ${sub}` : ''),
          icon: info?.icon,
          derived: info?.reliability === 'derived',
          note: source === 'schumann' ? 'solo ultime 24 ore' : undefined,
          rows: subs.get(sub)!,
        });
      }
    }

    const journalRows = this.journalRows();
    if (journalRows.length) groups.push({ section: 'journal', name: 'Il tuo diario', icon: 'journal', rows: journalRows });
    return groups;
  });

  /** Active schools with at least one rule on this kind of signal. */
  private readersOf(kind: string, active: string[]): Reader[] {
    return this.catalog
      .schools()
      .filter((s) => active.includes(s.id) && s.rules.some((r) => r.kind === kind))
      .map((s) => ({ id: s.id, name: s.name, icon: s.icon }));
  }

  constructor() {
    effect(() => {
      const p = this.store.current();
      const r = RANGES[this.range()];
      const consensus = this.consensus.mode();
      if (p) void this.load(p.id, r.back, r.fwd, consensus);
    });
  }

  private async load(id: number, back: number, fwd: number, consensus: string) {
    this.loading.set(true);
    this.error.set(null);
    const now = Date.now();
    const from = new Date(now - back * 86400_000);
    const to = new Date(now + fwd * 86400_000);
    const step = back + fwd > 100 ? 12 : 6;
    try {
      const [tl, journal] = await Promise.all([
        this.api.timeline(id, from.toISOString(), to.toISOString(), step, consensus),
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

function rank(order: string[], key: string) {
  const i = order.indexOf(key);
  return i < 0 ? 99 : i;
}
