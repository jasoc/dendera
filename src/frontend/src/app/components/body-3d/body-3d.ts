import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  afterNextRender,
  effect,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { Line2 } from 'three/examples/jsm/lines/Line2.js';
import { LineGeometry } from 'three/examples/jsm/lines/LineGeometry.js';
import { LineMaterial } from 'three/examples/jsm/lines/LineMaterial.js';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { PlacedPoint, Signal, ZoneActivation } from '../../core/models';
import { qualityHex } from '../../core/symbols';
import { BOUNDS, GROUPS, Group, Motion, STILL, Vec3, animate, bodyGlsl, groupAt, motionAt, motionGlsl, samplePointsInside } from './body-shape';
import { ANCHORS_3D, LAYER_STYLE } from './anchors-3d';
import { Beam, Sky3d } from './sky-3d';
import { NADI_COLOR, meridianPaths, nadiPaths } from './channels-3d';

const MAX_LIGHTS = 56; // 3 vec4 arrays (168 vectors): under the 224 fragment uniform vectors WebGL2 guarantees
const TARGET = new THREE.Vector3(0, 1.0, 0);
const BODY_DISTANCE = 3.1;
const SKY_DISTANCE = 12.5;

interface Light { zone: string; label: string; score: number; p: THREE.Vector3; base: Vec3; group: Group; slot: number }

/**
 * The body of light: a translucent human figure (raymarched signed distance field, golden rim, energy bands)
 * with every node of the body model glowing inside it — organs, temperaments, emotions, the cells of the mind —
 * coloured by their dominant quality, as bright as their score. Zoom out and the sky appears around it.
 * Without WebGL the projected content (the 2D body map) is shown instead.
 */
