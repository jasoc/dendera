import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SignalTrack, TimelineResponse } from '../../core/models';

interface Line { key: string; label: string; color: string; d: string; area?: string }

const HOURS = 48;
const COLORS = ['var(--c-signal)', '#ffd27a', '#ff8f6b'];

/**
 * Cause and effect in time: the consequence (its rarity) and the signals that feed it most, from two days before to
 * two days after the moment, with "now" marked. Says when the node rose above an ordinary moment and when it falls back.
 */
@Component({
  selector: 'app-time-trace',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) { <p class="note">Ricostruisco le 96 ore intorno a questo momento…</p> }
    @if (chart(); as c) {
      <svg viewBox="0 0 320 120" preserveAspectRatio="none" class="chart">
        @for (t of c.ticks; track t.x) {
          <line [attr.x1]="t.x" [attr.x2]="t.x" y1="0" y2="108" class="grid" />
          <text [attr.x]="t.x + 2" y="118" class="tick">{{ t.label }}</text>
        }
        <line x1="0" x2="320" [attr.y1]="y(0.5)" [attr.y2]="y(0.5)" class="half" />
        @for (l of c.lines; track l.key) {
          @if (l.area) { <path [attr.d]="l.area" class="area" /> }
          <path [attr.d]="l.d" [attr.stroke]="l.color" [class.main]="$first" class="line" />
        }
        <line [attr.x1]="c.nowX" [attr.x2]="c.nowX" y1="0" y2="108" class="now" />
      </svg>
      <ul class="tt-legend">
        @for (l of c.lines; track l.key) { <li><span class="sw" [style.background]="l.color"></span>{{ l.label }}</li> }
      </ul>
      @if (c.story) { <p class="story">{{ c.story }}</p> }
    }
  `,
  styles: `
    :host { display: block; }
    .chart { width: 100%; height: 130px; display: block; }
    .grid { stroke: var(--line); stroke-width: .5; }
    .half { stroke: var(--line-strong); stroke-dasharray: 2 3; stroke-width: .6; }
    .tick { font-family: var(--font-mono); font-size: 6.5px; fill: var(--muted); }
    .line { fill: none; stroke-width: 1; vector-effect: non-scaling-stroke; opacity: .85; }
    .line.main { stroke-width: 2; opacity: 1; }
    .area { fill: color-mix(in srgb, var(--c-node) 14%, transparent); }
    .now { stroke: var(--accent); stroke-width: 1; vector-effect: non-scaling-stroke; }
    .tt-legend { list-style: none; margin: 4px 0 0; padding: 0; display: flex; flex-wrap: wrap; gap: 4px 12px; font-size: 11px; color: var(--muted); }
    .sw { display: inline-block; width: 10px; height: 3px; border-radius: 2px; margin-right: 5px; vertical-align: middle; }
    .story { font-size: 12px; line-height: 1.5; margin: 6px 0 0; }
    .note { font-size: 12px; color: var(--muted); }
  `,
})
export class TimeTrace {
  /** The ±48 h around the moment, loaded once by the page. */
  readonly timeline = input<TimelineResponse | null>(null);
  readonly at = input.required<string>();
  readonly zone = input.required<string>();
  readonly zoneLabel = input('');
  /** The signals that feed the node most now (keys and labels). */
  readonly causes = input<{ key: string; label: string }[]>([]);

  protected readonly loading = computed(() => !this.timeline());
  protected readonly data = this.timeline;

  protected y = (v: number) => 104 - v * 100;

  protected readonly chart = computed(() => {
    const d = this.data();
    if (!d) return null;
    const at = new Date(this.at()).getTime();
    const t0 = at - HOURS * 3600_000, t1 = at + HOURS * 3600_000;
    const x = (t: number) => ((t - t0) / (t1 - t0)) * 320;
    const path = (track: SignalTrack | undefined) => {
      if (!track || !track.samples.length) return '';
      return track.samples.map((s, i) => `${i ? 'L' : 'M'}${x(new Date(s.at).getTime()).toFixed(1)},${this.y(s.intensity).toFixed(1)}`).join(' ');
    };
    const node = d.timeline.zones.find((z) => z.key === `zone:${this.zone()}`);
    const lines: Line[] = [];
    if (node) {
      const dPath = path(node);
      lines.push({ key: node.key, label: `${this.zoneLabel() || node.label} (rarità)`, color: 'var(--c-node)', d: dPath,
        area: dPath ? `${dPath} L${x(new Date(node.samples[node.samples.length - 1].at).getTime())},104 L${x(new Date(node.samples[0].at).getTime())},104 Z` : undefined });
    }
    this.causes().slice(0, 3).forEach((c, i) => {
      const track = d.timeline.signals.find((s) => s.key === c.key);
      if (track) lines.push({ key: c.key, label: c.label, color: COLORS[i], d: path(track) });
    });
    const ticks = [-48, -24, 0, 24, 48].map((h) => ({
      x: x(at + h * 3600_000),
      label: new Date(at + h * 3600_000).toLocaleDateString('it-IT', { weekday: 'short', day: 'numeric' }),
    }));
    return { lines, ticks, nowX: x(at), story: node ? story(node, at) : '' };
  });
}

/** When the node rose above an ordinary moment (50%) before now, and when it falls back after. */
function story(track: SignalTrack, at: number): string {
  const s = track.samples.map((x) => ({ t: new Date(x.at).getTime(), v: x.intensity }));
  const nowIdx = s.reduce((best, x, i) => (Math.abs(x.t - at) < Math.abs(s[best].t - at) ? i : best), 0);
  const now = s[nowIdx]?.v ?? 0;
  const fmt = (t: number) => new Date(t).toLocaleString('it-IT', { weekday: 'short', hour: '2-digit', minute: '2-digit' });
  if (now < 0.5) {
    const next = s.slice(nowIdx).find((x) => x.v >= 0.5);
    return next ? `Ora è nella sua norma; torna più sollecitato del solito verso ${fmt(next.t)}.` : 'Ora è nella sua norma, e ci resta nei prossimi due giorni.';
  }
  let start = nowIdx;
  while (start > 0 && s[start - 1].v >= 0.5) start--;
  let end = nowIdx;
  while (end < s.length - 1 && s[end + 1].v >= 0.5) end++;
  const peak = s.slice(start, end + 1).reduce((a, b) => (b.v > a.v ? b : a));
  const from = start === 0 ? 'da più di due giorni' : `da ${fmt(s[start].t)}`;
  const to = end === s.length - 1 ? 'oltre i prossimi due giorni' : `fino a ${fmt(s[end].t)} circa`;
  return `Più sollecitato del solito ${from} ${to}; il picco (${Math.round(peak.v * 100)}%) ${peak.t <= at ? 'è stato' : 'sarà'} ${fmt(peak.t)}.`;
}
