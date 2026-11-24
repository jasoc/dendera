import { SignalTrack } from './models';

/** A stretch of time in which a track stays above a threshold, around a moment. */
export interface Episode {
  /** Start (ms), or null when it was already above the threshold at the start of the window. */
  from: number | null;
  /** End (ms), or null when it is still above at the end of the window. */
  to: number | null;
  peak: { t: number; v: number };
  /** Above the threshold at the moment itself. */
  now: boolean;
  /** When not above now: the next rise inside the window, if any. */
  next: number | null;
}

/**
 * The episode of a track around `at`: the contiguous run of samples ≥ threshold that contains the moment, or, when the
 * moment is below, the next rise. Consequences use 0.5 (above an ordinary moment), signals a small threshold (present).
 */
export function episodeAt(track: SignalTrack | undefined, at: number, threshold: number): Episode | null {
  if (!track?.samples.length) return null;
  const s = track.samples.map((x) => ({ t: new Date(x.at).getTime(), v: x.intensity }));
  let i = 0;
  for (let k = 0; k < s.length; k++) if (Math.abs(s[k].t - at) < Math.abs(s[i].t - at)) i = k;
  if (s[i].v < threshold) {
    const next = s.slice(i).find((x) => x.v >= threshold);
    return { from: null, to: null, peak: s[i], now: false, next: next?.t ?? null };
  }
  let a = i, b = i;
  while (a > 0 && s[a - 1].v >= threshold) a--;
  while (b < s.length - 1 && s[b + 1].v >= threshold) b++;
  const peak = s.slice(a, b + 1).reduce((p, x) => (x.v > p.v ? x : p));
  return { from: a === 0 ? null : s[a].t, to: b === s.length - 1 ? null : s[b].t, peak, now: true, next: null };
}

/** "oggi 14:00", "domani 16:00", "ieri 09:00", "mer 26, 03:00": relative to the moment being read. */
export function when(t: number, at: number): string {
  const day = (x: number) => { const d = new Date(x); d.setHours(0, 0, 0, 0); return d.getTime(); };
  const diff = Math.round((day(t) - day(at)) / 86400_000);
  const time = new Date(t).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
  const word = diff === 0 ? 'oggi' : diff === 1 ? 'domani' : diff === -1 ? 'ieri'
    : new Date(t).toLocaleDateString('it-IT', { weekday: 'short', day: 'numeric' });
  return `${word} ${time}`;
}

/** The window in words: "da oggi 14:00 a domani 16:00", "da più di due giorni", "oltre i prossimi due giorni". */
export function windowText(e: Episode | null, at: number): string {
  if (!e) return '';
  if (!e.now) return e.next ? `riparte ${when(e.next, at)}` : 'non attivo nelle prossime 48 ore';
  if (e.from === null && e.to === null) return 'da più di due giorni, e per almeno altri due (una causa lenta: stagione, transito, periodo)';
  if (e.from === null) return `da più di due giorni, fino a ${when(e.to!, at)}`;
  if (e.to === null) return `da ${when(e.from, at)}, e per almeno altri due giorni`;
  return `da ${when(e.from, at)} a ${when(e.to, at)}`;
}
