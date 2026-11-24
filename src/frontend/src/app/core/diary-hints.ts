import { Injectable, inject, signal } from '@angular/core';
import { Api } from './api';
import { CorrelationFinding, Reading } from './models';

/** A strong correspondence from the journal whose signal (or zone) is active right now. */
export interface DiaryHint {
  finding: CorrelationFinding;
  /** The current sample of the signal, or of the zone ("Polmoni · 78%"). */
  now: string;
  /** Plain words: "possibile mal di testa". */
  outcome: string;
  /** "Testa 2,1 nei giorni con il segnale, 3,4 senza". */
  evidence: string;
  /** Signal key or zone id, to open it in the chain panel. */
  target: { kind: 'signal' | 'zone'; id: string };
}

/** The journal scale runs 1 (bad) → 5 (good), except stress (1 calm → 5 under pressure). */
const WORSE_WHEN_HIGHER = new Set(['stress']);

/** What a worse or better value of each journal metric means, in plain words. */
const OUTCOME: Record<string, { worse: string; better: string }> = {
  energy: { worse: 'energia più bassa', better: 'più energia' },
  mood: { worse: 'umore più cupo', better: 'umore più luminoso' },
  sleep: { worse: 'sonno più difficile', better: 'sonno più profondo' },
  focus: { worse: 'meno lucidità', better: 'mente più limpida' },
  stress: { worse: 'più stress', better: 'più calma' },
  gut: { worse: 'intestino in subbuglio', better: 'intestino tranquillo' },
  head: { worse: 'possibile mal di testa o testa pesante', better: 'testa leggera' },
  body: { worse: 'possibili dolori o tensioni', better: 'corpo più sciolto' },
};

/** Same threshold the correlation engine uses to call a day "active". */
const ACTIVE = 0.3;

/**
 * Loads the person's correlations once per profile and consensus, and turns the strong ones whose signal is active in
 * a reading into hints. Descriptive, from the person's own journal: "when this was active, you wrote…".
 */
@Injectable({ providedIn: 'root' })
export class DiaryHints {
  private readonly api = inject(Api);
  readonly findings = signal<CorrelationFinding[]>([]);
  private loadedFor = '';

  async load(profileId: number, consensus: string): Promise<void> {
    const key = `${profileId}:${consensus}`;
    if (key === this.loadedFor) return;
    this.loadedFor = key;
    this.findings.set([]);
    try {
      const r = await this.api.correlations(profileId, consensus);
      this.findings.set(r.findings.filter((f) => f.evidence === 'strong'));
    } catch (e) {
      console.error('Corrispondenze non disponibili', e);
    }
  }

  /** Strong findings active at this reading, strongest first, one per signal and metric. */
  forReading(reading: Reading): DiaryHint[] {
    const signals = new Map(reading.signals.map((s) => [s.key, s]));
    const zones = new Map(reading.zones.map((z) => [z.zone, z]));
    const hints: DiaryHint[] = [];
    for (const f of this.findings()) {
      let now: string | null = null;
      let target: DiaryHint['target'] | null = null;
      if (f.signalKey.startsWith('zone:')) {
        const parts = f.signalKey.split(':');
        const zone = zones.get(parts[parts.length - 1]);
        const score = !zone ? 0 : parts.length === 3 ? zone.bySchool[parts[1]] ?? 0 : zone.score;
        if (zone && score >= ACTIVE) {
          now = `${zone.label} · ${Math.round(score * 100)}%`;
          target = { kind: 'zone', id: zone.zone };
        }
      } else {
        const s = signals.get(f.signalKey);
        if (s && s.intensity >= ACTIVE) {
          now = `${s.label}: ${s.detail}`;
          target = { kind: 'signal', id: s.key };
        }
      }
      if (!now || !target) continue;
      const higherWhenActive = f.meanWhenActive > f.meanWhenQuiet;
      const worse = WORSE_WHEN_HIGHER.has(f.metric) ? higherWhenActive : !higherWhenActive;
      const words = OUTCOME[f.metric];
      hints.push({
        finding: f,
        now,
        outcome: words ? (worse ? words.worse : words.better) : f.metricLabel,
        evidence: `${f.metricLabel} ${num(f.meanWhenActive)} nei giorni con questo, ${num(f.meanWhenQuiet)} senza (r ${num(f.pearson, 2)})`,
        target,
      });
    }
    return hints.sort((a, b) => Math.abs(b.finding.pearson) - Math.abs(a.finding.pearson)).slice(0, 8);
  }
}

const num = (v: number, digits = 1) => v.toLocaleString('it-IT', { minimumFractionDigits: digits, maximumFractionDigits: digits });
