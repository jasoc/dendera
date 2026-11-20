import { Component, computed, effect, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { ReadingResponse, Signal } from '../core/models';
import { KIND_GROUP, QUALITY_NAME, qualityColor } from '../core/symbols';
import { SkyWheel } from '../components/sky-wheel';
import { BodyMap } from '../components/body-map';

const GROUP_ORDER = [
  'Cielo', 'Transiti sul tema natale', 'Aspetti nel cielo', 'Luna e luce', 'Sole e magnetosfera', 'Meteo',
];

/** "Adesso": the sky wheel, the body map and every active signal at one instant (scrubbable). */
@Component({
  selector: 'app-now',
  imports: [SkyWheel, BodyMap, DecimalPipe, RouterLink],
  template: `
    @if (!store.current()) {
      <section class="empty panel">
        <h2>Nessun profilo</h2>
        <p>Inserisci data, ora e luogo di nascita per calcolare il tema natale.</p>
        <a routerLink="/profilo" class="btn primary">Crea il profilo</a>
      </section>
    } @else {
      <header class="toolbar">
        <div class="when">
          <button class="btn" (click)="shift(-24)" title="-1 giorno">−1g</button>
          <button class="btn" (click)="shift(-6)" title="-6 ore">−6h</button>
          <input type="datetime-local" [value]="localValue()" (change)="setLocal($any($event.target).value)" />
          <button class="btn" (click)="shift(6)" title="+6 ore">+6h</button>
          <button class="btn" (click)="shift(24)" title="+1 giorno">+1g</button>
          <button class="btn primary" (click)="at.set(null)" [disabled]="at() === null">Adesso</button>
        </div>
        <div class="sources">
          @for (s of data()?.sources ?? []; track s.source) {
            <span class="chip" [class.bad]="!s.ok" [title]="s.error ?? s.samples + ' campioni'">
              {{ s.ok ? '●' : '○' }} {{ s.source }}
            </span>
          }
        </div>
      </header>

      @if (error()) { <p class="error">{{ error() }}</p> }

      @if (data(); as d) {
        <div class="now-grid" [class.loading]="loading()">
          <section class="panel">
            <h2>Cielo <small>{{ when() }}</small></h2>
            <app-sky-wheel [sky]="d.reading.sky" [natal]="d.reading.natal" [signals]="d.reading.signals" />
            <p class="legend">
              <span><b class="gold">●</b> tema natale</span>
              <span><b>●</b> cielo attuale</span>
              <span><b class="inward">℞</b> retrogrado</span>
              <span>linee: transiti attivi (colore = qualità)</span>
            </p>
          </section>

          <section class="panel">
            <h2>Corpo</h2>
            <app-body-map [zones]="d.reading.zones" [selected]="zone()" (pick)="zone.set($event)" />
            @if (zoneDetail(); as z) {
              <div class="zone-detail">
                <h3>{{ z.label }} <span class="pct">{{ z.score * 100 | number: '1.0-0' }}%</span></h3>
                @if (z.natalBaseline > 0.05) {
                  <p class="muted">Sensibilità natale: {{ z.natalBaseline * 100 | number: '1.0-0' }}%</p>
                }
                @if (z.contributions.length === 0) {
                  <p class="muted">Nessuna influenza attiva su questa zona.</p>
                }
                <ul class="contribs">
                  @for (c of z.contributions.slice(0, 8); track $index) {
                    <li>
                      <span class="dot" [style.background]="color(c.quality)"></span>
                      <span class="c-text">{{ c.text }}</span>
                      <span class="c-q">{{ qname(c.quality) }} · {{ c.amount | number: '1.2-2' }}</span>
                    </li>
                  }
                </ul>
              </div>
            } @else {
              <p class="muted hint">Tocca una zona per vedere quali segnali la attivano.</p>
            }
          </section>

          <section class="panel signals">
            <h2>Segnali attivi <small>{{ visibleSignals().length }}</small></h2>
            @for (g of groups(); track g.name) {
              <h3>{{ g.name }}</h3>
              <ul class="sig-list">
                @for (s of g.items; track s.key) {
                  <li [title]="s.key">
                    <div class="sig-head">
                      <span class="dot" [style.background]="color(s.tags['quality'])"></span>
                      <span class="sig-label">{{ s.label }}</span>
                    </div>
                    <div class="sig-detail">{{ s.detail }}</div>
                    <div class="bar"><span [style.width.%]="s.intensity * 100"></span></div>
                  </li>
                }
              </ul>
            }
          </section>
        </div>
      } @else if (loading()) {
        <p class="muted">Calcolo del cielo…</p>
      }
    }
  `,
})
export class Now {
  private readonly api = inject(Api);
  protected readonly store = inject(ProfileStore);

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

  protected readonly visibleSignals = computed(() =>
    (this.data()?.reading.signals ?? []).filter((s) => s.kind !== 'placement' && s.intensity > 0.02),
  );

  protected readonly groups = computed(() => {
    const by = new Map<string, Signal[]>();
    for (const s of this.visibleSignals()) {
      const g = KIND_GROUP[s.kind] ?? 'Altro';
      by.set(g, [...(by.get(g) ?? []), s]);
    }
    return [...by.entries()]
      .sort((a, b) => order(a[0]) - order(b[0]))
      .map(([name, items]) => ({ name, items }));
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

  protected color = qualityColor;
  protected qname = (q: string) => QUALITY_NAME[q] ?? q;
}

function order(group: string) {
  const i = GROUP_ORDER.indexOf(group);
  return i < 0 ? 99 : i;
}

function toLocalInput(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