@Component({
  selector: 'app-body-3d',
  imports: [MatButtonToggleModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (webgl()) {
      <div class="stage" #stage>
        <canvas #canvas (pointermove)="onMove($event)" (pointerleave)="hover.set(null)" (click)="onClick($event)"></canvas>
        @if (hover(); as h) {
          <div class="tip" [style.left.px]="h.x" [style.top.px]="h.y">{{ h.label }}</div>
        }
        <mat-button-toggle-group class="view-toggle" [value]="view()" (change)="go($event.value)" hideSingleSelectionIndicator>
          <mat-button-toggle value="body">Corpo</mat-button-toggle>
          <mat-button-toggle value="sky">Cielo</mat-button-toggle>
        </mat-button-toggle-group>
      </div>
    } @else {
      <ng-content />
    }
  `,
  styles: `
    :host { display: block; height: 100%; }
    .stage { position: relative; height: 100%; min-height: 320px; overflow: hidden;
      background: radial-gradient(circle at 50% 40%, #0c2236, #050b14 70%); }
    canvas { width: 100%; height: 100%; display: block; touch-action: none; cursor: grab; }
    canvas:active { cursor: grabbing; }
    .tip { position: absolute; transform: translate(-50%, -140%); pointer-events: none; white-space: nowrap;
      font-size: 12px; padding: 4px 10px; border-radius: 999px; color: var(--text);
      background: color-mix(in srgb, var(--mat-sys-surface-container-highest) 85%, transparent); backdrop-filter: blur(6px); }
    .view-toggle { position: absolute; left: 50%; bottom: var(--stage-toggle-bottom, 18px); transform: translateX(-50%);
      --mat-standard-button-toggle-height: 32px; backdrop-filter: blur(8px);
      background: color-mix(in srgb, var(--mat-sys-surface-container) 55%, transparent); border-radius: 999px; }
  `,
})
export class Body3d implements OnDestroy {
  readonly zones = input.required<ZoneActivation[]>();
  readonly sky = input<PlacedPoint[]>([]);
  readonly signals = input<Signal[]>([]);
  readonly selected = input<string | null>(null);
  /** Layers whose lights are shown and pickable (null = all): picking an emotion over an organ gets easy. */
  readonly layers = input<string[] | null>(null);
  readonly pick = output<string>();

  protected readonly webgl = signal(true);
  protected readonly hover = signal<{ label: string; x: number; y: number } | null>(null);
  protected readonly view = signal<'body' | 'sky'>('body');

  private readonly stageRef = viewChild<ElementRef<HTMLDivElement>>('stage');
  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvas');

  private renderer?: THREE.WebGLRenderer;
  private composer?: EffectComposer;
  private camera?: THREE.PerspectiveCamera;
  private controls?: OrbitControls;
  private bodyMaterial?: THREE.ShaderMaterial;
  private particles?: THREE.Points;
  private particleBase?: Float32Array;
  private sky3d?: Sky3d;
  private channels: { zone: string; line: Line2; material: LineMaterial; base: Vec3[]; groups: Group[] }[] = [];
  private lights: Light[] = [];
  private raf = 0;
  private resize?: ResizeObserver;
  private flyTo: number | null = null;
  /** When the user last let go: after a while the camera sways gently, like someone walking past a statue. */
  private idleSince = 0;
  private readonly clock = new THREE.Clock();
  private readonly reducedMotion = typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  constructor() {
    afterNextRender(() => this.init());
    effect(() => {
      const zones = this.zones();
      const selected = this.selected();
      const sky = this.sky();
      const signals = this.signals();
      this.layers();
      if (this.bodyMaterial) this.updateScene(zones, selected, sky, signals);
    });
  }

  // ---------- setup ----------

  private init() {
    const canvas = this.canvasRef()?.nativeElement;
    const stage = this.stageRef()?.nativeElement;
    if (!canvas || !stage) return;
    try {
      this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
    } catch {
      this.webgl.set(false);
      return;
    }
    const renderer = this.renderer;
    renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
    renderer.toneMapping = THREE.ACESFilmicToneMapping;

    const scene = new THREE.Scene();
    scene.background = new THREE.Color('#050b14');
    this.camera = new THREE.PerspectiveCamera(35, 1, 0.05, 100);
    this.camera.position.set(0.12, 1.12, BODY_DISTANCE);

    const controls = (this.controls = new OrbitControls(this.camera, canvas));
    controls.target.copy(TARGET);
    controls.enableDamping = true;
    controls.enablePan = false;
    // Free to walk around the figure; it turns slowly by itself when left alone. Zoom out for the sky.
    controls.minDistance = 1.3;
    controls.maxDistance = 17;
    controls.minPolarAngle = 0.9;
    controls.maxPolarAngle = 2.1;
    controls.rotateSpeed = 0.6;
    controls.autoRotateSpeed = 0.35;
    controls.addEventListener('start', () => {
      this.flyTo = null;
      this.idleSince = Number.POSITIVE_INFINITY;
    });
    controls.addEventListener('end', () => (this.idleSince = this.clock.getElapsedTime()));

    scene.add(this.makeStars(), this.makeMandala(), this.makeBody(), (this.particles = this.makeParticles()), this.makeChannels());
    this.sky3d = new Sky3d();
    scene.add(this.sky3d.group);

    this.composer = new EffectComposer(renderer);
    this.composer.addPass(new RenderPass(scene, this.camera));
    this.composer.addPass(new UnrealBloomPass(new THREE.Vector2(512, 512), 0.5, 0.45, 0.3));
    this.composer.addPass(new OutputPass());

    this.resize = new ResizeObserver(() => this.fit());
    this.resize.observe(stage);
    this.fit();
    this.updateScene(this.zones(), this.selected(), this.sky(), this.signals());
    this.loop();
  }

  private fit() {
    const stage = this.stageRef()?.nativeElement;
    if (!stage || !this.renderer || !this.camera || !this.composer) return;
    const { width, height } = stage.getBoundingClientRect();
    this.renderer.setSize(width, height, false);
    this.composer.setSize(width, height);
    this.camera.aspect = width / Math.max(1, height);
    for (const c of this.channels) c.material.resolution.set(width, height);
    this.camera.updateProjectionMatrix();
  }

  private makeBody() {
    const { min, max } = BOUNDS;
    const size = new THREE.Vector3(max[0] - min[0] + 0.2, max[1] - min[1] + 0.2, max[2] - min[2] + 0.24);
    const center = new THREE.Vector3((min[0] + max[0]) / 2, (min[1] + max[1]) / 2, (min[2] + max[2]) / 2);
    const boxMin = center.clone().sub(size.clone().multiplyScalar(0.5));
    const boxMax = center.clone().add(size.clone().multiplyScalar(0.5));

    this.bodyMaterial = new THREE.ShaderMaterial({
      transparent: true,
      depthWrite: false,
      side: THREE.BackSide,
      blending: THREE.AdditiveBlending,
      uniforms: {
        uTime: { value: 0 },
        uCount: { value: 0 },
        uPos: { value: Array.from({ length: MAX_LIGHTS }, () => new THREE.Vector4()) },
        uCol: { value: Array.from({ length: MAX_LIGHTS }, () => new THREE.Vector4()) },
        uMeta: { value: Array.from({ length: MAX_LIGHTS }, () => new THREE.Vector4()) },
        uAuraColor: { value: new THREE.Color('#45d4ff') },
        uAura: { value: 0 },
        uMotion: { value: new THREE.Vector4() },
        uMotion2: { value: new THREE.Vector3() },
        uBoxMin: { value: boxMin },
        uBoxMax: { value: boxMax },
      },
      vertexShader: `
        varying vec3 vWorld;
        void main() {
          vec4 w = modelMatrix * vec4(position, 1.0);
          vWorld = w.xyz;
          gl_Position = projectionMatrix * viewMatrix * w;
        }`,
      fragmentShader: `
        #define MAX_LIGHTS ${MAX_LIGHTS}
        uniform float uTime; uniform int uCount;
        uniform vec4 uPos[MAX_LIGHTS]; uniform vec4 uCol[MAX_LIGHTS]; uniform vec4 uMeta[MAX_LIGHTS];
        uniform vec3 uAuraColor; uniform float uAura; uniform vec3 uBoxMin; uniform vec3 uBoxMax;
        varying vec3 vWorld;
        ${bodyGlsl()}
        vec2 boxHit(vec3 ro, vec3 rd) {
          vec3 inv = 1.0 / rd; vec3 t0 = (uBoxMin - ro) * inv; vec3 t1 = (uBoxMax - ro) * inv;
          vec3 a = min(t0, t1); vec3 b = max(t0, t1);
          return vec2(max(max(a.x, a.y), a.z), min(min(b.x, b.y), b.z));
        }
        vec3 normalAt(vec3 p) {
          const vec2 e = vec2(0.0015, -0.0015);
          return normalize(e.xyy * sdBody(p + e.xyy) + e.yyx * sdBody(p + e.yyx) + e.yxy * sdBody(p + e.yxy) + e.xxx * sdBody(p + e.xxx));
        }
        void main() {
          vec3 ro = cameraPosition;
          vec3 rd = normalize(vWorld - cameraPosition);
          vec2 tb = boxHit(ro, rd);
          float t = max(tb.x, 0.0);
          float minD = 1e3;
          bool hit = false;
          for (int i = 0; i < 90; i++) {
            float d = sdBody(ro + rd * t);
            minD = min(minD, d);
            if (d < 0.0008) { hit = true; break; }
            t += max(d * 0.9, 0.0025);
            if (t > tb.y) break;
          }

          // hologram palette: cyan rim, deep blue body, white-cyan scan lines (names kept from the old palette)
          vec3 gold = vec3(0.32, 0.86, 1.0);
          vec3 violet = vec3(0.10, 0.24, 0.75);
          vec3 col = vec3(0.0);

          if (hit) {
            vec3 p = ro + rd * t;
            vec3 n = normalAt(p);
            float fr = pow(1.0 - max(dot(n, -rd), 0.0), 2.3);
            // skin of light: violet where it faces you, gold at the silhouette, slow energy rising through it
            float bands = smoothstep(0.93, 1.0, fract(p.y * 7.0 - uTime * 0.09)) * 0.5
              + smoothstep(0.6, 1.0, fract(p.y * 90.0 - uTime * 0.6)) * 0.12;
            col += mix(violet * 0.06, gold * 0.7, fr) + gold * bands * (0.08 + 0.6 * fr);
            // the organs light the skin from inside
            vec3 inner = vec3(0.0);
            for (int j = 0; j < MAX_LIGHTS; j++) {
              if (j >= uCount) break;
              vec3 q = p - uPos[j].xyz; float r = uPos[j].w * 2.4;
              inner += uCol[j].rgb * uCol[j].a * exp(-dot(q, q) / (r * r));
            }
            col += inner * (0.12 + 0.3 * fr) + uAuraColor * uAura * fr * 0.35;
          } else {
            float halo = exp(-max(minD, 0.0) * 42.0);
            col += mix(violet, gold, 0.55) * halo * 0.1 + uAuraColor * uAura * halo * 0.2;
          }

          // volumetric lights: the integral of each gaussian along the ray (analytic, no marching)
          for (int j = 0; j < MAX_LIGHTS; j++) {
            if (j >= uCount) break;
            vec4 m = uMeta[j];
            float speed = m.x == 2.0 ? 2.4 : (m.x == 1.0 ? 0.6 : (m.x == 4.0 ? 0.9 : 1.3));
            float amp = m.x == 2.0 ? 0.16 : 0.06;
            float r = uPos[j].w * (1.0 + amp * sin(uTime * speed + m.y)) * (1.0 + 0.3 * m.z);
            vec3 oc = uPos[j].xyz - ro;
            float tc = dot(oc, rd);
            vec3 perp = oc - rd * tc;
            float d2 = dot(perp, perp);
            float g = exp(-d2 / (r * r)) * r * 1.7724539 * step(0.0, tc);
            col += uCol[j].rgb * g * uCol[j].a * (1.0 + 1.6 * m.z) * 6.0;
            if (m.w > 0.25) {
              float ring = exp(-pow((sqrt(d2) - r * 1.6) / (r * 0.1), 2.0)) * step(0.0, tc);
              col += gold * ring * m.w * 0.22;
            }
            if (m.x == 4.0) {
              // chakra: a slowly turning lotus ring around the light
              float lotus = exp(-pow((sqrt(d2) - r * 1.45) / (r * 0.14), 2.0)) * step(0.0, tc);
              col += uCol[j].rgb * lotus * uCol[j].a * 0.9;
            }
            if (m.z > 0.5) {
              float sel = exp(-pow((sqrt(d2) - r * (2.0 + 0.25 * sin(uTime * 3.0))) / (r * 0.12), 2.0)) * step(0.0, tc);
              col += vec3(1.0) * sel * 0.5;
            }
          }
          gl_FragColor = vec4(col, 1.0);
        }`,
    });

    const box = new THREE.Mesh(new THREE.BoxGeometry(size.x, size.y, size.z), this.bodyMaterial);
    box.position.copy(center);
    box.renderOrder = 2;
    return box;
  }

  private makeParticles() {
    const pts = samplePointsInside(2200);
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(pts.flat(), 3));
    geo.setAttribute('seed', new THREE.Float32BufferAttribute(pts.map((_, i) => (i * 0.61803) % 1), 1));
    geo.setAttribute('group', new THREE.Float32BufferAttribute(pts.map((q) => GROUPS.indexOf(groupAt(q))), 1));
    this.particleBase = new Float32Array(pts.length * 3).fill(0.9);
    geo.setAttribute('color', new THREE.BufferAttribute(this.particleBase.slice(), 3));
    const mat = new THREE.ShaderMaterial({
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      vertexColors: true,
      uniforms: { uTime: { value: 0 }, uScale: { value: 1 }, uMotion: { value: new THREE.Vector4() }, uMotion2: { value: new THREE.Vector3() } },
      vertexShader: `
        ${motionGlsl()}
        attribute float seed; attribute float group; varying vec3 vColor; varying float vTw; uniform float uTime; uniform float uScale;
        void main() {
          vec3 p = animateFwd(position, group);
          p.y += mod(seed * 0.05 + uTime * 0.012 * (0.5 + seed), 0.05) - 0.025;
          p.x += sin(uTime * 0.7 + seed * 40.0) * 0.004;
          p.z += cos(uTime * 0.6 + seed * 31.0) * 0.004;
          vec4 mv = modelViewMatrix * vec4(p, 1.0);
          vTw = 0.45 + 0.55 * sin(uTime * (1.2 + seed * 2.0) + seed * 60.0);
          vColor = color;
          gl_PointSize = (2.2 + 2.5 * seed) * uScale / -mv.z;
          gl_Position = projectionMatrix * mv;
        }`,
      fragmentShader: `
        varying vec3 vColor; varying float vTw;
        void main() {
          float d = length(gl_PointCoord - 0.5);
          float a = smoothstep(0.5, 0.0, d);
          gl_FragColor = vec4(vColor * a * vTw * 0.32, 1.0);
        }`,
    });
    const points = new THREE.Points(geo, mat);
    points.renderOrder = 3;
    return points;
  }

  /** Meridians and nadis as lines of light, hidden until their node is solicited. */
  private makeChannels() {
    const group = new THREE.Group();
    for (const { zone, points } of [...meridianPaths(), ...nadiPaths()]) {
      const curve = new THREE.CatmullRomCurve3(points.map((p) => new THREE.Vector3(...p)));
      const geo = new LineGeometry();
      const base = curve.getPoints(90).map((v) => [v.x, v.y, v.z] as Vec3);
      geo.setPositions(base.flat());
      const material = new LineMaterial({
        color: 0xffffff,
        linewidth: NADI_COLOR[zone] ? 2.6 : 1.8,
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const line = new Line2(geo, material);
      line.visible = false;
      line.renderOrder = 4;
      group.add(line);
      this.channels.push({ zone, line, material, base, groups: base.map(groupAt) });
    }
    return group;
  }

  private updateChannels(zones: ZoneActivation[]) {
    const byId = new Map(zones.map((z) => [z.zone, z]));
    for (const c of this.channels) {
      const z = byId.get(c.zone);
      const score = z?.score ?? 0;
      c.line.visible = score > 0.02;
      if (!c.line.visible) continue;
      c.material.color.set(NADI_COLOR[c.zone] ?? qualityHex(z?.dominantQuality));
      c.material.opacity = 0.12 + 0.8 * Math.pow(score, 1.4);
    }
  }

  private makeMandala() {
    const mat = new THREE.ShaderMaterial({
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      uniforms: { uTime: { value: 0 } },
      vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
      fragmentShader: `
        varying vec2 vUv; uniform float uTime;
        float ring(float r, float at, float w) { return exp(-pow((r - at) / w, 2.0)); }
        void main() {
          vec2 p = vUv * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x) + uTime * 0.03;
          float rays = smoothstep(0.96, 1.0, cos(a * 12.0)) * step(0.62, r) * step(r, 0.82);
          float petals = smoothstep(0.7, 1.0, cos(a * 24.0)) * ring(r, 0.5, 0.05);
          float c = ring(r, 0.28, 0.01) + ring(r, 0.62, 0.008) + ring(r, 0.82, 0.006) * 0.8 + rays * 0.35 + petals * 0.25;
          c += exp(-r * 6.0) * 0.25;
          gl_FragColor = vec4(vec3(0.3, 0.85, 1.0) * c * 0.45 * (1.0 - smoothstep(0.85, 1.0, r)), 1.0);
        }`,
    });
    const plane = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 1.7), mat);
    plane.rotation.x = -Math.PI / 2;
    plane.position.y = 0.002;
    return plane;
  }

  private makeStars() {
    let s = 11;
    const rnd = () => ((s = (s * 16807) % 2147483647) - 1) / 2147483646;
    const pos: number[] = [];
    for (let i = 0; i < 1400; i++) {
      const u = rnd() * 2 - 1;
      const th = rnd() * Math.PI * 2;
      const r = 30 + rnd() * 10;
      const k = Math.sqrt(1 - u * u);
      pos.push(r * k * Math.cos(th), r * u, r * k * Math.sin(th));
    }
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    return new THREE.Points(geo, new THREE.PointsMaterial({ color: '#cfc6ff', size: 0.06, transparent: true, opacity: 0.7, depthWrite: false }));
  }

  // ---------- data → scene ----------

  private updateScene(zones: ZoneActivation[], selected: string | null, sky: PlacedPoint[], signals: Signal[]) {
    const mat = this.bodyMaterial;
    if (!mat) return;
    const pos = mat.uniforms['uPos'].value as THREE.Vector4[];
    const col = mat.uniforms['uCol'].value as THREE.Vector4[];
    const meta = mat.uniforms['uMeta'].value as THREE.Vector4[];
    const lights: Light[] = [];
    let n = 0;
    let aura = 0;
    const auraColor = new THREE.Color(0, 0, 0);
    let auraWeight = 0;

    // Strongest first, so that the MAX_LIGHTS limit drops the faintest.
    const shown = this.layers();
    for (const z of [...zones].sort((a, b) => b.score - a.score)) {
      if (shown && !shown.includes(z.layer) && z.zone !== selected) continue;
      const style = LAYER_STYLE[z.layer] ?? LAYER_STYLE.soma;
      const c = new THREE.Color(qualityHex(z.dominantQuality));
      if (z.layer === 'systems' || !ANCHORS_3D[z.zone]) {
        // whole-body nodes (systems, Qi, Yin, Yang, fluids) tint the aura
        aura = Math.max(aura, z.score);
        auraColor.add(c.clone().multiplyScalar(z.score));
        auraWeight += z.score;
        continue;
      }
      // Quiet organs keep a faint pearl light, so the body always shows its centres.
      const quiet = z.layer === 'soma' && z.score < 0.03;
      // Contrast curve: the most solicited nodes stand out, the many middling ones fade back.
      const intensity = quiet ? 0.03 : Math.pow(z.score, 2.6) * style.gain;
      if (!quiet && z.score < 0.02) continue;
      for (const a of ANCHORS_3D[z.zone] ?? []) {
        if (n >= MAX_LIGHTS) break;
        pos[n].set(a.p[0], a.p[1], a.p[2], a.r);
        const color = quiet ? new THREE.Color('#e8dcc0') : c;
        col[n].set(color.r, color.g, color.b, intensity);
        meta[n].set(style.kind, (n * 1.7) % 6.28, z.zone === selected ? 1 : 0, z.natalBaseline);
        lights.push({ zone: z.zone, label: `${z.label} · ${Math.round(z.score * 100)}%`, score: z.score, p: new THREE.Vector3(...a.p),
          base: a.p, group: this.groupOf(a.p), slot: n });
        n++;
      }
    }
    mat.uniforms['uCount'].value = n;
    mat.uniforms['uAura'].value = aura;
    (mat.uniforms['uAuraColor'].value as THREE.Color).copy(auraWeight > 0 ? auraColor.multiplyScalar(1 / auraWeight) : new THREE.Color('#d6a84c'));
    this.lights = lights;
    this.tintParticles(lights, zones);
    this.updateChannels(zones);

    // Beams for the sky: transit signals → the nodes their consequences reach.
    const beams: Beam[] = [];
    const transits = new Map(signals.filter((s) => s.kind === 'transit-aspect').map((s) => [s.key, s]));
    for (const z of zones) {
      const anchor = ANCHORS_3D[z.zone]?.[0];
      if (!anchor) continue;
      for (const c of z.contributions) {
        const s = transits.get(c.signalKey);
        if (s) beams.push({ signalKey: s.key, body: s.tags['transitBody'], target: new THREE.Vector3(...anchor.p), quality: c.quality, amount: c.amount });
      }
    }
    beams.sort((a, b) => b.amount - a.amount);
    this.sky3d?.update(sky, signals, beams.slice(0, 40));
  }

  private readonly groupCache = new Map<string, Group>();
  private groupOf(p: Vec3): Group {
    const k = p.join(',');
    let g = this.groupCache.get(k);
    if (!g) this.groupCache.set(k, (g = groupAt(p)));
    return g;
  }

  /** Moves the lights, the channels and (through uniforms) the skin and the particles with the idle animation. */
  private applyMotion(m: Motion, frame: number) {
    const mat = this.bodyMaterial;
    if (!mat) return;
    for (const u of [mat.uniforms, (this.particles?.material as THREE.ShaderMaterial | undefined)?.uniforms]) {
      if (!u) continue;
      (u['uMotion'].value as THREE.Vector4).set(m.swayZ, m.swayX, m.headYaw, m.headPitch);
      (u['uMotion2'].value as THREE.Vector3).set(m.armL, m.armR, m.breath);
    }
    const pos = mat.uniforms['uPos'].value as THREE.Vector4[];
    for (const l of this.lights) {
      const q = animate(l.base, l.group, m);
      l.p.set(q[0], q[1], q[2]);
      pos[l.slot].set(q[0], q[1], q[2], pos[l.slot].w);
    }
    // channels every other frame: they are thin, and rebuilding their geometry is not free
    if (frame % 2 === 0)
      for (const c of this.channels)
        if (c.line.visible) (c.line.geometry as LineGeometry).setPositions(c.base.flatMap((p, i) => animate(p, c.groups[i], m)));
  }

  private frame = 0;

  private tintParticles(lights: Light[], zones: ZoneActivation[]) {
    const pts = this.particles;
    const base = this.particleBase;
    if (!pts || !base) return;
    const pos = pts.geometry.attributes['position'] as THREE.BufferAttribute;
    const colors = pts.geometry.attributes['color'] as THREE.BufferAttribute;
    const active = lights.filter((l) => l.score >= 0.05);
    const tint = active.map((l) => {
      const z = zones.find((x) => x.zone === l.zone)!;
      return { p: l.p, c: new THREE.Color(qualityHex(z.dominantQuality)), w: l.score };
    });
    const v = new THREE.Vector3();
    for (let i = 0; i < pos.count; i++) {
      v.fromBufferAttribute(pos, i);
      let r = 0.55, g = 0.85, b = 1.0, sum = 1;
      for (const t of tint) {
        const k = Math.exp(-v.distanceToSquared(t.p) / 0.012) * t.w * 3;
        r += t.c.r * k; g += t.c.g * k; b += t.c.b * k; sum += k;
      }
      colors.setXYZ(i, r / sum, g / sum, b / sum);
    }
    colors.needsUpdate = true;
  }

  // ---------- loop and interaction ----------

  private loop = () => {
    this.raf = requestAnimationFrame(this.loop);
    const camera = this.camera;
    const controls = this.controls;
    if (!camera || !controls || !this.composer || !this.bodyMaterial) return;
    const t = this.clock.getElapsedTime() * (this.reducedMotion ? 0.2 : 1);

    if (this.flyTo !== null) {
      const dir = camera.position.clone().sub(controls.target);
      const dist = dir.length();
      const next = dist + (this.flyTo - dist) * 0.06;
      camera.position.copy(controls.target).add(dir.setLength(next));
      if (Math.abs(next - this.flyTo) < 0.02) this.flyTo = null;
    }
    this.applyMotion(this.reducedMotion ? STILL : motionAt(t), this.frame++);
    controls.autoRotate = !this.reducedMotion && t - this.idleSince > 6;
    controls.update();

    const dist = camera.position.distanceTo(controls.target);
    this.sky3d?.setOpacity(THREE.MathUtils.smoothstep(dist, 3.6, 7.5));
    this.sky3d?.tick(t);
    this.bodyMaterial.uniforms['uTime'].value = t;
    const pm = this.particles?.material as THREE.ShaderMaterial | undefined;
    if (pm) {
      pm.uniforms['uTime'].value = t;
      pm.uniforms['uScale'].value = (this.renderer?.getPixelRatio() ?? 1) * 1.6;
    }
    const v = dist > 6 ? 'sky' : 'body';
    if (this.view() !== v && this.flyTo === null) this.view.set(v);
    this.composer.render();
  };

  protected go(view: 'body' | 'sky') {
    this.view.set(view);
    this.flyTo = view === 'sky' ? SKY_DISTANCE : BODY_DISTANCE;
  }

  private nearest(ev: PointerEvent | MouseEvent): { light?: Light; label: string; x: number; y: number } | null {
    const canvas = this.canvasRef()?.nativeElement;
    const camera = this.camera;
    if (!canvas || !camera) return null;
    const rect = canvas.getBoundingClientRect();
    const mx = ev.clientX - rect.left;
    const my = ev.clientY - rect.top;
    const project = (p: THREE.Vector3) => {
      const s = p.clone().project(camera);
      return { x: ((s.x + 1) / 2) * rect.width, y: ((1 - s.y) / 2) * rect.height, z: s.z };
    };
    let best: { light?: Light; label: string; x: number; y: number; d: number } | null = null;
    for (const l of this.lights) {
      const s = project(l.p);
      const d = Math.hypot(s.x - mx, s.y - my);
      // prefer the brightest when lights overlap
      const score = d - l.score * 8;
      if (s.z < 1 && d < 26 && (!best || score < best.d)) best = { light: l, label: l.label, x: s.x, y: s.y, d: score };
    }
    if (!best && this.view() === 'sky') {
      for (const p of this.sky3d?.pickables ?? []) {
        const s = project(p.world);
        const d = Math.hypot(s.x - mx, s.y - my);
        if (s.z < 1 && d < 22 && (!best || d < best.d)) best = { label: p.label, x: s.x, y: s.y, d };
      }
    }
    return best;
  }

  protected onMove(ev: PointerEvent) {
    if (ev.buttons) return;
    const n = this.nearest(ev);
    this.hover.set(n ? { label: n.label, x: n.x, y: n.y } : null);
    const canvas = this.canvasRef()?.nativeElement;
    if (canvas) canvas.style.cursor = n?.light ? 'pointer' : '';
  }

  protected onClick(ev: MouseEvent) {
    const n = this.nearest(ev);
    if (n?.light) this.pick.emit(n.light.zone);
  }

  ngOnDestroy() {
    cancelAnimationFrame(this.raf);
    this.resize?.disconnect();
    this.controls?.dispose();
    this.sky3d?.dispose();
    this.composer?.dispose();
    this.renderer?.dispose();
  }
}
