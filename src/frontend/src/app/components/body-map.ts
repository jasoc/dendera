import { Component, computed, input, output } from '@angular/core';
import { ZoneActivation } from '../core/models';
import { QUALITY_NAME, qualityColor } from '../core/symbols';

/**
 * Anchor points of the local body zones (viewBox 0 0 200 460; the figure faces the viewer,
 * so the person's left is on the viewer's right). Ids MUST match Dendera.Core/Rules/BodyZones.cs.
 */
const ANCHORS: Record<string, { x: number; y: number; r: number }[]> = {
  head: [{ x: 100, y: 40, r: 22 }],
  throat: [{ x: 100, y: 80, r: 10 }],
  shoulders: [{ x: 60, y: 108, r: 13 }, { x: 140, y: 108, r: 13 }],
  lungs: [{ x: 84, y: 134, r: 13 }, { x: 116, y: 134, r: 13 }],
  heart: [{ x: 110, y: 150, r: 10 }],
  stomach: [{ x: 113, y: 180, r: 11 }],
  'solar-plexus': [{ x: 100, y: 167, r: 9 }],
  liver: [{ x: 86, y: 180, r: 11 }],
  gut: [{ x: 100, y: 212, r: 16 }],
  kidneys: [{ x: 82, y: 200, r: 8 }, { x: 118, y: 200, r: 8 }],
  pelvis: [{ x: 100, y: 248, r: 15 }],
  thighs: [{ x: 84, y: 292, r: 13 }, { x: 116, y: 292, r: 13 }],
  knees: [{ x: 84, y: 338, r: 9 }, { x: 116, y: 338, r: 9 }],
  calves: [{ x: 84, y: 378, r: 10 }, { x: 116, y: 378, r: 10 }],
  feet: [{ x: 82, y: 432, r: 10 }, { x: 118, y: 432, r: 10 }],
};

@Component({
  selector: 'app-body-map',
  template: `
    <div class="body-wrap">
      <svg viewBox="0 0 200 460" class="figure" role="img" aria-label="Mappa del corpo">
        <defs>
          @for (z of local(); track z.zone) {
            <radialGradient [attr.id]="'g-' + z.zone">
              <stop offset="0%" [attr.stop-color]="z.color" stop-opacity="0.95" />
              <stop offset="70%" [attr.stop-color]="z.color" stop-opacity="0.35" />
              <stop offset="100%" [attr.stop-color]="z.color" stop-opacity="0" />
            </radialGradient>
          }
        </defs>

        <g class="silhouette">
          <ellipse cx="100" cy="40" rx="20" ry="25" />
          <rect x="91" y="62" width="18" height="20" rx="6" />
          <path d="M58 96 Q100 84 142 96 L146 150 Q140 196 132 236 L68 236 Q60 196 54 150 Z" />
          <path d="M68 232 L132 232 Q136 252 130 268 L70 268 Q64 252 68 232 Z" />
          <path d="M56 100 Q44 104 42 124 L34 200 Q32 222 38 238 L46 238 Q48 220 50 204 L60 140 Z" />
          <path d="M144 100 Q156 104 158 124 L166 200 Q168 222 162 238 L154 238 Q152 220 150 204 L140 140 Z" />
          <path d="M72 262 L98 262 L96 340 L94 410 L92 424 L74 424 L74 410 L70 340 Z" />
          <path d="M102 262 L128 262 L130 340 L126 410 L126 424 L108 424 L106 410 L104 340 Z" />
          <ellipse cx="82" cy="434" rx="13" ry="7" />
          <ellipse cx="118" cy="434" rx="13" ry="7" />
        </g>

        @for (z of local(); track z.zone) {
          @for (a of z.anchors; track $index) {
            @if (z.baseline > 0.05) {
              <circle [attr.cx]="a.x" [attr.cy]="a.y" [attr.r]="a.r + 3" class="baseline"
                      [attr.stroke-opacity]="0.15 + 0.6 * z.baseline" />
            }
            <circle [attr.cx]="a.x" [attr.cy]="a.y" [attr.r]="a.r * (0.7 + 1.1 * z.score)"
                    [attr.fill]="'url(#g-' + z.zone + ')'" [attr.opacity]="0.12 + 0.88 * z.score"
                    class="glow" [class.selected]="selected() === z.zone"
                    (click)="pick.emit(z.zone)">
              <title>{{ z.label }} · {{ pct(z.score) }}{{ z.quality ? ' · ' + z.quality : '' }}</title>
            </circle>
          }
        }
      </svg>

      <div class="systemic">
        <div class="sys-title">Sistemi</div>
        @for (z of systemic(); track z.zone) {
          <button type="button" class="gauge" [class.selected]="selected() === z.zone" (click)="pick.emit(z.zone)">
            <span class="g-label">{{ z.label }}</span>
            <span class="g-bar"><span [style.width.%]="z.score * 100" [style.background]="z.color"></span></span>
            <span class="g-val">{{ pct(z.score) }}</span>
          </button>
        }
      </div>
    </div>
  `,
  styles: `
    :host { display: block; }
    .body-wrap { display: grid; grid-template-columns: minmax(150px, 1fr) minmax(150px, 1fr); gap: 12px; align-items: start; }
    .figure { width: 100%; max-height: 520px; }
    .silhouette * { fill: var(--panel-2); stroke: var(--line-strong); stroke-width: 1; }
    .baseline { fill: none; stroke: var(--gold); stroke-width: 1; stroke-dasharray: 2 2; }
    .glow { cursor: pointer; transition: r .4s ease, opacity .4s ease; }
    .glow.selected { stroke: var(--text); stroke-width: 1; }
    .systemic { display: flex; flex-direction: column; gap: 6px; }
    .sys-title { font-size: 12px; text-transform: uppercase; letter-spacing: .12em; color: var(--muted); margin-bottom: 2px; }
    .gauge { display: grid; grid-template-columns: 1fr 70px 34px; gap: 8px; align-items: center; background: none;
      border: 1px solid transparent; border-radius: 6px; padding: 4px 6px; color: var(--text); cursor: pointer; text-align: left; font: inherit; font-size: 13px; }
    .gauge:hover, .gauge.selected { border-color: var(--line-strong); background: var(--panel-2); }
    .g-bar { height: 6px; background: var(--line); border-radius: 3px; overflow: hidden; }
    .g-bar span { display: block; height: 100%; border-radius: 3px; }
    .g-val { font-size: 12px; color: var(--muted); text-align: right; font-variant-numeric: tabular-nums; }
    @media (max-width: 560px) { .body-wrap { grid-template-columns: 1fr; } }
  `,
})
export class BodyMap {
  readonly zones = input.required<ZoneActivation[]>();
  readonly selected = input<string | null>(null);
  readonly pick = output<string>();

  private readonly view = computed(() =>
    this.zones().map((z) => ({
      zone: z.zone,
      label: z.label,
      systemic: z.systemic,
      score: z.score,
      baseline: z.natalBaseline,
      color: qualityColor(z.dominantQuality),
      quality: z.dominantQuality ? QUALITY_NAME[z.dominantQuality] ?? z.dominantQuality : '',
      anchors: ANCHORS[z.zone] ?? [],
    })),
  );

  protected readonly local = computed(() => this.view().filter((z) => !z.systemic && z.anchors.length > 0));
  protected readonly systemic = computed(() => this.view().filter((z) => z.systemic));

  protected pct = (v: number) => `${Math.round(v * 100)}%`;
}
