import { CatalogStore } from './catalog-store';
import { Reading, Signal, ZoneActivation, ZoneContribution } from './models';

/** The four kinds of things in a chain, always in this order: cause → effect. */
export type FlowKind = 'influence' | 'signal' | 'rule' | 'node';
export const FLOW_ORDER: FlowKind[] = ['influence', 'signal', 'rule', 'node'];

/** A typed id used in links and in the URL: "node:heart", "rule:melothesia/retrograde", "signal:kp", "influence:gfz". */
export type FlowRef = `${FlowKind}:${string}`;

export interface FlowItem {
  ref: FlowRef;
  kind: FlowKind;
  label: string;
  /** Second line: the signal's detail, the rule's school and source, the node's layer. */
  sub: string;
  icon: string;
  /** 0..1: signal intensity, node rarity; null for influences and rules. */
  level: number | null;
  /** Colour of the quality (nodes, rule → node links). */
  quality: string | null;
  /** Sum of the raw amounts flowing through it in this view. */
  value: number;
}

/** One contribution walked end to end. */
export interface FlowPath {
  influence: FlowRef;
  signal: FlowRef;
  rule: FlowRef;
  node: FlowRef;
  amount: number;
  quality: string;
  c: ZoneContribution;
}

export interface FlowGraph {
  items: Map<FlowRef, FlowItem>;
  paths: FlowPath[];
}

export const refOf = (kind: FlowKind, id: string): FlowRef => `${kind}:${id}` as FlowRef;
export const parseRef = (ref: string | null | undefined): { kind: FlowKind; id: string } | null => {
  if (!ref) return null;
  const i = ref.indexOf(':');
  const kind = ref.slice(0, i) as FlowKind;
  return FLOW_ORDER.includes(kind) ? { kind, id: ref.slice(i + 1) } : null;
};

/** Second-pass contributions come from a node of the same school: they show as an influence "the school itself". */
const SECOND_PASS = 'second-pass';

/**
 * Every contribution of a reading as a path influence → signal → rule → node, optionally around one focus, and
 * pruned to the strongest so a picture stays readable: by default the strongest nodes and, for each, the paths that
 * make most of it.
 */
export function buildFlow(
  reading: Reading,
  catalog: CatalogStore,
  focus: FlowRef | null,
  opts: { nodes?: number; share?: number; maxPaths?: number } = {},
): FlowGraph {
  const { nodes: nodeCount = 8, share = 0.85, maxPaths = 70 } = opts;
  const signals = new Map(reading.signals.map((s) => [s.key, s]));
  const f = parseRef(focus);

  const pathsOf = (z: ZoneActivation): FlowPath[] =>
    z.contributions.map((c) => {
      const s = signals.get(c.signalKey);
      const influence = s ? s.source : `${SECOND_PASS}/${c.schoolId}`;
      return {
        influence: refOf('influence', influence),
        signal: refOf('signal', c.signalKey),
        rule: refOf('rule', `${c.schoolId}/${c.ruleId}`),
        node: refOf('node', z.zone),
        amount: c.amount,
        quality: c.quality,
        c,
      };
    });

  let paths: FlowPath[];
  if (f) {
    paths = reading.zones.flatMap(pathsOf).filter((p) => p[f.kind] === focus);
    if (f.kind !== 'node') paths = keepStrongest(paths, maxPaths);
  } else {
    const top = [...reading.zones].filter((z) => z.score > 0.02).sort((a, b) => b.score - a.score).slice(0, nodeCount);
    paths = top.flatMap((z) => {
      const all = pathsOf(z).sort((a, b) => b.amount - a.amount);
      const total = all.reduce((s, p) => s + p.amount, 0);
      const kept: FlowPath[] = [];
      let acc = 0;
      for (const p of all) {
        if (acc >= total * share && kept.length >= 2) break;
        kept.push(p);
        acc += p.amount;
      }
      return kept;
    });
    paths = keepStrongest(paths, maxPaths);
  }

  const items = new Map<FlowRef, FlowItem>();
  const zones = new Map(reading.zones.map((z) => [z.zone, z]));
  const add = (ref: FlowRef, amount: number, make: () => Omit<FlowItem, 'value'>) => {
    const it = items.get(ref) ?? { ...make(), value: 0 };
    it.value += amount;
    items.set(ref, it);
  };
  for (const p of paths) {
    add(p.influence, p.amount, () => influenceItem(p.influence, catalog));
    add(p.signal, p.amount, () => signalItem(p.signal, signals.get(p.c.signalKey), p.c, catalog));
    add(p.rule, p.amount, () => ruleItem(p.rule, p.c, catalog));
    add(p.node, p.amount, () => nodeItem(p.node, zones.get(parseRef(p.node)!.id)!, catalog));
  }
  return { items, paths };
}

function keepStrongest(paths: FlowPath[], max: number) {
  return paths.length <= max ? paths : [...paths].sort((a, b) => b.amount - a.amount).slice(0, max);
}

function influenceItem(ref: FlowRef, catalog: CatalogStore): Omit<FlowItem, 'value'> {
  const id = parseRef(ref)!.id;
  if (id.startsWith(SECOND_PASS)) {
    const school = catalog.school(id.split('/')[1]);
    return { ref, kind: 'influence', label: school?.name ?? id, sub: 'la scuola stessa: un suo nodo attivato', icon: school?.icon ?? 'consequences', level: null, quality: null };
  }
  const info = catalog.influence(id);
  return { ref, kind: 'influence', label: info?.name ?? id, sub: info?.providerName ?? '', icon: info?.icon ?? id, level: null, quality: null };
}

