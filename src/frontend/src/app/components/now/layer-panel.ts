import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CatalogStore } from '../../core/catalog-store';
import { BodyLayer, ZoneActivation } from '../../core/models';
import { qualityColor } from '../../core/symbols';
import { Icon } from '../icon';

interface LayerRow { id: BodyLayer; label: string; touched: number; top: number }

/**
 * The body by layers: pick a layer, see its nodes (strongest first) and choose one. The eye shows only that layer's
 * lights in the 3D body, so an emotion is easy to pick even where it sits on an organ.
 */
@Component({
  selector: 'app-layer-panel',
  imports: [Icon, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <span class="kicker consequences"><app-icon name="consequences" [size]="14" /> Il corpo, per strati</span>
      <button class="eye" [class.on]="isolate()" (click)="toggleIsolate()"
              [matTooltip]="isolate() ? 'Mostra tutti gli strati nel corpo' : 'Mostra nel corpo solo questo strato'">
        <app-icon [name]="isolate() ? 'eye' : 'eye-off'" [size]="18" />
      </button>
    </header>
    <nav class="layers" aria-label="Strati del corpo">
      @for (l of layers(); track l.id) {
        <button class="layer" [class.active]="l.id === layer()" (click)="choose(l.id)">
          <span class="l-name">{{ l.label }}</span>
          @if (l.touched) { <span class="l-count">{{ l.touched }}</span> }
          <span class="l-glow" [style.opacity]="l.top"></span>
        </button>
      }
    </nav>
    <ul class="nodes">
      @for (z of nodes(); track z.zone) {
        <li>
          <button class="node" [class.selected]="z.zone === selected()" [class.quiet]="z.score <= 0.005"
                  (click)="pick.emit(z.zone)" [style.--q]="color(z.dominantQuality)">
            <span class="n-name">{{ z.label }}</span>
            <span class="n-meter"><span [style.width.%]="z.score * 100"></span></span>
            <span class="n-pct">{{ z.score > 0.005 ? pct(z.score) : '—' }}</span>
          </button>
        </li>
      }
    </ul>
  `,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 0; }
    .head { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 8px; }
    .head .kicker { margin: 0; }
    .eye { border: 1px solid var(--line); background: transparent; color: var(--muted); border-radius: 50%; width: 32px; height: 32px;
      display: grid; place-items: center; cursor: pointer; }
    .eye.on { color: var(--gold); border-color: color-mix(in srgb, var(--gold) 60%, transparent); }
    .layers { display: flex; flex-wrap: wrap; gap: 4px; margin-bottom: 10px; }
    .layer { position: relative; overflow: hidden; border: 1px solid var(--line); background: transparent; color: var(--muted);
      border-radius: 999px; padding: 4px 10px; font: inherit; font-size: 12px; cursor: pointer; display: inline-flex; gap: 6px; align-items: center; }
    .layer.active { color: var(--text); border-color: color-mix(in srgb, var(--gold) 70%, transparent);
      background: color-mix(in srgb, var(--gold) 12%, transparent); }
    .l-count { font-size: 10.5px; color: var(--gold); }
    .l-glow { position: absolute; inset: auto 0 0 0; height: 2px; background: var(--gold); }
    .nodes { list-style: none; margin: 0; padding: 0; overflow-y: auto; min-height: 0; display: flex; flex-direction: column; gap: 2px; }
    .node { width: 100%; display: grid; grid-template-columns: minmax(0, 1fr) 64px 34px; gap: 8px; align-items: center;
      border: 0; background: transparent; color: var(--text); font: inherit; font-size: 13px; text-align: left;
      padding: 6px 8px; border-radius: 10px; cursor: pointer; }
    .node:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .node.selected { background: color-mix(in srgb, var(--gold) 14%, transparent); }
    .node.quiet { color: var(--muted); }
    .n-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .n-meter { height: 5px; border-radius: 999px; background: color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent); overflow: hidden; }
    .n-meter span { display: block; height: 100%; border-radius: inherit;
      background: linear-gradient(90deg, color-mix(in srgb, var(--q) 15%, transparent), var(--q)); }
    .n-pct { text-align: right; font-size: 11.5px; color: var(--muted); font-variant-numeric: tabular-nums; }
  `,
})
export class LayerPanel {
  private readonly catalog = inject(CatalogStore);

  readonly zones = input.required<ZoneActivation[]>();
  readonly selected = input<string | null>(null);
  readonly pick = output<string>();
  /** The chosen layer and whether the 3D body shows only it: owned by the page (URL). */
  readonly layer = input<BodyLayer>('soma');
  readonly isolate = input(false);
  readonly layerChange = output<BodyLayer>();
  readonly isolateChange = output<boolean>();

  protected readonly layers = computed<LayerRow[]>(() => {
    const labels = new Map((this.catalog.catalog()?.zones ?? []).map((z) => [z.layer, z.layerLabel]));
    const rows = new Map<BodyLayer, LayerRow>();
    for (const z of this.zones()) {
      const row = rows.get(z.layer) ?? { id: z.layer, label: labels.get(z.layer) ?? z.layer, touched: 0, top: 0 };
      if (z.score > 0.005) row.touched++;
      row.top = Math.max(row.top, z.score);
      rows.set(z.layer, row);
    }
    return [...rows.values()];
  });

  protected readonly nodes = computed(() =>
    this.zones().filter((z) => z.layer === this.layer()).sort((a, b) => b.score - a.score),
  );

  protected choose(layer: BodyLayer) {
    this.layerChange.emit(layer);
  }

  protected toggleIsolate() {
    this.isolateChange.emit(!this.isolate());
  }

  protected color = qualityColor;
  protected pct = (v: number) => `${Math.round(v * 100)}%`;
}
