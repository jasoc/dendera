import { BodyLayer } from '../../core/models';
import { Vec3 } from './body-shape';

// Where every node of the body model glows in 3D (metres, see body-shape.ts). Ids MUST match
// Dendera.Core/Rules/BodyZones.cs. Inner layers (temperament, emotions, mind) sit in their seat organ.

export interface Anchor3d { p: Vec3; r: number }

const both = (x: number, y: number, z: number, r: number): Anchor3d[] => [
  { p: [x, y, z], r },
  { p: [-x, y, z], r },
];

export const ANCHORS_3D: Record<string, Anchor3d[]> = {
  // soma
  brain: [{ p: [0, 1.67, -0.005], r: 0.055 }],
  head: [{ p: [0, 1.6, 0.05], r: 0.06 }],
  throat: [{ p: [0, 1.5, 0.02], r: 0.035 }],
  shoulders: both(0.19, 1.405, 0, 0.05),
  lungs: both(0.068, 1.32, 0, 0.06),
  heart: [{ p: [0.035, 1.27, 0.025], r: 0.045 }],
  stomach: [{ p: [0.045, 1.155, 0.03], r: 0.045 }],
  'solar-plexus': [{ p: [0, 1.19, 0.055], r: 0.04 }],
  liver: [{ p: [-0.058, 1.14, 0.02], r: 0.05 }],
  gut: [{ p: [0, 1.03, 0.03], r: 0.07 }],
  kidneys: both(0.055, 1.09, -0.045, 0.035),
  pelvis: [{ p: [0, 0.92, 0.01], r: 0.07 }],
  thighs: both(0.095, 0.72, 0, 0.06),
  knees: both(0.104, 0.5, 0.012, 0.04),
  calves: both(0.107, 0.33, -0.02, 0.045),
  feet: both(0.113, 0.04, 0.04, 0.045),
  // senses
  eyes: both(0.032, 1.645, 0.085, 0.018),
  ears: both(0.083, 1.63, 0.0, 0.02),
  nose: [{ p: [0, 1.61, 0.095], r: 0.015 }],
  mouth: [{ p: [0, 1.575, 0.085], r: 0.018 }],
  // zang-fu (their meridians are drawn as lines, see channels-3d.ts)
  'zf-liver': [{ p: [-0.065, 1.13, 0.0], r: 0.05 }],
  'zf-gallbladder': [{ p: [-0.05, 1.1, 0.035], r: 0.03 }],
  'zf-heart': [{ p: [0.03, 1.27, 0.0], r: 0.045 }],
  'zf-pericardium': [{ p: [0.03, 1.27, 0.035], r: 0.055 }],
  'zf-small-intestine': [{ p: [0, 1.0, 0.03], r: 0.06 }],
  'zf-large-intestine': both(0.07, 1.0, 0.02, 0.04),
  'zf-triple-burner': [{ p: [0, 1.3, 0.06], r: 0.04 }, { p: [0, 1.15, 0.06], r: 0.04 }, { p: [0, 0.98, 0.06], r: 0.04 }],
  'zf-spleen': [{ p: [0.07, 1.15, 0.0], r: 0.04 }],
  'zf-stomach': [{ p: [0.035, 1.16, 0.04], r: 0.04 }],
  'zf-lung': both(0.075, 1.32, -0.01, 0.06),
  'zf-kidney': both(0.055, 1.08, -0.05, 0.035),
  'zf-bladder': [{ p: [0, 0.88, 0.03], r: 0.035 }],
  // the five Shen
  shen: [{ p: [0, 1.29, 0.04], r: 0.05 }],
  hun: [{ p: [-0.065, 1.15, -0.02], r: 0.05 }],
  po: both(0.07, 1.33, 0.02, 0.04),
  yi: [{ p: [0.05, 1.16, -0.01], r: 0.04 }],
  zhi: both(0.056, 1.08, -0.03, 0.035),
  // substances with a seat (the others tint the aura)
  xue: [{ p: [0.03, 1.25, -0.02], r: 0.07 }],
  jing: [{ p: [0, 0.98, 0.0], r: 0.06 }],
  // chakras, along the central axis
  muladhara: [{ p: [0, 0.87, -0.01], r: 0.045 }],
  svadhisthana: [{ p: [0, 0.96, 0.01], r: 0.045 }],
  manipura: [{ p: [0, 1.08, 0.03], r: 0.045 }],
  anahata: [{ p: [0, 1.26, 0.02], r: 0.05 }],
  vishuddha: [{ p: [0, 1.48, 0.01], r: 0.04 }],
  ajna: [{ p: [0, 1.645, 0.07], r: 0.03 }],
  sahasrara: [{ p: [0, 1.77, 0.0], r: 0.05 }],
  // nadis (drawn as lines; these small lights make them pickable)
  ida: [{ p: [0.03, 1.2, -0.03], r: 0.02 }],
  pingala: [{ p: [-0.03, 1.2, -0.03], r: 0.02 }],
  sushumna: [{ p: [0, 1.35, -0.035], r: 0.02 }],
  // temperament: wide, slow clouds around their organ
  sanguine: [{ p: [0.03, 1.27, 0.0], r: 0.12 }],
  choleric: [{ p: [-0.05, 1.14, 0.0], r: 0.11 }],
  melancholic: [{ p: [0.04, 1.12, -0.01], r: 0.11 }],
  phlegmatic: [{ p: [0, 1.33, -0.01], r: 0.12 }],
  // emotions: bright flames in the organ that houses them
  anger: [{ p: [-0.06, 1.15, 0.045], r: 0.06 }],
  joy: [{ p: [0.035, 1.28, 0.05], r: 0.055 }],
  worry: both(0.075, 1.31, 0.045, 0.045),
  pensiveness: [{ p: [0.05, 1.17, 0.05], r: 0.055 }],
  grief: both(0.07, 1.33, 0.03, 0.05),
  fear: both(0.056, 1.09, -0.02, 0.045),
  fright: [{ p: [0.03, 1.28, 0.06], r: 0.05 }],
  // mind: the three cells of the brain, front to back, and mood around the heart
  imagination: [{ p: [0, 1.67, 0.045], r: 0.03 }],
  reason: [{ p: [0, 1.685, 0.0], r: 0.03 }],
  memory: [{ p: [0, 1.665, -0.045], r: 0.03 }],
  mood: [{ p: [0, 1.25, 0.03], r: 0.11 }],
};

/**
 * How each layer looks: brightness gain and kind index used by the shader
 * (0 organ, 1 slow cloud, 2 flame, 3 radiant mind/spirit, 4 chakra with its lotus ring).
 * Nodes without anchors (systems, qi, yin, yang, jinye) tint the aura instead.
 */
export const LAYER_STYLE: Record<BodyLayer, { gain: number; kind: number }> = {
  soma: { gain: 1, kind: 0 },
  systems: { gain: 0, kind: 0 },
  temperament: { gain: 0.55, kind: 1 },
  emotions: { gain: 1.25, kind: 2 },
  mind: { gain: 1.1, kind: 3 },
  zangfu: { gain: 0.9, kind: 0 },
  spirits: { gain: 1.0, kind: 3 },
  substances: { gain: 0.6, kind: 1 },
  chakras: { gain: 1.2, kind: 4 },
  nadis: { gain: 0.35, kind: 0 },
};
