// The body of light: one smooth human figure made of soft primitives blended together (signed distance field).
// The same list drives the GLSL (raymarched skin) and the TypeScript SDF (particles sampled inside the body).
// Units are metres: feet at y = 0, height ≈ 1.78, the figure faces +z (its left hand is at +x).

export type Vec3 = [number, number, number];

/** Parts that move together in the idle animation (see `animate`). */
export type Group = 'core' | 'chest' | 'head' | 'armL' | 'armR';

export type Primitive =
  | { kind: 'ellipsoid'; c: Vec3; r: Vec3; k: number; g?: Group }
  | { kind: 'cone'; a: Vec3; b: Vec3; r1: number; r2: number; k: number; g?: Group };

const mirror = (p: Primitive): Primitive[] => {
  const m = (v: Vec3): Vec3 => [-v[0], v[1], v[2]];
  const g = p.g === 'armL' ? 'armR' : p.g;
  return p.kind === 'ellipsoid' ? [p, { ...p, c: m(p.c), g }] : [p, { ...p, a: m(p.a), b: m(p.b), g }];
};

/**
 * The figure at rest: symmetric, arms open, as the anchors and meridian paths were drawn. A classical, moderately
 * muscular build (pectorals, deltoids, arms, abdomen, glutes, quadriceps, calves). `pose()` then gives it the
 * contrapposto of a Renaissance statue. k = smooth-union radius with what came before (bigger = softer join).
 */
const REST: Primitive[] = [
  // pelvis and glutes
  { kind: 'ellipsoid', c: [0, 0.935, -0.008], r: [0.148, 0.088, 0.094], k: 0 },
  ...mirror({ kind: 'ellipsoid', c: [0.068, 0.895, -0.052], r: [0.076, 0.084, 0.066], k: 0.04 }),
  // waist, abdomen, obliques: narrow, so the torso reads as a V
  { kind: 'ellipsoid', c: [0, 1.04, 0.006], r: [0.13, 0.09, 0.082], k: 0.05 },
  { kind: 'ellipsoid', c: [0, 1.135, 0.0], r: [0.126, 0.078, 0.078], k: 0.045 },
  { kind: 'ellipsoid', c: [0, 1.1, 0.05], r: [0.066, 0.115, 0.03], k: 0.04 },
  ...mirror({ kind: 'ellipsoid', c: [0.094, 1.05, 0.016], r: [0.042, 0.075, 0.056], k: 0.04 }),
  // ribcage and chest (they breathe), pectorals, lats
  { kind: 'ellipsoid', c: [0, 1.215, 0.0], r: [0.128, 0.09, 0.084], k: 0.045, g: 'chest' },
  { kind: 'ellipsoid', c: [0, 1.33, 0.0], r: [0.162, 0.115, 0.092], k: 0.045, g: 'chest' },
  ...mirror({ kind: 'ellipsoid', c: [0.066, 1.335, 0.058], r: [0.074, 0.05, 0.036], k: 0.035, g: 'chest' }),
  ...mirror({ kind: 'ellipsoid', c: [0.112, 1.26, -0.036], r: [0.06, 0.11, 0.058], k: 0.045 }),
  // clavicles and trapezius
  { kind: 'cone', a: [-0.15, 1.44, -0.006], b: [0.15, 1.44, -0.006], r1: 0.04, r2: 0.04, k: 0.04 },
  ...mirror({ kind: 'cone', a: [0.04, 1.5, -0.032], b: [0.155, 1.445, -0.022], r1: 0.036, r2: 0.03, k: 0.04 }),
  // deltoids
  ...mirror({ kind: 'ellipsoid', c: [0.188, 1.405, -0.004], r: [0.058, 0.068, 0.058], k: 0.035 }),
  // neck and head (they turn together)
  { kind: 'cone', a: [0, 1.46, -0.01], b: [0, 1.56, 0.0], r1: 0.05, r2: 0.04, k: 0.035, g: 'head' },
  { kind: 'ellipsoid', c: [0, 1.648, 0.0], r: [0.078, 0.1, 0.092], k: 0.03, g: 'head' },
  { kind: 'ellipsoid', c: [0, 1.58, 0.03], r: [0.054, 0.05, 0.06], k: 0.03, g: 'head' },
  // arms: upper arm, biceps and triceps, forearm, hand
  ...mirror({ kind: 'cone', a: [0.194, 1.4, -0.01], b: [0.252, 1.13, -0.012], r1: 0.04, r2: 0.03, k: 0.025, g: 'armL' }),
  ...mirror({ kind: 'ellipsoid', c: [0.224, 1.265, 0.012], r: [0.032, 0.078, 0.032], k: 0.022, g: 'armL' }),
  ...mirror({ kind: 'ellipsoid', c: [0.228, 1.28, -0.028], r: [0.03, 0.082, 0.03], k: 0.022, g: 'armL' }),
  ...mirror({ kind: 'cone', a: [0.252, 1.13, -0.012], b: [0.31, 0.885, 0.008], r1: 0.031, r2: 0.021, k: 0.018, g: 'armL' }),
  ...mirror({ kind: 'ellipsoid', c: [0.266, 1.065, -0.004], r: [0.03, 0.068, 0.03], k: 0.02, g: 'armL' }),
  ...mirror({ kind: 'ellipsoid', c: [0.326, 0.815, 0.014], r: [0.021, 0.066, 0.04], k: 0.02, g: 'armL' }),
  // legs: thigh and quadriceps, knee, shin and calf, ankle, foot
  ...mirror({ kind: 'cone', a: [0.086, 0.9, 0], b: [0.102, 0.5, 0.006], r1: 0.08, r2: 0.046, k: 0.045 }),
  ...mirror({ kind: 'ellipsoid', c: [0.098, 0.73, 0.03], r: [0.06, 0.15, 0.052], k: 0.04 }),
  ...mirror({ kind: 'cone', a: [0.102, 0.5, 0.006], b: [0.108, 0.085, -0.01], r1: 0.045, r2: 0.026, k: 0.022 }),
  ...mirror({ kind: 'ellipsoid', c: [0.106, 0.365, -0.03], r: [0.047, 0.1, 0.047], k: 0.025 }),
  ...mirror({ kind: 'ellipsoid', c: [0.112, 0.034, 0.038], r: [0.038, 0.029, 0.1], k: 0.025 }),
];

