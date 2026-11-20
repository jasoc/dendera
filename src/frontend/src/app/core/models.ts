// Mirrors the API contracts (src/backend/Dendera.Core + Dendera.Api/Endpoints/Dtos.cs).

export interface PlacedPoint {
  id: string;
  longitude: number;
  latitude: number;
  speed: number;
  sign: string;
  degreeInSign: number;
  house: number;
  retrograde: boolean;
  label: string;
  position: string;
}

export interface NatalChart {
  birth: { birthUtc: string; latitude: number; longitude: number };
  houseSystem: string;
  cusps: number[];
  ascendant: number;
  midheaven: number;
  points: PlacedPoint[];
}

export interface Signal {
  key: string;
  kind: string;
  source: string;
  at: string;
  intensity: number;
  value: number | null;
  unit: string | null;
  tags: Record<string, string>;
  label: string;
  detail: string;
}

export interface ZoneContribution {
  ruleId: string;
  signalKey: string;
  signalLabel: string;
  quality: string;
  amount: number;
  text: string;
}

export interface ZoneActivation {
  zone: string;
  label: string;
  systemic: boolean;
  score: number;
  raw: number;
  dominantQuality: string | null;
  byQuality: Record<string, number>;
  natalBaseline: number;
  contributions: ZoneContribution[];
}

export interface Reading {
  at: string;
  ruleSetId: string;
  sky: PlacedPoint[];
  natal: NatalChart | null;
  signals: Signal[];
  zones: ZoneActivation[];
}

export interface SourceStatus {
  source: string;
  ok: boolean;
  samples: number;
  error: string | null;
}

export interface ReadingResponse {
  reading: Reading;
  sources: SourceStatus[];
}

export interface SignalSample {
  at: string;
  intensity: number;
  value: number | null;
  detail: string;
}

export interface SignalTrack {
  key: string;
  kind: string;
  source: string;
  label: string;
  tags: Record<string, string>;
  peak: number;
  samples: SignalSample[];
}

export interface TimelineResponse {
  timeline: { from: string; to: string; signals: SignalTrack[]; zones: SignalTrack[] };
  sources: SourceStatus[];
  stepHours: number;
}

export interface Profile {
  id: number;
  name: string;
  birthLocal: string;
  birthUtcOffsetMinutes: number;
  birthUtc: string;
  birthTimeKnown: boolean;
  birthPlace: string;
  birthLatitude: number;
  birthLongitude: number;
  birthTimeZone: string | null;
  currentPlace: string;
  currentLatitude: number;
  currentLongitude: number;
  currentTimeZone: string | null;
  houseSystem: string;
  ruleSetId: string;
}

export type ProfileInput = Omit<Profile, 'id' | 'birthUtc'>;

export interface Place {
  name: string;
  region: string | null;
  country: string | null;
  latitude: number;
  longitude: number;
  timeZone: string | null;
}

export interface RuleSetInfo {
  id: string;
  version: string;
  name: string;
  description: string;
  rules: number;
}

export const METRICS = ['energy', 'mood', 'sleep', 'focus', 'stress', 'gut', 'head', 'body'] as const;
export type Metric = (typeof METRICS)[number];

export type JournalDay = { date: string; notes: string | null } & Record<Metric, number | null>;

export interface CorrelationFinding {
  metric: string;
  metricLabel: string;
  signalKey: string;
  signalKind: string;
  signalLabel: string;
  days: number;
  activeDays: number;
  pearson: number;
  meanWhenActive: number;
  meanWhenQuiet: number;
  pValue: number;
  qValue: number;
  evidence: 'strong' | 'hint' | 'none';
}

export interface CorrelationResponse {
  from: string | null;
  to: string | null;
  journalDays: number;
  tracksTested: number;
  findings: CorrelationFinding[];
  sources: SourceStatus[];
  message: string | null;
}
