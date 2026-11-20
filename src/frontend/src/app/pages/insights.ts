import { Component, computed, effect, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { CorrelationResponse } from '../core/models';

const EVIDENCE: Record<string, string> = { strong: 'forte', hint: 'indizio', none: '—' };

/** The Babylonian diary engine: which signals actually move this person's journal. */
@Component({
  selector: 'app-insights',
  imports: [DecimalPipe],
  template: `
    <section class="panel">
      <h2>Corrispondenze <small>cosa ti muove davvero</small></h2>
      <p class="intro">
        Come nei Diari Astronomici di Babilonia: ogni giorno il cielo e la terra vengono registrati accanto a come stai.
        Qui Dendera confronta ogni segnale (e ogni zona del corpo calcolata dalle regole) con il tuo diario.
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
          <label class="check"><input type="checkbox" [checked]="onlyRelevant()" (change)="onlyRelevant.set(!onlyRelevant())" /> mostra solo forti e indizi</label>
          <div class="table-wrap">
            <table class="data">
              <thead>
                <tr>
                  <th>Evidenza</th><th>Diario</th><th>Segnale</th><th class="num">r</th>
                  <th class="num">con segnale</th><th class="num">senza</th><th class="num">giorni attivi</th>
                  <th class="num">p</th><th class="num">q</th>
                </tr>
              </thead>
              <tbody>
                @for (f of rows(); track f.metric + f.signalKey) {
                  <tr>
                    <td><span class="badge" [class]="f.evidence">{{ evidence[f.evidence] }}</span></td>
                    <td>{{ f.metricLabel }}</td>
                    <td [title]="f.signalKey">{{ f.signalLabel }}</td>
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
})
export class Insights {
  private readonly api = inject(Api);
  private readonly store = inject(ProfileStore);

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