// ---------- the pose: contrapposto ----------
// Weight on the right leg (−x): the hips tilt down to the figure's left, the shoulders the other way, the relaxed left
// knee comes forward, the arms fall closer to the body, the left forearm turns slightly forward, the head turns to
// its left. Applied to every point of the model (primitives, anchors, meridians, nadis), so lights stay in place.

const deg = Math.PI / 180;
const smooth = (e0: number, e1: number, x: number) => {
  const t = Math.min(1, Math.max(0, (x - e0) / (e1 - e0)));
  return t * t * (3 - 2 * t);
};
/** Rotation of p around a pivot, in the plane of two axes (0 x, 1 y, 2 z), blended by w. */
function rot(p: Vec3, pivot: Vec3, i: number, j: number, angle: number, w: number): Vec3 {
  if (w <= 0) return p;
  const a = angle * w;
  const di = p[i] - pivot[i];
  const dj = p[j] - pivot[j];
  const out: Vec3 = [p[0], p[1], p[2]];
  out[i] = pivot[i] + di * Math.cos(a) - dj * Math.sin(a);
  out[j] = pivot[j] + di * Math.sin(a) + dj * Math.cos(a);
  return out;
}

const HIP: Vec3 = [0.088, 0.9, 0];
/** The left knee after the thigh came forward: the shin turns about it. */
const KNEE: Vec3 = rot([0.103, 0.5, 0.005], HIP, 2, 1, 6 * (Math.PI / 180), 1);

export function pose(p0: Vec3): Vec3 {
  const [x, y] = p0;
  let p: Vec3 = [...p0];
  // arms closer to the body (about each shoulder)
  const arm = smooth(0.15, 0.19, Math.abs(x)) * (1 - smooth(1.42, 1.46, y));
  p = rot(p, [Math.sign(x) * 0.19, 1.405, 0], 0, 1, -Math.sign(x) * 6 * deg, arm);
  // left forearm a little forward (about the elbow)
  const forearm = smooth(0.18, 0.2, x) * (1 - smooth(1.12, 1.16, y));
  p = rot(p, [0.25, 1.13, -0.012], 2, 1, 16 * deg, forearm);
  // relaxed left leg: thigh forward from the hip, shin back from the knee
  const leftLeg = smooth(0.02, 0.05, x) * (1 - smooth(0.86, 0.94, y));
  p = rot(p, HIP, 2, 1, 6 * deg, leftLeg);
  const shin = smooth(0.02, 0.05, x) * (1 - smooth(0.46, 0.54, y));
  p = rot(p, KNEE, 2, 1, -14 * deg, shin);
  // hips tilt down to the figure's left (the pelvis only: the feet stay planted), shoulders and arms up on that side
  // (not the neck and head, which stay above the standing leg)
  const pelvis = smooth(0.74, 0.88, y) * (1 - smooth(0.98, 1.08, y));
  p = rot(p, [0, 0.93, 0], 0, 1, -5 * deg, pelvis);
  const shoulders = Math.max(smooth(1.12, 1.26, y), smooth(0.15, 0.19, Math.abs(x))) * (1 - smooth(1.47, 1.55, y));
  p = rot(p, [0, 1.15, 0], 0, 1, 3.5 * deg, shoulders);
  // the head turns to its left
  p = rot(p, [0, 1.6, 0], 2, 0, 14 * deg, smooth(1.5, 1.56, y));
  return p;
}

