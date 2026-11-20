// The body of light: one smooth human figure made of soft primitives blended together (signed distance field).
// The same list drives the GLSL (raymarched skin) and the TypeScript SDF (particles sampled inside the body).
// Units are metres: feet at y = 0, height ≈ 1.78, the figure faces +z (its left hand is at +x).

export type Vec3 = [number, number, number];

export type Primitive =
  | { kind: 'ellipsoid'; c: Vec3; r: Vec3; k: number }
  | { kind: 'cone'; a: Vec3; b: Vec3; r1: number; r2: number; k: number };

const mirror = (p: Primitive): Primitive[] => {
  const m = (v: Vec3): Vec3 => [-v[0], v[1], v[2]];
  return p.kind === 'ellipsoid' ? [p, { ...p, c: m(p.c) }] : [p, { ...p, a: m(p.a), b: m(p.b) }];
};

/** k = smooth-union radius with what came before (bigger = softer join). */
export const BODY: Primitive[] = [
  // torso, from the pelvis up
  { kind: 'ellipsoid', c: [0, 0.93, -0.005], r: [0.158, 0.095, 0.1], k: 0 },
  ...mirror({ kind: 'ellipsoid', c: [0.068, 0.9, -0.045], r: [0.075, 0.085, 0.07], k: 0.04 }),
  { kind: 'ellipsoid', c: [0, 1.02, 0.012], r: [0.128, 0.1, 0.088], k: 0.07 },
  { kind: 'ellipsoid', c: [0, 1.14, 0], r: [0.122, 0.12, 0.084], k: 0.07 },
  { kind: 'ellipsoid', c: [0, 1.31, 0.005], r: [0.152, 0.15, 0.096], k: 0.07 },
  { kind: 'cone', a: [-0.15, 1.435, -0.012], b: [0.15, 1.435, -0.012], r1: 0.048, r2: 0.048, k: 0.06 },
  ...mirror({ kind: 'ellipsoid', c: [0.182, 1.405, -0.006], r: [0.058, 0.066, 0.058], k: 0.04 }),
  // neck and head
  { kind: 'cone', a: [0, 1.46, -0.006], b: [0, 1.56, 0], r1: 0.052, r2: 0.043, k: 0.04 },
  { kind: 'ellipsoid', c: [0, 1.638, 0.004], r: [0.082, 0.106, 0.096], k: 0.035 },
  { kind: 'ellipsoid', c: [0, 1.578, 0.026], r: [0.058, 0.055, 0.064], k: 0.035 },
  // arms, slightly open (A pose)
  ...mirror({ kind: 'cone', a: [0.192, 1.4, -0.01], b: [0.252, 1.13, -0.012], r1: 0.045, r2: 0.035, k: 0.03 }),
  ...mirror({ kind: 'cone', a: [0.252, 1.13, -0.012], b: [0.31, 0.885, 0.008], r1: 0.035, r2: 0.025, k: 0.02 }),
  ...mirror({ kind: 'ellipsoid', c: [0.328, 0.81, 0.016], r: [0.024, 0.07, 0.043], k: 0.025 }),
  // legs
  ...mirror({ kind: 'cone', a: [0.088, 0.9, 0], b: [0.103, 0.5, 0.005], r1: 0.084, r2: 0.051, k: 0.05 }),
  ...mirror({ kind: 'cone', a: [0.103, 0.5, 0.005], b: [0.108, 0.085, -0.01], r1: 0.049, r2: 0.031, k: 0.025 }),
  ...mirror({ kind: 'ellipsoid', c: [0.106, 0.355, -0.024], r: [0.048, 0.1, 0.048], k: 0.03 }),
  ...mirror({ kind: 'ellipsoid', c: [0.112, 0.034, 0.038], r: [0.041, 0.031, 0.104], k: 0.03 }),
];

/** Axis-aligned bounds of the figure (with margin), where the skin is raymarched. */
export const BOUNDS = { min: [-0.42, -0.03, -0.2] as Vec3, max: [0.42, 1.8, 0.22] as Vec3 };

const f = (n: number) => (Number.isInteger(n) ? n.toFixed(1) : String(n));
const v = (x: Vec3) => `vec3(${x.map(f).join(', ')})`;

