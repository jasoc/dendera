import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { ReadingResponse, Signal } from '../core/models';
import { KIND_GROUP, qualityColor } from '../core/symbols';
import { CatalogStore } from '../core/catalog-store';
import { SkyWheel } from '../components/sky-wheel';
import { BodyMap } from '../components/body-map';
import { Icon } from '../components/icon';
import { ZoneDetail } from '../components/zone-detail';
import { Body3d } from '../components/body-3d/body-3d';

interface SignalRow { signal: Signal; readers: { id: string; name: string; icon: string }[] }
interface SubGroup { name: string | null; rows: SignalRow[] }
interface InfluenceGroup { id: string; name: string; icon: string; derived: boolean; count: number; subgroups: SubGroup[] }

const LAYER_NAME: Record<string, string> = {
  soma: 'corpo', systems: 'sistemi', temperament: 'temperamento', emotions: 'emozione', mind: 'mente',
  zangfu: 'zang-fu', spirits: 'shen', substances: 'sostanza', chakras: 'chakra', nadis: 'nadi',
};

/** Sub-groups inside the ephemeris influence, which emits many kinds. */
const EPHEMERIS_ORDER = ['Transiti sul tema natale', 'Cielo', 'Aspetti nel cielo', 'Luna e luce'];

/**
 * "Adesso": the facts (sky wheel, active signals grouped by the influence that produced them) and their
 * consequences (the body map: how the active schools read those facts), at one instant (scrubbable).
 */
