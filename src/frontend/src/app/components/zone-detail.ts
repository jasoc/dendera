import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ZoneActivation, ZoneContribution } from '../core/models';
import { QUALITY_NAME, qualityColor } from '../core/symbols';
import { CatalogStore } from '../core/catalog-store';
import { Icon } from './icon';

interface QualityBlock {
  id: string;
  name: string;
  color: string;
  score: number;
  raw: number;
  contributions: (ZoneContribution & { share: number })[];
}

/**
 * The consequences on one zone: total score, how it is reached, the split per school and, per quality, a shaded
 * bar (rarity of the node split by the qualities' shares, same colour as the body map) with the contributions.
 */
@Component({
  selector: 'app-zone-detail',
  imports: [DecimalPipe, MatTooltipModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let z = zone();
    <header class="zd-head">
      <h3>{{ z.label }} <small class="layer">{{ layerLabel() }}</small></h3>
      <span class="zd-pct" [style.color]="color(z.dominantQuality)" matTooltip="Rarità dell'influsso">{{ z.score * 100 | number: '1.0-0' }}%</span>
    </header>
    @if (description(); as d) { <p class="desc muted">{{ d }}</p> }
    <div class="meter big"><span [style.width.%]="z.score * 100" [style.--q]="color(z.dominantQuality)"></span></div>
    <p class="influx">Rarità dell'influsso: @if (z.score > 0) { secondo le scuole attive questo nodo è
      <b>più sollecitato che nel {{ z.score * 100 | number: '1.0-0' }}% dei momenti</b> del cielo di riferimento. }
      @else { nessun segnale tocca questo nodo ora. }
      <b>Non misura come stai</b>: la direzione la dà la qualità (flusso sostiene, svuotamento toglie…); se lo senti davvero
      lo dice il diario.</p>

    <p class="how muted">
      Ogni regola di una scuola aggiunge un contributo (peso × intensità del segnale
      @if (z.natalBaseline > 0.05) { × sensibilità natale }).
      I contributi si sommano; la somma è confrontata con quella di ogni scuola su migliaia di momenti di un cielo
      sintetico (1900–2100, 500 temi; per il meteo, il clima del tuo luogo): 50% è un momento ordinario, 95% è raro.
      @if (uncalibrated().length > 0) { {{ uncalibrated().join(', ') }}: tabella da ricalibrare, qui vale la scala
        provvisoria <b>1 − e<sup>−somma/{{ saturation() }}</sup></b>. }
      @if (schools().length > 1) { Più scuole attive: il totale è la <b>media</b> delle scuole che sanno parlare di
        questo nodo (anche quelle che oggi dicono zero), così più scuole non gonfiano nulla e il disaccordo si vede qui sotto. }
    </p>

    @if (z.natalBaseline > 0.05) {
      <p class="natal"><app-icon name="profile" [size]="16" /> Sensibilità natale {{ z.natalBaseline * 100 | number: '1.0-0' }}%:
        il tuo tema rende questa zona più ricettiva.</p>
    }

    @if (schools().length > 1) {
      <h4>Per scuola</h4>
      <ul class="rows">
        @for (b of schools(); track b.id) {
          <li class="row" [matTooltip]="b.name">
            <app-icon [name]="b.icon" [size]="17" class="gold" />
            <span class="name">{{ b.name }}</span>
            <div class="meter"><span [style.width.%]="b.score * 100" style="--q: var(--gold)"></span></div>
            <span class="pct">{{ b.score * 100 | number: '1.0-0' }}%</span>
          </li>
        }
      </ul>
    }

    @if (qualities().length) {
      <h4>Conseguenze per qualità</h4>
      @for (q of qualities(); track q.id) {
        <section class="quality" [style.--q]="q.color">
          <div class="row q-row" [matTooltip]="'somma dei contributi ' + (q.raw | number: '1.2-2')">
            <span class="dot"></span>
            <span class="name q-name">{{ q.name }}</span>
            <div class="meter"><span [style.width.%]="q.score * 100"></span></div>
            <span class="pct">{{ q.score * 100 | number: '1.0-0' }}%</span>
          </div>
          <ul class="contribs">
            @for (c of q.contributions; track $index) {
              <li>
                <app-icon [name]="schoolIcon(c.schoolId)" [size]="15" class="gold" [matTooltip]="schoolName(c.schoolId)" />
                <div class="c-body">
                  <div class="c-text">{{ c.text }}</div>
                  <div class="c-meta">
                    <span class="share"><span [style.width.%]="c.share * 100"></span></span>
                    <span>+{{ c.amount | number: '1.2-2' }}</span>
                    <span class="muted">· {{ schoolName(c.schoolId) }} ·
                      @if (fromBody(c)) { dall'organo: } @else { dal segnale: } {{ c.signalLabel }}</span>
                  </div>
                </div>
              </li>
            }
          </ul>
        </section>
      }
    } @else {
      <p class="muted">Nessuna scuola attiva legge qualcosa su questa zona, ora.</p>
    }
  `,
  styles: `
    :host { display: block; margin-top: 14px; border-top: 1px solid var(--line); padding-top: 12px; }
    .zd-head { display: flex; justify-content: space-between; align-items: baseline; }
    .layer { font-family: var(--font-ui); font-size: 11px; color: var(--muted); letter-spacing: .08em; text-transform: uppercase; margin-left: 6px; }
    .desc { font-size: 12.5px; margin: 4px 0 0; line-height: 1.45; }
    .influx { font-size: 12px; line-height: 1.5; margin: 8px 0 0; padding: 8px 10px; border-radius: 12px; color: var(--muted);
      background: color-mix(in srgb, var(--mat-sys-primary-container) 35%, transparent); }
    .influx b { color: var(--text); font-weight: 500; }
    .zd-head h3 { font-size: 17px; margin: 0; color: var(--text); font-family: var(--font-brand); letter-spacing: .03em; }
    .zd-pct { font-size: 22px; font-weight: 600; font-variant-numeric: tabular-nums; }
    h4 { font-size: 11px; text-transform: uppercase; letter-spacing: .12em; color: var(--muted); font-weight: 500; margin: 16px 0 8px; }
    .how { font-size: 12px; line-height: 1.5; margin: 10px 0; }
    .how b { color: var(--text); font-weight: 500; }
    .natal { display: flex; align-items: center; gap: 6px; font-size: 12.5px; color: var(--gold); margin: 6px 0; }

    /* Shaded meters: transparent → quality colour, the same "glow" as the body map. */
    .meter { height: 8px; border-radius: 999px; background: color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent); overflow: hidden; }
    .meter.big { height: 12px; margin-top: 6px; }
    .meter span {
      display: block; height: 100%; border-radius: inherit;
      background: linear-gradient(90deg, color-mix(in srgb, var(--q, var(--gold)) 15%, transparent), var(--q, var(--gold)));
      box-shadow: 0 0 12px color-mix(in srgb, var(--q, var(--gold)) 55%, transparent);
    }
    .rows { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 6px; }
    .row { display: grid; grid-template-columns: 18px minmax(110px, auto) 1fr 40px; gap: 8px; align-items: center; font-size: 12.5px; }
    .pct { text-align: right; font-variant-numeric: tabular-nums; color: var(--muted); }
    .gold { color: var(--gold); }

    .quality { margin-bottom: 14px; }
    .q-row .dot { width: 12px; height: 12px; border-radius: 50%; background: var(--q); box-shadow: 0 0 8px var(--q); margin: 0 3px; }
    .q-name { font-weight: 600; color: var(--q); text-transform: capitalize; }
    .q-row .pct { color: var(--text); }
    .contribs { list-style: none; margin: 6px 0 0 9px; padding: 0 0 0 16px; border-left: 2px solid color-mix(in srgb, var(--q) 40%, transparent);
      display: flex; flex-direction: column; gap: 8px; }
    .contribs li { display: grid; grid-template-columns: 16px 1fr; gap: 8px; font-size: 12.5px; }
    .c-text { line-height: 1.35; }
    .c-meta { display: flex; align-items: center; gap: 6px; font-size: 11.5px; margin-top: 3px; font-variant-numeric: tabular-nums; flex-wrap: wrap; }
    .share { width: 54px; height: 4px; border-radius: 999px; background: color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent); overflow: hidden; display: inline-block; }
    .share span { display: block; height: 100%; background: var(--q); }
  `,
})
export class ZoneDetail {
  private readonly catalog = inject(CatalogStore);
  readonly zone = input.required<ZoneActivation>();

  protected readonly schools = computed(() =>
    Object.entries(this.zone().bySchool)
      .filter(([, score]) => score > 0)
      .map(([id, score]) => ({ id, score, name: this.schoolName(id), icon: this.schoolIcon(id) }))
      .sort((a, b) => b.score - a.score),
  );

  /** Schools at work here still on the fallback scale (table missing or stale). */
  protected readonly uncalibrated = computed(() =>
    this.schools().filter((s) => this.catalog.school(s.id)?.calibration !== 'Calibrated').map((s) => s.name),
  );

  /** Saturation of the uncalibrated schools at work here (fallback scale); shown in the formula. */
  protected readonly saturation = computed(() => {
    const values = [...new Set(this.schools()
      .filter((s) => this.catalog.school(s.id)?.calibration !== 'Calibrated')
      .map((s) => this.catalog.school(s.id)?.saturation ?? 2.5))];
    return values.length === 1 ? String(values[0]).replace('.', ',') : 's';
  });

  protected readonly qualities = computed<QualityBlock[]>(() => {
    const z = this.zone();
    const max = Math.max(1e-9, ...z.contributions.map((c) => c.amount));
    return Object.entries(z.qualityScores)
      .map(([id, score]) => ({
        id,
        name: QUALITY_NAME[id] ?? id,
        color: qualityColor(id),
        score,
        raw: z.byQuality[id] ?? 0,
        contributions: z.contributions.filter((c) => c.quality === id).map((c) => ({ ...c, share: c.amount / max })),
      }))
      .sort((a, b) => b.score - a.score);
  });

  private readonly node = computed(() => this.catalog.catalog()?.zones.find((n) => n.id === this.zone().zone));
  protected readonly description = computed(() => this.node()?.description ?? null);
  protected readonly layerLabel = computed(() => this.node()?.layerLabel ?? '');
  /** Second-pass contributions come from another node of the body ("node:liver"), not from an influence. */
  protected fromBody = (c: ZoneContribution) => c.signalKey.startsWith('node:');

  protected color = qualityColor;
  protected schoolName = (id: string) => this.catalog.school(id)?.name ?? id;
  protected schoolIcon = (id: string) => this.catalog.school(id)?.icon ?? id;
}
