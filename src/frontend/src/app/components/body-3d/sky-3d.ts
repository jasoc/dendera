import * as THREE from 'three';
import { PlacedPoint, Signal } from '../../core/models';
import { BODY_GLYPH, SIGNS, SIGN_GLYPH, qualityHex } from '../../core/symbols';

// The sky around the body of light, revealed by zooming out: the ecliptic with the twelve signs, the planets at
// their real longitudes, aspects between planets, transits as beams to the organs they touch, the geomagnetic
// aurora and the Schumann wave. Every visible element is a signal.

const R = 4.2; // ecliptic radius (m)
const CENTER = new THREE.Vector3(0, 1.0, 0);
const TILT = -0.42; // the ecliptic leans towards the viewer, like a sky chart seen from slightly above
const GOLD = new THREE.Color('#d6a84c');
const SYMBOL_FONT = "'Segoe UI Symbol', 'Noto Sans Symbols', 'Noto Sans Symbols 2', 'DejaVu Sans', serif";

/** A transit's consequences: which organs it reaches (for the beams). */
export interface Beam { signalKey: string; body: string; target: THREE.Vector3; quality: string | null; amount: number }

export class Sky3d {
  readonly group = new THREE.Group();
  private readonly frame = new THREE.Group(); // the tilted ecliptic plane
  private readonly dynamic = new THREE.Group(); // rebuilt on every reading
  private readonly materials: { m: THREE.Material & { opacity: number }; base: number }[] = [];
  private readonly planetPos = new Map<string, THREE.Vector3>();
  private schumann?: { line: THREE.Line; amp: number };
  private aurora?: THREE.ShaderMaterial;
  private opacity = 1;
  readonly pickables: { label: string; world: THREE.Vector3 }[] = [];

  constructor() {
    this.frame.position.copy(CENTER);
    this.frame.rotation.x = TILT;
    this.group.add(this.frame, this.dynamic);
    this.buildZodiac();
  }

  private track<M extends THREE.Material & { opacity: number }>(m: M, base = m.opacity): M {
    m.transparent = true;
    m.depthWrite = false;
    this.materials.push({ m, base });
    m.opacity = base * this.opacity;
    return m;
  }