@Component({
  selector: 'app-now',
  imports: [SkyWheel, BodyMap, Body3d, RouterLink, Icon, ZoneDetail, MatButtonModule, MatTooltipModule],
  template: `
    @if (!store.current()) {
      <section class="empty panel">
        <h2>Nessun profilo</h2>
        <p>Inserisci data, ora e luogo di nascita per calcolare il tema natale.</p>
        <a routerLink="/profilo" matButton="filled">Crea il profilo</a>
      </section>
    } @else {
      <header class="toolbar">
        <div class="when">
          <button matButton="outlined" (click)="shift(-24)" matTooltip="-1 giorno">−1g</button>
          <button matButton="outlined" (click)="shift(-6)" matTooltip="-6 ore">−6h</button>
          <input type="datetime-local" [value]="localValue()" (change)="setLocal($any($event.target).value)" />
          <button matButton="outlined" (click)="shift(6)" matTooltip="+6 ore">+6h</button>
          <button matButton="outlined" (click)="shift(24)" matTooltip="+1 giorno">+1g</button>
          <button matButton="filled" (click)="at.set(null)" [disabled]="at() === null">Adesso</button>
        </div>
        <div class="sources">
          @for (s of data()?.sources ?? []; track s.source) {
            <span class="chip" [class.bad]="!s.ok"
                  [matTooltip]="(s.ok ? s.samples + ' campioni' : 'errore: ' + s.error) + (derived(s.source) ? ' · fonte derivata' : '')">
              <span class="state">●</span>
              <app-icon [name]="influenceIcon(s.source)" [size]="16" />
              {{ influenceName(s.source) }}
              @if (derived(s.source)) { <span class="badge-derived">derivata</span> }
            </span>
          }
        </div>
      </header>

      @if (error()) { <p class="error">{{ error() }}</p> }

      @if (data(); as d) {
        <div class="now-grid" [class.loading]="loading()">
          <section class="panel body-panel">
            <span class="kicker consequences"><app-icon name="consequences" [size]="15" /> Conseguenze · il corpo</span>
            <h2>Corpo <small>come le scuole attive leggono i segnali, dagli organi alle emozioni · {{ when() }}</small></h2>
            <div class="body-layout">
              <app-body-3d [zones]="d.reading.zones" [sky]="d.reading.sky" [signals]="d.reading.signals"
                           [selected]="zone()" (pick)="zone.set($event)">
                <app-body-map [zones]="d.reading.zones" [selected]="zone()" (pick)="zone.set($event)" />
              </app-body-3d>
              <aside class="body-side">
                @if (zoneDetail(); as z) {
                  <button matButton class="back" (click)="zone.set(null)"><app-icon name="close" [size]="16" /> chiudi</button>
                  <app-zone-detail [zone]="z" />
                } @else {
                  <h3 class="side-title">Le conseguenze più forti, ora</h3>
                  <ul class="top">
                    @for (z of topZones(); track z.zone) {
                      <li (click)="zone.set(z.zone)" [style.--q]="color(z.dominantQuality)">
                        <span class="t-name">{{ z.label }} <small>{{ layerName(z.layer) }}</small></span>
                        <span class="t-meter"><span [style.width.%]="z.score * 100"></span></span>
                        <span class="t-pct">{{ pct(z.score) }}</span>
                      </li>
                    }
                  </ul>
                  <p class="muted hint">Tocca una luce nel corpo, o una riga qui sopra, per vedere quali scuole la
                    accendono e a partire da quali segnali. La percentuale è la rarità dell'influsso (più sollecitato che nell'N% dei momenti), non come stai.</p>
                }
              </aside>
            </div>
          </section>

          <section class="panel signals">
            <span class="kicker facts"><app-icon name="facts" [size]="15" /> Segnali · fatti, senza interpretazione</span>
            <h2>Segnali attivi <small>{{ visibleSignals().length }}</small></h2>
            <p class="muted small-note">Raggruppati per influenza (la fonte). A destra, le scuole attive che li leggono:
              senza icone, il segnale è osservato ma nessuna scuola attiva lo interpreta.</p>
            @for (g of groups(); track g.id) {
              <div class="infl-group">
                <h3 class="infl-head">
                  <app-icon [name]="g.icon" [size]="20" />
                  <span>{{ g.name }}</span>
                  <small class="muted">{{ g.count }}</small>
                  @if (g.derived) { <span class="badge-derived">fonte derivata</span> }
                </h3>
                @for (sg of g.subgroups; track sg.name) {
                  @if (sg.name) { <h4 class="sub-head">{{ sg.name }}</h4> }
                  <ul class="sig-list">
                    @for (r of sg.rows; track r.signal.key) {
                      <li [matTooltip]="r.signal.key" matTooltipShowDelay="600">
                        <div class="sig-head">
                          <app-icon class="sig-infl" [name]="g.icon" [size]="16" />
                          <span class="sig-label">{{ r.signal.label }}</span>
                          @if (r.readers.length) {
                            <span class="readers">
                              @for (s of r.readers; track s.id) {
                                <app-icon [name]="s.icon" [size]="16" [matTooltip]="'Letto da ' + s.name" />
                              }
                            </span>
                          } @else {
                            <span class="readers none">non letto</span>
                          }
                        </div>
                        <div class="sig-detail">
                          <span class="dot" [style.background]="color(r.signal.tags['quality'])"></span> {{ r.signal.detail }}
                        </div>
                        <div class="bar"><span [style.width.%]="r.signal.intensity * 100"></span></div>
                      </li>
                    }
                  </ul>
                }
              </div>
            }
          </section>

          <section class="panel sky-panel">
            <span class="kicker facts"><app-icon name="facts" [size]="15" /> Segnali · il cielo</span>
            <h2>Cielo <small>{{ when() }}</small></h2>
            <app-sky-wheel [sky]="d.reading.sky" [natal]="d.reading.natal" [signals]="d.reading.signals" />
            <p class="legend">
              <span><b class="gold">●</b> tema natale</span>
              <span><b>●</b> cielo attuale</span>
              <span><b class="inward">℞</b> retrogrado</span>
              <span>linee: transiti attivi (colore = qualità)</span>
            </p>
          </section>
        </div>
      } @else if (loading()) {
        <p class="muted">Calcolo del cielo…</p>
      }
    }
  `,
  styles: `
    .now-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(300px, 400px); gap: 16px; align-items: start; }
    .body-panel { grid-column: 1; grid-row: 1; }
    .signals { grid-column: 2; grid-row: 1 / span 2; max-height: none; }
    .sky-panel { grid-column: 1; grid-row: 2; }
    .sky-panel app-sky-wheel { display: block; max-width: 560px; margin: 0 auto; }
    .body-layout { display: grid; grid-template-columns: minmax(0, 1fr) 340px; gap: 18px; align-items: start; }
    .body-side { max-height: clamp(440px, 68vh, 760px); overflow-y: auto; padding-right: 4px; }
    .back { margin-bottom: -6px; }
    .side-title { margin-top: 0; }
    .top { list-style: none; padding: 0; margin: 0 0 12px; display: flex; flex-direction: column; gap: 4px; }
    .top li { display: grid; grid-template-columns: minmax(0, 1fr) 90px 38px; gap: 8px; align-items: center; cursor: pointer;
      padding: 6px 8px; border-radius: 12px; font-size: 13px; }
    .top li:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .t-name small { color: var(--muted); font-size: 10.5px; text-transform: uppercase; letter-spacing: .06em; margin-left: 4px; }
    .t-meter { height: 6px; border-radius: 999px; background: color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent); overflow: hidden; }
    .t-meter span { display: block; height: 100%; border-radius: inherit;
      background: linear-gradient(90deg, color-mix(in srgb, var(--q) 15%, transparent), var(--q)); }
    .t-pct { text-align: right; color: var(--muted); font-variant-numeric: tabular-nums; font-size: 12px; }
    @media (max-width: 1350px) { .body-layout { grid-template-columns: 1fr; } .body-side { max-height: none; } }
    @media (max-width: 1000px) {
      .now-grid { grid-template-columns: 1fr; }
      .body-panel, .signals, .sky-panel { grid-column: 1; grid-row: auto; }
    }
    .small-note { font-size: 12px; margin: -6px 0 6px; line-height: 1.45; }
    .infl-group { margin-top: 14px; padding-top: 10px; border-top: 1px solid var(--line); }
    .infl-head { display: flex; align-items: center; gap: 8px; font-size: 14px; color: var(--text); margin: 0 0 8px; }
    .infl-head app-icon { color: var(--gold); }
    .sub-head { font-size: 11px; text-transform: uppercase; letter-spacing: .1em; color: var(--muted); margin: 12px 0 6px; font-weight: 500; }
    .sig-head { display: flex; gap: 8px; align-items: center; font-size: 13px; }
    .sig-infl { color: var(--mat-sys-tertiary); }
    .sig-label { flex: 1; }
    .sig-detail { display: flex; align-items: center; gap: 6px; font-size: 11.5px; color: var(--muted); margin-left: 24px; }
    .sig-detail .dot { margin-top: 0; width: 7px; height: 7px; }
    .bar { margin-left: 24px; }
  `,
})
export class Now {
  private readonly api = inject(Api);
  protected readonly store = inject(ProfileStore);
  private readonly catalog = inject(CatalogStore);

