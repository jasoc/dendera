import { Component, computed, effect, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { CorrelationResponse } from '../core/models';
import { CatalogStore, schoolOfTrack } from '../core/catalog-store';
import { Icon } from '../components/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';

const EVIDENCE: Record<string, string> = { strong: 'forte', hint: 'indizio', none: '—' };

/** Which signals (and which schools' zone readings) actually move this person's journal. */
@Component({
  selector: 'app-insights',
  imports: [DecimalPipe, Icon, MatSlideToggleModule, MatTooltipModule],
  template: `
    <section class="panel">
      <h2>Corrispondenze <small>cosa ti muove davvero</small></h2>
      <p class="legend-kinds">
        <span class="kind-tag facts">segnale</span> un fatto osservato (Kp, un transito, la pressione…)
        <span class="kind-tag consequences">conseguenza</span> una zona del corpo come la legge una scuola
      </p>
      <p class="intro">
        Ogni giorno i segnali di tutte le influenze vengono registrati accanto a come stai. Qui Dendera confronta
        ogni segnale e ogni zona del corpo con il tuo diario: le zone combinate e, se hai più scuole attive,
        quelle di ciascuna scuola separatamente, così puoi vedere quale scuola descrive meglio quello che senti.
        Per ogni coppia calcola la correlazione, un test a permutazione (1000 rimescolamenti) e la correzione
        Benjamini–Hochberg, perché provando centinaia di coppie qualche coincidenza esce sempre.
        Le regole che non trovano riscontro nel tuo diario si possono ridimensionare.
      </p>

      @if (loading()) { <p class="muted">Confronto del cielo con il diario…</p> }
      @if (error()) { <p class="error">{{ error() }}</p> }

      @if (data(); as d) {
        @if (d.message) {
          <p class="notice">{{ d.message }}</p>
        } @else {
          <p class="muted">
            {{ d.journalDays }} giorni di diario ({{ d.from }} → {{ d.to }}), {{ d.tracksTested }} tracce confrontate,
            {{ strong() }} corrispondenze forti, {{ hints() }} indizi.
          </p>
          <mat-slide-toggle class="only" [checked]="onlyRelevant()" (change)="onlyRelevant.set($event.checked)">mostra solo forti e indizi</mat-slide-toggle>
          <div class="table-wrap">
            <table class="data">
              <thead>
                <tr>
                  <th>Evidenza</th><th>Diario</th><th>Segnale o conseguenza</th><th class="num">r</th>
                  <th class="num">con segnale</th><th class="num">senza</th><th class="num">giorni attivi</th>
                  <th class="num">p</th><th class="num">q</th>
                </tr>
              </thead>
              <tbody>
                @for (f of rows(); track f.metric + f.signalKey) {
                  <tr>
                    <td><span class="badge" [class]="f.evidence">{{ evidence[f.evidence] }}</span></td>
                    <td>{{ f.metricLabel }}</td>
                    <td [matTooltip]="f.signalKey" matTooltipShowDelay="600">
                      @if (f.signalKind === 'zone') {
                        <span class="kind-tag consequences" matTooltip="Conseguenza: zona del corpo calcolata dalle scuole">conseguenza</span>
                      } @else {
                        <span class="kind-tag facts" matTooltip="Segnale: un fatto osservato da un'influenza">segnale</span>
                      }
                      @if (iconOf(f.signalKey); as ic) { <app-icon [name]="ic.icon" [size]="16" class="row-icon" [matTooltip]="ic.name" /> }
                      {{ f.signalLabel }}
                    </td>
                    <td class="num" [class.neg]="f.pearson < 0">{{ f.pearson | number: '1.2-2' }}</td>
                    <td class="num">{{ f.meanWhenActive | number: '1.1-1' }}</td>
                    <td class="num">{{ f.meanWhenQuiet | number: '1.1-1' }}</td>
                    <td class="num">{{ f.activeDays }}/{{ f.days }}</td>
                    <td class="num">{{ f.pValue | number: '1.3-3' }}</td>
                    <td class="num">{{ f.qValue | number: '1.3-3' }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="9" class="muted">Nessuna corrispondenza sopra la soglia, per ora.</td></tr>
                }
              </tbody>
            </table>
          </div>
        }
      }
    </section>
  `,
  styles: `
    .only { margin: 4px 0 12px; display: block; }
    .legend-kinds { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; font-size: 12px; color: var(--muted); }
    .legend-kinds .kind-tag:not(:first-child) { margin-left: 10px; }
    .kind-tag { font-size: 10px; font-weight: 600; letter-spacing: .08em; text-transform: uppercase; padding: 2px 7px; border-radius: 999px; margin-right: 4px; white-space: nowrap; }
    .kind-tag.facts { color: var(--mat-sys-on-tertiary-container); background: var(--mat-sys-tertiary-container); }
    .kind-tag.consequences { color: var(--mat-sys-on-primary-container); background: var(--mat-sys-primary-container); }
  `,
})
export class Insights {
  private readonly api = inject(Api);
  private readonly store = inject(ProfileStore);
  private readonly catalog = inject(CatalogStore);

  protected readonly evidence = EVIDENCE;
  protected readonly data = signal<CorrelationResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly onlyRelevant = signal(true);

  protected readonly rows = computed(() => {
    const all = this.data()?.findings ?? [];
    return (this.onlyRelevant() ? all.filter((f) => f.evidence !== 'none') : all).slice(0, 200);
  });
  protected readonly strong = computed(() => this.data()?.findings.filter((f) => f.evidence === 'strong').length ?? 0);
  protected readonly hints = computed(() => this.data()?.findings.filter((f) => f.evidence === 'hint').length ?? 0);

  /** Per-school zone tracks carry the school icon; signal tracks the icon of their influence. */
  protected iconOf(key: string): { icon: string; name: string } | null {
    const school = schoolOfTrack(key);
    if (school) {
      const s = this.catalog.school(school);
      return s ? { icon: s.icon, name: s.name } : null;
    }
    const track = this.data()?.findings.find((f) => f.signalKey === key);
    if (!track || track.signalKind === 'zone') return null;
    const inf = this.catalog.influences().find((i) => i.info.kinds.some((k) => k.kind === track.signalKind));
    return inf ? { icon: inf.info.icon, name: inf.info.name } : null;
  }

  constructor() {
    effect(() => {
      const p = this.store.current();
      if (p) void this.load(p.id);
    });
  }

  private async load(id: number) {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(await this.api.correlations(id));
    } catch (e) {
      this.error.set('Impossibile calcolare le corrispondenze.');
      console.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