/** GLSL source of `float sdBody(vec3 p)`, with its helpers. */
export function bodyGlsl(): string {
  const lines = BODY.map((p, i) => {
    const d =
      p.kind === 'ellipsoid'
        ? `sdEllipsoid(p - ${v(p.c)}, ${v(p.r)})`
        : `sdRoundCone(p, ${v(p.a)}, ${v(p.b)}, ${f(p.r1)}, ${f(p.r2)})`;
    return i === 0 ? `  float d = ${d};` : `  d = smin(d, ${d}, ${f(p.k)});`;
  });
  return `
float smin(float a, float b, float k) {
  if (k <= 0.0) return min(a, b);
  float h = max(k - abs(a - b), 0.0) / k;
  return min(a, b) - h * h * k * 0.25;
}
float sdEllipsoid(vec3 p, vec3 r) {
  float k0 = length(p / r);
  float k1 = length(p / (r * r));
  return k0 * (k0 - 1.0) / k1;
}
float sdRoundCone(vec3 p, vec3 a, vec3 b, float r1, float r2) {
  vec3 ba = b - a; float l2 = dot(ba, ba); float rr = r1 - r2; float a2 = l2 - rr * rr; float il2 = 1.0 / l2;
  vec3 pa = p - a; float y = dot(pa, ba); float z = y - l2;
  vec3 xv = pa * l2 - ba * y; float x2 = dot(xv, xv); float y2 = y * y * l2; float z2 = z * z * l2;
  float k = sign(rr) * rr * rr * x2;
  if (sign(z) * a2 * z2 > k) return sqrt(x2 + z2) * il2 - r2;
  if (sign(y) * a2 * y2 < k) return sqrt(x2 + y2) * il2 - r1;
  return (sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
}
float sdBody(vec3 p) {
${lines.join('\n')}
  return d;
}`;
}

// ---------- the same SDF in TypeScript (CPU: particles) ----------

const sub = (a: Vec3, b: Vec3): Vec3 => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const dot = (a: Vec3, b: Vec3) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const len = (a: Vec3) => Math.sqrt(dot(a, a));

function smin(a: number, b: number, k: number) {
  if (k <= 0) return Math.min(a, b);
  const h = Math.max(k - Math.abs(a - b), 0) / k;
  return Math.min(a, b) - h * h * k * 0.25;
}

function sdEllipsoid(p: Vec3, r: Vec3) {
  const k0 = len([p[0] / r[0], p[1] / r[1], p[2] / r[2]]);
  const k1 = len([p[0] / (r[0] * r[0]), p[1] / (r[1] * r[1]), p[2] / (r[2] * r[2])]);
  return (k0 * (k0 - 1)) / k1;
}

function sdRoundCone(p: Vec3, a: Vec3, b: Vec3, r1: number, r2: number) {
  const ba = sub(b, a);
  const l2 = dot(ba, ba);
  const rr = r1 - r2;
  const a2 = l2 - rr * rr;
  const il2 = 1 / l2;
  const pa = sub(p, a);
  const y = dot(pa, ba);
  const z = y - l2;
  const xv: Vec3 = [pa[0] * l2 - ba[0] * y, pa[1] * l2 - ba[1] * y, pa[2] * l2 - ba[2] * y];
  const x2 = dot(xv, xv);
  const y2 = y * y * l2;
  const z2 = z * z * l2;
  const k = Math.sign(rr) * rr * rr * x2;
  if (Math.sign(z) * a2 * z2 > k) return Math.sqrt(x2 + z2) * il2 - r2;
  if (Math.sign(y) * a2 * y2 < k) return Math.sqrt(x2 + y2) * il2 - r1;
  return (Math.sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
}

export function sdBody(p: Vec3): number {
  let d = Infinity;
  BODY.forEach((prim, i) => {
    const di = prim.kind === 'ellipsoid' ? sdEllipsoid(sub(p, prim.c), prim.r) : sdRoundCone(p, prim.a, prim.b, prim.r1, prim.r2);
    d = i === 0 ? di : smin(d, di, prim.k);
  });
  return d;
}

/** Deterministic points inside the body (seeded), for the particles of light. */
export function samplePointsInside(count: number, seed = 7): Vec3[] {
  let s = seed;
  const rnd = () => ((s = (s * 16807) % 2147483647) - 1) / 2147483646;
  const out: Vec3[] = [];
  const { min, max } = BOUNDS;
  for (let guard = 0; out.length < count && guard < count * 60; guard++) {
    const p: Vec3 = [min[0] + rnd() * (max[0] - min[0]), min[1] + rnd() * (max[1] - min[1]), min[2] + rnd() * (max[2] - min[2])];
    if (sdBody(p) < -0.004) out.push(p);
  }
  return out;
}