  private circle(radius: number, color: THREE.ColorRepresentation, opacity: number, segments = 256) {
    const pts = Array.from({ length: segments + 1 }, (_, i) => {
      const a = (i / segments) * Math.PI * 2;
      return new THREE.Vector3(radius * Math.cos(a), 0, -radius * Math.sin(a));
    });
    return new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts),
      this.track(new THREE.LineBasicMaterial({ color, opacity, blending: THREE.AdditiveBlending })));
  }

  private buildZodiac() {
    this.frame.add(this.circle(R, GOLD, 0.55), this.circle(R * 0.9, GOLD, 0.25), this.circle(R * 1.14, GOLD, 0.12));
    const ticks: THREE.Vector3[] = [];
    SIGNS.forEach((sign, i) => {
      const a = (i * 30 * Math.PI) / 180;
      ticks.push(new THREE.Vector3(R * 0.9 * Math.cos(a), 0, -R * 0.9 * Math.sin(a)),
        new THREE.Vector3(R * Math.cos(a), 0, -R * Math.sin(a)));
      const mid = ((i * 30 + 15) * Math.PI) / 180;
      const glyph = this.textSprite(SIGN_GLYPH[sign].replace('︎', ''), '#d6a84c', 0.36, 0.7);
      glyph.position.set(R * 0.95 * Math.cos(mid), 0, -R * 0.95 * Math.sin(mid));
      this.frame.add(glyph);
    });
    this.frame.add(new THREE.LineSegments(new THREE.BufferGeometry().setFromPoints(ticks),
      this.track(new THREE.LineBasicMaterial({ color: GOLD, opacity: 0.4, blending: THREE.AdditiveBlending }))));

    // Schumann: a standing wave between the ground and the ionosphere, around the feet.
    const wave = new THREE.Line(new THREE.BufferGeometry().setFromPoints(Array.from({ length: 257 }, () => new THREE.Vector3())),
      this.track(new THREE.LineBasicMaterial({ color: '#6fa8dc', opacity: 0.7, blending: THREE.AdditiveBlending })));
    this.group.add(wave, this.circle(1.75, '#6fa8dc', 0.12).translateY(0.02));
    this.schumann = { line: wave, amp: 0 };

    // Geomagnetic aurora: luminous curtains on an open cylinder above the body.
    this.aurora = new THREE.ShaderMaterial({
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
      uniforms: { uTime: { value: 0 }, uIntensity: { value: 0 }, uOpacity: { value: 1 } },
      vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
      fragmentShader: `
        varying vec2 vUv; uniform float uTime; uniform float uIntensity; uniform float uOpacity;
        void main(){
          float x = vUv.x * 40.0;
          float curtain = 0.5 + 0.5 * sin(x + sin(x * 0.37 + uTime * 0.4) * 2.0 + uTime * 0.25);
          curtain *= 0.6 + 0.4 * sin(x * 2.3 - uTime * 0.6);
          float fade = smoothstep(0.0, 0.25, vUv.y) * (1.0 - smoothstep(0.55, 1.0, vUv.y));
          vec3 col = mix(vec3(0.25, 1.0, 0.6), vec3(0.6, 0.35, 1.0), vUv.y);
          gl_FragColor = vec4(col * curtain * fade * uIntensity * uOpacity * 0.55, 1.0);
        }`,
    });
    const curtain = new THREE.Mesh(new THREE.CylinderGeometry(2.6, 2.2, 2.2, 96, 1, true), this.aurora);
    curtain.position.y = 3.0;
    this.group.add(curtain);
  }

  private textSprite(text: string, color: string, size: number, opacity: number) {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 128;
    const ctx = canvas.getContext('2d')!;
    ctx.fillStyle = color;
    ctx.font = `64px ${SYMBOL_FONT}`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.shadowColor = color;
    ctx.shadowBlur = 16;
    ctx.fillText(text, 64, 68);
    const tex = new THREE.CanvasTexture(canvas);
    tex.colorSpace = THREE.SRGBColorSpace;
    const sprite = new THREE.Sprite(this.track(new THREE.SpriteMaterial({ map: tex, opacity, blending: THREE.AdditiveBlending })));
    sprite.scale.setScalar(size);
    return sprite;
  }

  private glowSprite(color: THREE.ColorRepresentation, size: number, opacity: number) {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 64;
    const ctx = canvas.getContext('2d')!;
    const g = ctx.createRadialGradient(32, 32, 0, 32, 32, 32);
    g.addColorStop(0, 'rgba(255,255,255,1)');
    g.addColorStop(0.25, 'rgba(255,255,255,0.55)');
    g.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, 64, 64);
    const sprite = new THREE.Sprite(this.track(new THREE.SpriteMaterial({
      map: new THREE.CanvasTexture(canvas), color, opacity, blending: THREE.AdditiveBlending,
    })));
    sprite.scale.setScalar(size);
    return sprite;
  }

  private worldOnEcliptic(lon: number, radius = R) {
    const a = (lon * Math.PI) / 180;
    return this.frame.localToWorld(new THREE.Vector3(radius * Math.cos(a), 0, -radius * Math.sin(a)));
  }

  /** Rebuilds planets, aspects, beams and the physical signals for one reading. */
  update(sky: PlacedPoint[], signals: Signal[], beams: Beam[]) {
    this.frame.updateMatrixWorld(true);
    for (const child of [...this.dynamic.children]) {
      this.dynamic.remove(child);
      child.traverse((o) => {
        const any = o as THREE.Mesh;
        any.geometry?.dispose();
        const mat = any.material as THREE.Material | undefined;
        if (mat) {
          const i = this.materials.findIndex((x) => x.m === mat);
          if (i >= 0) this.materials.splice(i, 1);
          (mat as THREE.SpriteMaterial).map?.dispose();
          mat.dispose();
        }
      });
    }
    this.planetPos.clear();
    this.pickables.length = 0;

    for (const p of sky) {
      if (p.id === 'north-node') continue;
      const world = this.worldOnEcliptic(p.longitude);
      this.planetPos.set(p.id, world);
      const color = p.retrograde ? '#a081e6' : p.id === 'sun' ? '#ffd27a' : p.id === 'moon' ? '#dfe6ff' : '#f3dca4';
      const glow = this.glowSprite(color, p.id === 'sun' || p.id === 'moon' ? 0.9 : 0.55, 0.9);
      glow.position.copy(world);
      const glyph = this.textSprite((BODY_GLYPH[p.id] ?? '•') + (p.retrograde ? '℞' : ''), color, 0.42, 1);
      glyph.position.copy(world).add(new THREE.Vector3(0, 0.32, 0));
      this.dynamic.add(glow, glyph);
      this.pickables.push({ label: `${p.label} ${p.position}${p.retrograde ? ' · retrogrado' : ''}`, world });
    }

    // Aspects between planets: chords across the ecliptic, coloured by quality.
    for (const s of signals.filter((x) => x.kind === 'sky-aspect')) {
      const a = this.planetPos.get(s.tags['bodyA']);
      const b = this.planetPos.get(s.tags['bodyB']);
      if (!a || !b) continue;
      const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints([a, b]),
        this.track(new THREE.LineBasicMaterial({ color: qualityHex(s.tags['quality']), opacity: 0.15 + 0.55 * s.intensity, blending: THREE.AdditiveBlending })));
      this.dynamic.add(line);
    }

    // Transits: beams of light from the planet to every organ the schools say it touches.
    for (const beam of beams) {
      const from = this.planetPos.get(beam.body);
      if (!from) continue;
      const mid = from.clone().lerp(beam.target, 0.5).add(new THREE.Vector3(0, 0.9, 0));
      const curve = new THREE.QuadraticBezierCurve3(from, mid, beam.target);
      const pts = curve.getPoints(48);
      const geo = new THREE.BufferGeometry().setFromPoints(pts);
      const color = new THREE.Color(qualityHex(beam.quality));
      geo.setAttribute('color', new THREE.Float32BufferAttribute(pts.flatMap((_, i) => {
        const t = i / (pts.length - 1);
        const k = 0.25 + 0.75 * t; // brighter where it reaches the body
        return [color.r * k, color.g * k, color.b * k];
      }), 3));
      this.dynamic.add(new THREE.Line(geo, this.track(new THREE.LineBasicMaterial({
        vertexColors: true, opacity: Math.min(0.9, 0.2 + beam.amount * 0.6), blending: THREE.AdditiveBlending,
      }))));
    }

    const kp = signals.find((s) => s.kind === 'geomagnetic');
    if (this.aurora) this.aurora.uniforms['uIntensity'].value = kp ? 0.15 + kp.intensity : 0.1;
    const sch = signals.find((s) => s.kind === 'schumann');
    if (this.schumann) this.schumann.amp = sch ? 0.02 + 0.09 * sch.intensity : 0.015;
  }

  setOpacity(o: number) {
    this.opacity = o;
    for (const { m, base } of this.materials) m.opacity = base * o;
    if (this.aurora) this.aurora.uniforms['uOpacity'].value = o;
    this.group.visible = o > 0.01;
  }

  tick(t: number) {
    if (this.aurora) this.aurora.uniforms['uTime'].value = t;
    if (this.schumann) {
      const pos = this.schumann.line.geometry.attributes['position'] as THREE.BufferAttribute;
      for (let i = 0; i < pos.count; i++) {
        const a = (i / (pos.count - 1)) * Math.PI * 2;
        const r = 1.45 + this.schumann.amp * Math.sin(a * 8 + t * 2.2) * Math.sin(t * 0.9 + a * 2);
        pos.setXYZ(i, r * Math.cos(a), 0.03 + this.schumann.amp * 0.6 * Math.sin(a * 8 - t * 1.6), -r * Math.sin(a));
      }
      pos.needsUpdate = true;
    }
  }

  dispose() {
    this.group.traverse((o) => {
      const any = o as THREE.Mesh;
      any.geometry?.dispose();
      const mat = any.material as THREE.Material | undefined;
      (mat as THREE.SpriteMaterial | undefined)?.map?.dispose();
      mat?.dispose();
    });
  }
}
