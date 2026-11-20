import { Vec3 } from './body-shape';

// Channels drawn as lines of light: the twelve meridians of Chinese medicine (one per zang-fu node, drawn on both
// sides of the body) and the three nadis of yoga. Paths are simplified traces of the classical routes (Lingshu 10),
// in body coordinates (metres, person's left at +x, see body-shape.ts). Points are for the left side; mirrored.

export const MERIDIANS: Record<string, Vec3[]> = {
  'zf-lung': [[0.11, 1.37, 0.05], [0.17, 1.38, 0.04], [0.22, 1.25, 0.04], [0.25, 1.12, 0.03], [0.29, 0.98, 0.035], [0.33, 0.8, 0.045]],
  'zf-large-intestine': [[0.335, 0.78, 0.04], [0.3, 0.97, 0.0], [0.265, 1.13, -0.02], [0.19, 1.43, 0.0], [0.05, 1.5, 0.03], [0.012, 1.6, 0.09]],
  'zf-stomach': [[0.03, 1.63, 0.08], [0.05, 1.56, 0.06], [0.04, 1.49, 0.04], [0.07, 1.35, 0.1], [0.06, 1.1, 0.09], [0.08, 0.92, 0.08],
    [0.1, 0.7, 0.07], [0.1, 0.5, 0.05], [0.11, 0.25, 0.04], [0.12, 0.03, 0.13]],
  'zf-spleen': [[0.1, 0.03, 0.13], [0.08, 0.08, 0.0], [0.075, 0.3, 0.0], [0.07, 0.5, 0.02], [0.06, 0.72, 0.04], [0.1, 1.0, 0.08], [0.14, 1.32, 0.06]],
  'zf-heart': [[0.15, 1.36, 0.0], [0.21, 1.24, -0.01], [0.245, 1.12, 0.0], [0.3, 0.89, 0.02], [0.33, 0.77, 0.03]],
  'zf-small-intestine': [[0.335, 0.77, 0.0], [0.3, 0.95, -0.02], [0.26, 1.12, -0.04], [0.15, 1.38, -0.08], [0.06, 1.52, -0.02],
    [0.06, 1.6, 0.06], [0.08, 1.63, 0.01]],
  'zf-bladder': [[0.015, 1.64, 0.08], [0.02, 1.74, 0.03], [0.02, 1.66, -0.09], [0.035, 1.4, -0.1], [0.04, 1.05, -0.09], [0.06, 0.88, -0.1],
    [0.09, 0.65, -0.06], [0.1, 0.5, -0.04], [0.105, 0.33, -0.07], [0.13, 0.07, -0.02], [0.14, 0.03, 0.1]],
  'zf-kidney': [[0.11, 0.01, 0.05], [0.08, 0.07, -0.02], [0.075, 0.32, -0.02], [0.07, 0.5, -0.01], [0.05, 0.75, 0.0], [0.02, 1.0, 0.09],
    [0.03, 1.35, 0.1]],
  'zf-pericardium': [[0.1, 1.3, 0.1], [0.15, 1.33, 0.03], [0.21, 1.24, 0.03], [0.25, 1.12, 0.02], [0.3, 0.89, 0.035], [0.335, 0.76, 0.035]],
  'zf-triple-burner': [[0.335, 0.77, -0.005], [0.3, 0.95, -0.03], [0.26, 1.13, -0.045], [0.19, 1.42, -0.05], [0.07, 1.53, -0.02],
    [0.085, 1.62, -0.02], [0.04, 1.66, 0.08]],
  'zf-gallbladder': [[0.05, 1.64, 0.07], [0.08, 1.68, 0.0], [0.075, 1.6, -0.04], [0.12, 1.45, -0.02], [0.14, 1.2, 0.0], [0.15, 0.92, 0.0],
    [0.13, 0.7, 0.0], [0.12, 0.5, 0.0], [0.125, 0.28, 0.0], [0.14, 0.03, 0.11]],
  'zf-liver': [[0.095, 0.03, 0.12], [0.09, 0.06, 0.06], [0.075, 0.3, 0.01], [0.07, 0.5, 0.0], [0.05, 0.75, 0.03], [0.04, 0.88, 0.07],
    [0.08, 1.16, 0.09]],
};

const mirrorPath = (p: Vec3[]): Vec3[] => p.map(([x, y, z]) => [-x, y, z]);

/** Both sides of every meridian. */
export function meridianPaths(): { zone: string; points: Vec3[] }[] {
  return Object.entries(MERIDIANS).flatMap(([zone, points]) => [
    { zone, points },
    { zone, points: mirrorPath(points) },
  ]);
}

/** Sushumna up the spine; Ida (left, lunar) and Pingala (right, solar) spiralling around it, meeting at Ajna. */
export function nadiPaths(): { zone: string; points: Vec3[] }[] {
  const bottom = 0.87;
  const top = 1.645;
  const n = 60;
  const spiral = (sign: number): Vec3[] =>
    Array.from({ length: n + 1 }, (_, i) => {
      const t = i / n;
      const y = bottom + t * (top - bottom);
      // five crossings, closing at Ajna between the eyebrows
      const x = sign * 0.038 * Math.sin(Math.PI * 5 * t) * (1 - 0.3 * t);
      const z = -0.03 + t * 0.1 * t;
      return [x, y, z];
    });
  return [
    { zone: 'sushumna', points: [[0, bottom, -0.035], [0, 1.2, -0.04], [0, 1.5, -0.03], [0, 1.77, -0.005]] },
    { zone: 'ida', points: spiral(1) },
    { zone: 'pingala', points: spiral(-1) },
  ];
}

/** Natural colours of the nadis (they are not tied to a quality in the tradition). */
export const NADI_COLOR: Record<string, string> = { ida: '#bcd0ff', pingala: '#ffb46b', sushumna: '#ffe3a0' };
