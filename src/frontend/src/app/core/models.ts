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
  schoolId: string;
  ruleId: string;
  signalKey: string;
  signalLabel: string;
  quality: string;
  amount: number;
  text: string;
}

export type BodyLayer =
  | 'soma' | 'systems' | 'temperament' | 'emotions' | 'mind'
  | 'zangfu' | 'spirits' | 'substances' | 'chakras' | 'nadis'
  | 'domains' | 'doshas' | 'sephiroth' | 'members';

export interface ZoneActivation {
  zone: string;
  label: string;
  layer: BodyLayer;
  systemic: boolean;
  score: number;
  raw: number;
  dominantQuality: string | null;
  /** Raw sum of contributions per quality. */
  byQuality: Record<string, number>;
  /** Saturated 0..1 per quality (same formula as score, schools combined). */
  qualityScores: Record<string, number>;
  natalBaseline: number;
  contributions: ZoneContribution[];
  /** school id → that school's score (0..1). */
  bySchool: Record<string, number>;
}

export interface Reading {
  at: string;
  schoolIds: string[];
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
  schools: string[];
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

// ---------- catalog: influences (signal sources), schools (rule sets), body zones ----------

export interface InfluenceKind {
  kind: string;
  description: string;
}

export interface InfluenceInfo {
  id: string;
  name: string;
  description: string;
  icon: string;
  providerKind: 'library' | 'api' | 'computed';
  providerName: string;
  url: string;
  license: string;
  reliability: 'primary' | 'derived';
  kinds: InfluenceKind[];
}

export interface InfluenceStatus {
  ok: boolean;
  samples: number;
  error: string | null;
  checkedAt: string;
}

export interface CatalogZoneRef {
  id: string;
  label: string;
}

export interface CatalogRule {
  id: string;
  kind: string;
  kindLabel: string;
  tags: Record<string, string[]> | null;
  /** Italian rendering of tags: "elemento: Legno". */
  conditions: string[];
  minIntensity: number;
  zones: CatalogZoneRef[];
  zonesFrom: string[];
  zonesFromText: string | null;
  weight: number;
  quality: string | null;
  qualityLabel: string | null;
  text: string;
  /** Short, cause first: "Pianeta retrogrado → i suoi organi". */
  name: string;
  /** What the rule says and why, in its tradition. */
  about: string;
  /** Historical source (author, work, chapter) or "convenzione dello strumento". */
  source: string;
}

export interface CatalogLineage {
  id: string;
  name: string;
  description: string;
}

export interface CatalogSchool {
  id: string;
  version: string;
  name: string;
  description: string;
  tradition: string;
  icon: string;
  references: string[];
  requires: string[];
  baseline: string;
  baselineText: string;
  /** Fallback scale, used only while the school has no up-to-date quantile table. */
  saturation: number;
  natalAmplification: number;
  /** Quantile table written by the calibration ritual: scores are rarities only when 'Calibrated'. */
  calibration: 'Calibrated' | 'Stale' | 'Missing';
  /** Family of traditions; schools of the same lineage, or borrowing from it (parents), are not independent. */
  lineage: CatalogLineage;
  parents: CatalogLineage[];
  /** Reference tables of the tradition (five elements, chakras…). */
  tables: { title: string; columns: string[]; rows: string[][] }[];
  rules: CatalogRule[];
  correspondences: { symbol: string; label: string; zones: CatalogZoneRef[] }[];
}

/** A node of the body model (zones are its soma layer). */
export interface CatalogZone {
  id: string;
  label: string;
  layer: BodyLayer;
  layerLabel: string;
  systemic: boolean;
  /** Soma node it lives in (3D placement); null = the whole body. */
  seat: string | null;
  description: string;
}

export interface Catalog {
  influences: { info: InfluenceInfo; status: InfluenceStatus | null }[];
  schools: CatalogSchool[];
  zones: CatalogZone[];
}

export const METRICS = ['energy', 'mood', 'sleep', 'focus', 'stress', 'gut', 'head', 'body'] as const;
export type Metric = (typeof METRICS)[number];

/** illnessOnset: local time ("HH:mm") a malaise began that day — the decumbiture, followed for 30 days. */
export type JournalDay = { date: string; notes: string | null; illnessOnset?: string | null } & Record<Metric, number | null>;

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