const posePrimitive = (q: Primitive): Primitive =>
  q.kind === 'ellipsoid' ? { ...q, c: pose(q.c) } : { ...q, a: pose(q.a), b: pose(q.b) };

/** The figure as drawn: the rest model in contrapposto. */
export const BODY: Primitive[] = REST.map(posePrimitive);

/** Axis-aligned bounds of the figure (with margin), where the skin is raymarched. */
export const BOUNDS = { min: [-0.42, -0.05, -0.24] as Vec3, max: [0.42, 1.82, 0.26] as Vec3 };

const f = (n: number) => (Number.isInteger(n) ? n.toFixed(1) : String(n));
const v = (x: Vec3) => `vec3(${x.map(f).join(', ')})`;

/** GLSL source of `float sdBody(vec3 p)`, with its helpers. */
// ---------- the idle animation: breathing, a slow sway, the head looking around, the arms swinging ----------

export interface Motion { swayZ: number; swayX: number; headYaw: number; headPitch: number; armL: number; armR: number; breath: number }

export const STILL: Motion = { swayZ: 0, swayX: 0, headYaw: 0, headPitch: 0, armL: 0, armR: 0, breath: 0 };

/** The motion at t seconds: slow, incommensurate periods, so it never looks like a loop. */
export function motionAt(t: number): Motion {
  return {
    swayZ: 0.012 * Math.sin(t * 0.42),
    swayX: 0.008 * Math.sin(t * 0.31 + 1),
    headYaw: 0.3 * Math.sin(t * 0.16) + 0.08 * Math.sin(t * 0.47),
    headPitch: 0.05 * Math.sin(t * 0.23 + 0.5),
    armL: 0.07 * Math.sin(t * 0.5),
    armR: 0.07 * Math.sin(t * 0.5 + 2.8),
    breath: 0.5 + 0.5 * Math.sin(t * 1.25),
  };
}

export const GROUPS: Group[] = ['core', 'chest', 'head', 'armL', 'armR'];
const NECK: Vec3 = pose([0, 1.5, -0.01]);
const SHOULDER_L: Vec3 = pose([0.19, 1.405, -0.006]);
const SHOULDER_R: Vec3 = pose([-0.19, 1.405, -0.006]);
const CHEST: Vec3 = pose([0, 1.28, 0]);
const BREATH = 0.022;

/** Where a point of the posed figure goes when the figure moves (the same transforms as the shader, forward). */
export function animate(p: Vec3, g: Group, m: Motion): Vec3 {
  let q: Vec3 = p;
  if (g === 'head') {
    q = rot(q, NECK, 2, 0, m.headYaw, 1);
    q = rot(q, NECK, 2, 1, m.headPitch, 1);
  } else if (g === 'armL') q = rot(q, SHOULDER_L, 2, 1, m.armL, 1);
  else if (g === 'armR') q = rot(q, SHOULDER_R, 2, 1, m.armR, 1);
  else if (g === 'chest') {
    const b = 1 + BREATH * m.breath;
    q = [CHEST[0] + (q[0] - CHEST[0]) * b, CHEST[1] + (q[1] - CHEST[1]) * b, CHEST[2] + (q[2] - CHEST[2]) * b];
  }
  q = rot(q, [0, 0, 0], 2, 1, m.swayX, 1);
  q = rot(q, [0, 0, 0], 0, 1, m.swayZ, 1);
  return q;
}

