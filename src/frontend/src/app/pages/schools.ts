import { Component, computed, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { CatalogStore, PROVIDER_KIND } from '../core/catalog-store';
import { ProfileStore } from '../core/profile-store';
import { Icon } from '../components/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';

/**
 * "Scuole": static catalog of what the code implements. Influences are the sources of facts (signals); schools
 * read those facts and map them onto the same body. Readable without knowing any astrology.
 */
@Component({
  selector: 'app-schools',
  imports: [Icon, DecimalPipe, MatExpansionModule, MatSlideToggleModule, MatTooltipModule],
  template: `
    <section class="panel intro-panel">
      <h2>Scuole e influenze <small>cosa osserva Dendera e come lo interpreta</small></h2>
      <p class="intro">
        Dendera separa due cose. Le <b>influenze</b> sono le fonti dei dati: il cielo calcolato con le effemeridi,
        il campo magnetico terrestre, il meteo del luogo in cui vivi e altro. Producono <b>segnali</b>: fatti,
        senza interpretazione. Le <b>scuole</b> sono modi diversi di leggere quei fatti: ognuna dice quali
        influenze guarda e, con le sue regole, quali <b>conseguenze</b> hanno sul corpo. Puoi accendere e spegnere
        le scuole dai chip in alto: i segnali restano, cambiano solo le conseguenze. Il diario resta il banco di
        prova di ogni scuola.
      </p>
      @if (!catalog.catalog()) { <p class="muted">Caricamento del catalogo…</p> }
    </section>

    <span class="kicker consequences section-kicker"><app-icon name="consequences" [size]="15" /> Conseguenze · chi interpreta</span>
    <h2 class="section-title">Scuole <small>{{ catalog.schools().length }}</small></h2>
    <div class="cards">
      @for (s of catalog.schools(); track s.id) {
        <article class="panel card" [class.off]="store.current() && !store.schools().includes(s.id)">
          <header class="card-head">
            <app-icon [name]="s.icon" [size]="34" class="big-icon" />
            <div>
              <h3>{{ s.name }}</h3>
              <p class="muted tradition">{{ s.tradition }}</p>
              @if (s.calibration !== 'Calibrated') {
                <span class="recal" matTooltip="La tabella di rarità non corrisponde più alle regole: finché non si ripete il rituale di calibrazione vale la scala provvisoria">da ricalibrare</span>
              }
            </div>
            @if (store.current()) {
              <mat-slide-toggle [checked]="store.schools().includes(s.id)"
                                [disabled]="store.saving() || (store.schools().includes(s.id) && store.schools().length === 1)"
                                (change)="store.toggle(s.id)"
                                [matTooltip]="store.schools().includes(s.id) ? 'Attiva: clic per spegnerla' : 'Spenta: clic per accenderla'" />
            }
          </header>
          <p>{{ s.description }}</p>

          <h4>Legge queste influenze</h4>
          <div class="chips">
            @for (id of s.requires; track id) {
              <a class="chip infl" [href]="'#influence-' + id">
                <app-icon [name]="influenceIcon(id)" [size]="14" /> {{ influenceName(id) }}
              </a>
            }
          </div>

          <h4>Sensibilità natale</h4>
          <p class="muted">{{ s.baselineText }}</p>

          @if (s.references.length) {
            <h4>Fonti</h4>
            <ul class="refs">@for (r of s.references; track r) { <li>{{ r }}</li> }</ul>
          }

          <mat-accordion multi displayMode="flat">
          @for (t of s.tables; track t.title) {
            <mat-expansion-panel>
              <mat-expansion-panel-header><mat-panel-title>{{ t.title }}</mat-panel-title></mat-expansion-panel-header>
              <div class="table-wrap">
                <table class="data ref">
                  <thead><tr>@for (c of t.columns; track $index) { <th>{{ c }}</th> }</tr></thead>
                  <tbody>
                    @for (r of t.rows; track $index) {
                      <tr>@for (cell of r; track $index) { <td>{{ cell }}</td> }</tr>
                    }
                  </tbody>
                </table>
              </div>
            </mat-expansion-panel>
          }
          <mat-expansion-panel>
            <mat-expansion-panel-header><mat-panel-title>Regole ({{ s.rules.length }}): dal segnale alla conseguenza</mat-panel-title></mat-expansion-panel-header>
            <div class="table-wrap">
              <table class="data">
                <thead><tr><th>Quando arriva il segnale…</th><th>…conseguenza sulle zone</th><th class="num">Peso</th><th>Qualità</th><th>Spiegazione</th></tr></thead>
                <tbody>
                  @for (r of s.rules; track r.id) {
                    <tr>
                      <td>
                        <b>{{ r.kindLabel }}</b>
                        @for (t of r.conditions; track t) { <div class="muted small">{{ t }}</div> }
                        @if (r.minIntensity > 0) { <div class="muted small">intensità ≥ {{ r.minIntensity * 100 | number: '1.0-0' }}%</div> }
                      </td>
                      <td>
                        {{ zoneNames(r.zones) }}
                        @if (r.zonesFromText) { <div class="muted small">{{ r.zonesFromText }}</div> }
                      </td>
                      <td class="num">{{ r.weight | number: '1.2-2' }}</td>
                      <td>{{ r.qualityLabel }}</td>
                      <td class="small">{{ r.text }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </mat-expansion-panel>

          @if (s.correspondences.length) {
            <mat-expansion-panel>
              <mat-expansion-panel-header><mat-panel-title>Corrispondenze ({{ s.correspondences.length }}): simbolo → zona</mat-panel-title></mat-expansion-panel-header>
              <table class="data">
                <tbody>
                  @for (c of s.correspondences; track c.symbol) {
                    <tr><td>{{ c.label }}</td><td>{{ zoneNames(c.zones) || '—' }}</td></tr>
                  }
                </tbody>
              </table>
            </mat-expansion-panel>
          }
          </mat-accordion>
        </article>
      }
    </div>

    <span class="kicker facts section-kicker"><app-icon name="facts" [size]="15" /> Segnali · da dove vengono i fatti</span>
    <h2 class="section-title">Influenze <small>{{ catalog.influences().length }}</small></h2>
    <div class="cards">
      @for (i of catalog.influences(); track i.info.id) {
        <article class="panel card" [id]="'influence-' + i.info.id">
          <header class="card-head">
            <app-icon [name]="i.info.icon" [size]="34" class="big-icon" />
            <div>
              <h3>{{ i.info.name }}</h3>
              <p class="muted tradition">
                {{ providerKind(i.info.providerKind) }} ·
                <a [href]="i.info.url" target="_blank" rel="noopener">{{ i.info.providerName }}</a> ·
                {{ i.info.license }}
              </p>
            </div>
            @if (i.info.reliability === 'derived') {
              <span class="badge-derived" title="Dato elaborato da terzi, non una misura diretta">fonte derivata</span>
            } @else {
              <span class="badge-primary" title="Dato misurato o calcolato direttamente dalla fonte">fonte primaria</span>
            }
          </header>
          <p>{{ i.info.description }}</p>

          <h4>Segnali che produce</h4>
          <ul class="kinds">
            @for (k of i.info.kinds; track k.kind) { <li><code>{{ k.kind }}</code> {{ k.description }}</li> }
          </ul>

          <h4>Letta da</h4>
          <div class="chips">
            @for (s of readers(i.info.id); track s.id) {
              <span class="chip infl"><app-icon [name]="s.icon" [size]="14" /> {{ s.name }}</span>
            } @empty { <span class="muted">nessuna scuola</span> }
          </div>

          <p class="status small">
            @if (i.status; as st) {
              <span [class.ok]="st.ok" [class.error]="!st.ok">{{ st.ok ? '● attiva' : '○ errore' }}</span>
              · {{ st.samples }} campioni all'ultima lettura ({{ time(st.checkedAt) }})
              @if (st.error) { · {{ st.error }} }
            } @else {
              <span class="muted">non ancora interrogata da quando il server è partito</span>
            }
          </p>
        </article>
      }
    </div>

    @if (catalog.catalog(); as c) {
      <section class="panel body-model">
        <h2>Il corpo <small>il vocabolario comune di tutte le scuole</small></h2>
        <p class="muted">Ogni scuola proietta le sue conseguenze su questi nodi, a strati: dagli organi alle emozioni.
          Il vocabolario è quello delle tradizioni (le cinque emozioni del Neijing, i quattro temperamenti di Galeno,
          le tre celle del cervello della medicina medievale), così ogni scuola può scrivere le sue relazioni, per
          esempio "Fegato sollecitato → Collera" per la Medicina cinese. Quando più scuole sono attive, il punteggio di
          un nodo è la media delle scuole che sanno parlarne: più scuole non gonfiano nulla, e se non sono d'accordo
          lo vedi nella ripartizione per scuola.</p>
        @for (layer of layers(); track layer.id) {
          <h4>{{ layer.label }}</h4>
          <div class="nodes">
            @for (z of layer.nodes; track z.id) {
              <div class="node" [matTooltip]="z.description">
                <b>{{ z.label }}</b>
                @if (z.seat) { <small class="muted">in {{ zoneLabel(z.seat) }}</small> }
                <span class="muted desc">{{ z.description }}</span>
              </div>
            }
          </div>
        }
      </section>
    }
  `,
  styles: `
    .intro-panel { margin-bottom: 18px; }
    .section-title { margin: 4px 0 14px; }
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(360px, 1fr)); gap: 16px; align-items: start; }
    .card.off { opacity: .7; }
    .card p { line-height: 1.5; }
    .card-head { display: grid; grid-template-columns: auto 1fr auto; gap: 12px; align-items: center; }
    .card-head h3 { margin: 0; font-size: 16px; }
    .big-icon { color: var(--gold); }
    .tradition { margin: 2px 0 0; font-size: 12.5px; }
    .recal { display: inline-block; margin-top: 4px; padding: 1px 8px; border-radius: 999px; font-size: 11.5px;
      color: var(--mat-sys-on-error-container); background: var(--mat-sys-error-container); }
    h4 { font-size: 11.5px; text-transform: uppercase; letter-spacing: .1em; color: var(--muted); margin: 14px 0 6px; font-weight: 500; }
    .chips { display: flex; gap: 6px; flex-wrap: wrap; }
    .chip.infl { color: var(--text); text-decoration: none; }
    .chip.zone { color: var(--text); }
    .chip.zone.sys { border-style: dashed; }
    .refs, .kinds { margin: 0; padding-left: 18px; line-height: 1.5; }
    .kinds code { color: var(--gold); font-size: 11.5px; margin-right: 4px; }
    mat-accordion { display: block; margin-top: 14px; }
    mat-expansion-panel { background: var(--mat-sys-surface-container); }
    mat-panel-title { color: var(--gold); font-size: 13px; }
    .section-kicker { margin-top: 26px; }
    .body-model { margin-top: 26px; }
    .nodes { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 8px; }
    .node { padding: 10px 12px; border-radius: 14px; background: var(--mat-sys-surface-container); display: flex; flex-direction: column; gap: 3px; }
    .node .desc { font-size: 12px; line-height: 1.4; }
    table.ref td:first-child { color: var(--gold); white-space: nowrap; }
    .small { font-size: 12px; }
    .status { margin: 14px 0 0; }
    @media (max-width: 480px) { .cards { grid-template-columns: 1fr; } }
  `,
})
export class Schools {
  protected readonly catalog = inject(CatalogStore);
  protected readonly store = inject(ProfileStore);

  private readonly readersById = computed(() => {
    const map = new Map<string, { id: string; name: string; icon: string }[]>();
    for (const s of this.catalog.schools())
      for (const r of s.requires) map.set(r, [...(map.get(r) ?? []), { id: s.id, name: s.name, icon: s.icon }]);
    return map;
  });

  constructor() {
    void this.catalog.load(); // fresh influence status
  }

  protected readonly layers = computed(() => {
    const zones = this.catalog.catalog()?.zones ?? [];
    const ids = [...new Set(zones.map((z) => z.layer))];
    return ids.map((id) => {
      const nodes = zones.filter((z) => z.layer === id);
      return { id, label: nodes[0]?.layerLabel ?? id, nodes };
    });
  });
  protected zoneLabel = (id: string) => this.catalog.catalog()?.zones.find((z) => z.id === id)?.label ?? id;

  protected readers = (id: string) => this.readersById().get(id) ?? [];
  protected influenceName = (id: string) => this.catalog.influence(id)?.name ?? id;
  protected influenceIcon = (id: string) => this.catalog.influence(id)?.icon ?? id;
  protected providerKind = (k: string) => PROVIDER_KIND[k] ?? k;
  protected zoneNames = (zones: { label: string }[]) => zones.map((z) => z.label).join(', ');
  protected time = (iso: string) => new Date(iso).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' });
}