  /** null = live "now". */
  protected readonly at = signal<Date | null>(null);
  protected readonly data = signal<ReadingResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly zone = signal<string | null>(null);

  protected readonly when = computed(() =>
    new Date(this.data()?.reading.at ?? Date.now()).toLocaleString('it-IT', { dateStyle: 'medium', timeStyle: 'short' }),
  );

  protected readonly localValue = computed(() => toLocalInput(this.at() ?? new Date()));

  protected readonly zoneDetail = computed(() => {
    const z = this.zone();
    return z ? this.data()?.reading.zones.find((x) => x.zone === z) ?? null : null;
  });

  protected readonly topZones = computed(() =>
    [...(this.data()?.reading.zones ?? [])].filter((z) => z.score > 0.02).sort((a, b) => b.score - a.score).slice(0, 10),
  );

  protected readonly visibleSignals = computed(() =>
    (this.data()?.reading.signals ?? []).filter((s) => s.kind !== 'placement' && s.intensity > 0.02),
  );

  /** signal key → schools that turned it into a consequence at this instant. */
  private readonly readersByKey = computed(() => {
    const map = new Map<string, Set<string>>();
    for (const z of this.data()?.reading.zones ?? [])
      for (const c of z.contributions) map.set(c.signalKey, (map.get(c.signalKey) ?? new Set()).add(c.schoolId));
    return map;
  });

  protected readonly groups = computed<InfluenceGroup[]>(() => {
    const readers = this.readersByKey();
    const order = this.catalog.influences().map((i) => i.info.id);
    const bySource = new Map<string, Signal[]>();
    for (const s of this.visibleSignals()) bySource.set(s.source, [...(bySource.get(s.source) ?? []), s]);

    return [...bySource.entries()]
      .sort((a, b) => rank(order, a[0]) - rank(order, b[0]))
      .map(([id, signals]) => {
        const info = this.catalog.influence(id);
        const row = (s: Signal): SignalRow => ({
          signal: s,
          readers: [...(readers.get(s.key) ?? [])].map((sid) => ({ id: sid, name: this.schoolName(sid), icon: this.schoolIcon(sid) })),
        });
        let subgroups: SubGroup[];
        if (id === 'ephemeris') {
          const by = new Map<string, Signal[]>();
          for (const s of signals) {
            const g = KIND_GROUP[s.kind] ?? 'Altro';
            by.set(g, [...(by.get(g) ?? []), s]);
          }
          subgroups = [...by.entries()]
            .sort((a, b) => rank(EPHEMERIS_ORDER, a[0]) - rank(EPHEMERIS_ORDER, b[0]))
            .map(([name, items]) => ({ name, rows: items.map(row) }));
        } else {
          subgroups = [{ name: null, rows: signals.map(row) }];
        }
        return {
          id,
          name: info?.name ?? id,
          icon: info?.icon ?? id,
          derived: info?.reliability === 'derived',
          count: signals.length,
          subgroups,
        };
      });
  });

  constructor() {
    effect(() => {
      const p = this.store.current();
      const at = this.at();
      if (p) void this.load(p.id, at);
    });
  }

  private async load(id: number, at: Date | null) {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(await this.api.reading(id, at?.toISOString()));
    } catch (e) {
      this.error.set('Impossibile calcolare la lettura. Il backend è avviato?');
      console.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  protected shift(hours: number) {
    const base = this.at() ?? new Date();
    this.at.set(new Date(base.getTime() + hours * 3600_000));
  }

  protected setLocal(v: string) {
    if (v) this.at.set(new Date(v));
  }

  protected influenceName = (id: string) => this.catalog.influence(id)?.name ?? id;
  protected influenceIcon = (id: string) => this.catalog.influence(id)?.icon ?? id;
  protected derived = (id: string) => this.catalog.influence(id)?.reliability === 'derived';
  protected schoolName = (id: string) => this.catalog.school(id)?.name ?? id;
  protected schoolIcon = (id: string) => this.catalog.school(id)?.icon ?? id;

  protected color = qualityColor;
  protected pct = (v: number) => `${Math.round(v * 100)}%`;
  protected layerName = (layer: string) => LAYER_NAME[layer] ?? layer;
}

function rank(order: string[], key: string) {
  const i = order.indexOf(key);
  return i < 0 ? 99 : i;
}

function toLocalInput(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