/** The group a point of the figure belongs to: the one whose primitives are nearest. */
export function groupAt(p: Vec3): Group {
  let best: Group = 'core';
  let bestD = Infinity;
  for (const prim of BODY) {
    const d = prim.kind === 'ellipsoid' ? sdEllipsoid(sub(p, prim.c), prim.r) : sdRoundCone(p, prim.a, prim.b, prim.r1, prim.r2);
    if (d < bestD) { bestD = d; best = prim.g ?? 'core'; }
  }
  // the chest breathes with the torso: a light inside it moves with it, but near the waist it stays still
  return best;
}


/** GLSL of the animation, inverse (from a point in space back to the figure at rest), plus the forward version for particles. */
const MOTION_GLSL = `
uniform vec4 uMotion;   // swayZ, swayX, headYaw, headPitch
uniform vec3 uMotion2;  // armL, armR, breath
vec2 r2(vec2 v, float a) { float c = cos(a), s = sin(a); return vec2(v.x * c - v.y * s, v.x * s + v.y * c); }
// forward rotation in the plane (i, j) around a pivot, as rot() in TypeScript
vec3 rotZY(vec3 p, vec3 pv, float a) { vec2 d = r2(vec2(p.z - pv.z, p.y - pv.y), a); return vec3(p.x, pv.y + d.y, pv.z + d.x); }
vec3 rotZX(vec3 p, vec3 pv, float a) { vec2 d = r2(vec2(p.z - pv.z, p.x - pv.x), a); return vec3(pv.x + d.y, p.y, pv.z + d.x); }
vec3 rotXY(vec3 p, vec3 pv, float a) { vec2 d = r2(vec2(p.x - pv.x, p.y - pv.y), a); return vec3(pv.x + d.x, pv.y + d.y, p.z); }
const vec3 NECK = ${v(NECK)};
const vec3 SHOULDER_L = ${v(SHOULDER_L)};
const vec3 SHOULDER_R = ${v(SHOULDER_R)};
const vec3 CHEST = ${v(CHEST)};
vec3 swayInv(vec3 p) { return rotZY(rotXY(p, vec3(0.0), -uMotion.x), vec3(0.0), -uMotion.y); }
vec3 animateFwd(vec3 p, float g) {
  vec3 q = p;
  if (g > 1.5 && g < 2.5) { q = rotZX(q, NECK, uMotion.z); q = rotZY(q, NECK, uMotion.w); }
  else if (g > 2.5 && g < 3.5) q = rotZY(q, SHOULDER_L, uMotion2.x);
  else if (g > 3.5) q = rotZY(q, SHOULDER_R, uMotion2.y);
  else if (g > 0.5) q = CHEST + (q - CHEST) * (1.0 + ${f(BREATH)} * uMotion2.z);
  return rotXY(rotZY(q, vec3(0.0), uMotion.y), vec3(0.0), uMotion.x);
}`;

/** GLSL source of `float sdBody(vec3 p)` (animated by uMotion/uMotion2), with its helpers. */
export function bodyGlsl(): string {
  const lines = BODY.map((p, i) => {
    const g = p.g ?? 'core';
    const pv = g === 'head' ? 'ph' : g === 'armL' ? 'pl' : g === 'armR' ? 'pr' : 'p';
    let d: string;
    if (p.kind === 'ellipsoid') {
      d = g === 'chest'
        ? `sdEllipsoid(p - (CHEST + (${v(p.c)} - CHEST) * br), ${v(p.r)} * br)`
        : `sdEllipsoid(${pv} - ${v(p.c)}, ${v(p.r)})`;
    } else {
      d = `sdRoundCone(${pv}, ${v(p.a)}, ${v(p.b)}, ${f(p.r1)}, ${f(p.r2)})`;
    }
    return i === 0 ? `  float d = ${d};` : `  d = smin(d, ${d}, ${f(p.k)});`;
  });
  return `${MOTION_GLSL}
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
float sdBody(vec3 p0) {
  vec3 p = swayInv(p0);
  vec3 ph = rotZX(rotZY(p, NECK, -uMotion.w), NECK, -uMotion.z);
  vec3 pl = rotZY(p, SHOULDER_L, -uMotion2.x);
  vec3 pr = rotZY(p, SHOULDER_R, -uMotion2.y);
  float br = 1.0 + ${f(BREATH)} * uMotion2.z;
${lines.join('\n')}
  return d;
}`;
}

/** GLSL for the particles: animateFwd(position, group). */
export function motionGlsl(): string {
  return MOTION_GLSL;
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