function signalItem(ref: FlowRef, s: Signal | undefined, c: ZoneContribution, catalog: CatalogStore): Omit<FlowItem, 'value'> {
  const icon = s ? catalog.influence(s.source)?.icon ?? s.source : 'consequences';
  return { ref, kind: 'signal', label: s?.label ?? c.signalLabel, sub: s?.detail ?? 'nodo attivato (secondo passaggio)', icon, level: s?.intensity ?? null, quality: s?.tags['quality'] ?? null };
}

function ruleItem(ref: FlowRef, c: ZoneContribution, catalog: CatalogStore): Omit<FlowItem, 'value'> {
  const school = catalog.school(c.schoolId);
  const rule = school?.rules.find((r) => r.id === c.ruleId);
  return { ref, kind: 'rule', label: rule?.name || c.text, sub: `${school?.name ?? c.schoolId} · ${rule?.source ?? ''}`, icon: school?.icon ?? c.schoolId, level: null, quality: rule?.quality ?? null };
}

function nodeItem(ref: FlowRef, z: ZoneActivation, catalog: CatalogStore): Omit<FlowItem, 'value'> {
  const layer = catalog.catalog()?.zones.find((n) => n.id === z.zone)?.layerLabel ?? z.layer;
  return { ref, kind: 'node', label: z.label, sub: layer, icon: 'consequences', level: z.score, quality: z.dominantQuality };
}

// ---------- common theories ----------

export interface Theory {
  /** What converges: a signal now, or a symbol (planet, sign) in the catalog. */
  cause: string;
  causeRef: FlowRef | null;
  node: string;
  nodeRef: FlowRef;
  schools: string[];
  families: string[];
  amount: number;
}

/** Family each school votes with: own lineage, or a directly borrowed one that is present (same rule as the API). */
export function familiesOf(ids: string[], catalog: CatalogStore): Map<string, string> {
  const schools = ids.map((id) => catalog.school(id)).filter((s) => !!s);
  const present = new Set(schools.map((s) => s!.lineage.id));
  return new Map(schools.map((s) => [s!.id, s!.parents.find((p) => present.has(p.id))?.id ?? s!.lineage.id]));
}

/** Now: the same signal landing on the same node through schools of two or more families. */
export function theoriesNow(reading: Reading, catalog: CatalogStore): Theory[] {
  const families = familiesOf(reading.schoolIds, catalog);
  const signals = new Map(reading.signals.map((s) => [s.key, s]));
  const map = new Map<string, Theory & { fam: Set<string>; sch: Set<string> }>();
  for (const z of reading.zones)
    for (const c of z.contributions) {
      if (!signals.has(c.signalKey)) continue; // second pass: not a shared cause
      const key = `${c.signalKey}|${z.zone}`;
      const t = map.get(key) ?? {
        cause: signals.get(c.signalKey)!.label, causeRef: refOf('signal', c.signalKey), node: z.label, nodeRef: refOf('node', z.zone),
        schools: [], families: [], amount: 0, fam: new Set<string>(), sch: new Set<string>(),
      };
      t.sch.add(c.schoolId);
      t.fam.add(families.get(c.schoolId) ?? c.schoolId);
      t.amount += c.amount;
      map.set(key, t);
    }
  return [...map.values()]
    .filter((t) => t.fam.size >= 2)
    .map((t) => ({ ...t, schools: [...t.sch].map((s) => catalog.school(s)?.name ?? s), families: [...t.fam].map((f) => lineageName(f, catalog)) }))
    .sort((a, b) => b.families.length - a.families.length || b.amount - a.amount);
}

const PLANET_TAGS = new Set(['body', 'transitBody', 'ruler', 'dayRuler', 'lord', 'subLord', 'bodyA', 'bodyB', 'face']);

/** In the catalog: the same planet (or sign) mapped to the same node by schools of two or more families. */
export function theoriesInCatalog(catalog: CatalogStore): Theory[] {
  const all = catalog.schools();
  const families = familiesOf(all.map((s) => s.id), catalog);
  const map = new Map<string, { symbol: string; zone: string; sch: Set<string>; fam: Set<string> }>();
  const zoneLabel = new Map((catalog.catalog()?.zones ?? []).map((z) => [z.id, z.label]));
  for (const s of all) {
    const reads = new Set(s.rules.flatMap((r) => r.zonesFrom.filter((t) => PLANET_TAGS.has(t))));
    if (!reads.size) continue;
    for (const c of s.correspondences) {
      for (const z of c.zones) {
        const key = `${c.symbol}|${z.id}`;
        const t = map.get(key) ?? { symbol: c.label, zone: z.id, sch: new Set<string>(), fam: new Set<string>() };
        t.sch.add(s.id);
        t.fam.add(families.get(s.id) ?? s.lineage.id);
        map.set(key, t);
      }
    }
  }
  return [...map.values()]
    .filter((t) => t.fam.size >= 2)
    .map((t) => ({
      cause: t.symbol, causeRef: null, node: zoneLabel.get(t.zone) ?? t.zone, nodeRef: refOf('node', t.zone),
      schools: [...t.sch].map((id) => catalog.school(id)?.name ?? id), families: [...t.fam].map((f) => lineageName(f, catalog)), amount: 0,
    }))
    .sort((a, b) => b.families.length - a.families.length || a.cause.localeCompare(b.cause));
}

function lineageName(id: string, catalog: CatalogStore) {
  for (const s of catalog.schools()) {
    if (s.lineage.id === id) return s.lineage.name;
    const p = s.parents.find((x) => x.id === id);
    if (p) return p.name;
  }
  return id;
}
