import { Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { ConsensusStore } from '../core/consensus-store';
import { DiaryHint, DiaryHints } from '../core/diary-hints';
import { ReadingResponse, TimelineResponse } from '../core/models';
import { BodyMap } from '../components/body-map';
import { Icon } from '../components/icon';
import { Body3d } from '../components/body-3d/body-3d';
import { ChainPanel, PanelTab } from '../components/now/chain-panel';
import { SchoolPicker } from '../components/school-picker';
import { FlowRef, parseRef, refOf } from '../core/flow-graph';
import { BodyLayer } from '../core/models';
import { LayerPanel } from '../components/now/layer-panel';

type Sheet = 'layers' | 'chain' | null;

/**
 * "Adesso": the body of light and its sky fill the screen; two floating panels read it. On the left, the body by
 * layers; on the right, the chains from influences to consequences (expandable to the whole screen). A floating card
 * brings forward what the person's own journal says about a signal active now. On a phone the panels are sheets.
 */
@Component({
  selector: 'app-now',
  imports: [Body3d, BodyMap, RouterLink, Icon, ChainPanel, LayerPanel, SchoolPicker, MatButtonModule, MatTooltipModule],
  template: `
    @if (!store.current()) {
      <section class="empty">
        <h2>Nessun profilo</h2>
        <p>Inserisci data, ora e luogo di nascita per calcolare il tema natale.</p>
        <a routerLink="/profilo" matButton="filled">Crea il profilo</a>
      </section>
    } @else {
      @if (data(); as d) {
        <div class="stage" [class.dim]="expanded()">
          <app-body-3d [zones]="d.reading.zones" [sky]="d.reading.sky" [signals]="d.reading.signals"
                       [selected]="zone()" [layers]="visibleLayers()" (pick)="pickZone($event)">
            <div class="fallback">
              <app-body-map [zones]="d.reading.zones" [selected]="zone()" (pick)="pickZone($event)" />
            </div>
          </app-body-3d>
        </div>

        <div class="schools-widget"><app-school-picker /></div>

        <div class="timebar glass" [class.loading]="loading()">
          <button class="t-btn" (click)="shift(-24)" matTooltip="un giorno prima">«</button>
          <button class="t-btn" (click)="shift(-3)" matTooltip="tre ore prima">‹</button>
          <label class="when-pill">
            <span class="when-text">{{ when() }}</span>
            <input type="datetime-local" [value]="localValue()" (change)="setLocal($any($event.target).value)" aria-label="Momento" />
          </label>
          <button class="t-btn" (click)="shift(3)" matTooltip="tre ore dopo">›</button>
          <button class="t-btn" (click)="shift(24)" matTooltip="un giorno dopo">»</button>
          @if (at() !== null) { <button class="t-now" (click)="setAt(null)">adesso</button> }
        </div>

        <aside class="panel-left glass" [class.open]="sheet() === 'layers'" [class.hidden]="expanded()">
          <button class="sheet-close" (click)="nav({ sheet: null })" aria-label="Chiudi"><app-icon name="close" [size]="18" /></button>
          <app-layer-panel [zones]="d.reading.zones" [selected]="zone()" (pick)="pickZone($event)"
                           [layer]="layer()" [isolate]="isolate()"
                           (layerChange)="nav({ layer: $event }, true)" (isolateChange)="nav({ iso: $event ? '1' : null }, true)" />
        </aside>

        <aside class="panel-right glass" [class.open]="sheet() === 'chain'" [class.full]="expanded()" [class.narrow]="!expanded()">
          <button class="sheet-close" (click)="nav({ sheet: null })" aria-label="Chiudi"><app-icon name="close" [size]="18" /></button>
          <app-chain-panel [reading]="d.reading" [sources]="d.sources" [hints]="hints()" [selRef]="sel()" [tab]="tab()" [timeline]="timeline()" [flowView]="flowView()"
                           [expanded]="expanded()" [showPositions]="positions()" [profileId]="store.current()!.id" [consensus]="consensus.mode()"
                           (navigate)="panelNav($event)" (back)="back()" (fullscreen)="nav({ full: $event ? '1' : null })" />
        </aside>

        @if (toast(); as h) {
          <div class="toast glass" [class.hidden]="expanded()">
            <button class="toast-main" (click)="openHint(h)">
              <app-icon name="hint" [size]="18" />
              <span><b>{{ h.outcome }}</b><small>{{ h.now }}</small></span>
            </button>
            <button class="toast-x" (click)="dismissed.set(true)" aria-label="Chiudi"><app-icon name="close" [size]="15" /></button>
          </div>
        }

        <nav class="dock glass" aria-label="Pannelli">
          <button [class.on]="sheet() === 'layers'" (click)="toggleSheet('layers')"><app-icon name="layers" [size]="20" /> Strati</button>
          <button [class.on]="sheet() === 'chain'" (click)="toggleSheet('chain')"><app-icon name="flow" [size]="20" /> Catene</button>
        </nav>

        @if (error()) { <p class="error-float glass">{{ error() }}</p> }
      } @else if (error()) {
        <p class="error-float glass">{{ error() }}</p>
      } @else {
        <p class="opening">Il cielo si apre…</p>
      }
    }
  `,
  styles: `
    :host { position: fixed; top: var(--topbar-h, 64px); left: 0; right: 0; bottom: 0; overflow: hidden; z-index: 1;
      --edge: 16px; --panel-top: 74px; }
    .empty { max-width: 560px; margin: 10vh auto; padding: 24px; text-align: center; }
    .opening { position: absolute; inset: 0; display: grid; place-items: center; margin: 0; font-family: var(--font-brand);
      letter-spacing: .2em; color: var(--gold); }

    .stage { position: absolute; inset: 0; transition: filter .4s; }
    .stage::after { content: ''; position: absolute; inset: 0; pointer-events: none;
      background: radial-gradient(ellipse at 50% 45%, transparent 45%, rgba(5, 4, 14, .55) 100%); }
    .stage.dim { filter: blur(3px) brightness(.55); }
    .fallback { height: 100%; overflow: auto; padding: 90px 16px 16px; }

    .glass { background: color-mix(in srgb, var(--mat-sys-surface-container-low) 62%, transparent);
      backdrop-filter: blur(16px) saturate(1.2); -webkit-backdrop-filter: blur(16px) saturate(1.2);
      border: 1px solid color-mix(in srgb, var(--gold) 22%, transparent);
      box-shadow: 0 18px 50px rgba(0, 0, 0, .35); }

    .schools-widget { position: absolute; top: 14px; left: var(--edge); z-index: 6; }
    .timebar { position: absolute; top: 14px; left: 50%; transform: translateX(-50%); z-index: 3; display: flex; align-items: center;
      gap: 2px; padding: 4px 6px; border-radius: 999px; transition: opacity .2s; }
    .timebar.loading { opacity: .7; }
    .t-btn, .t-now { border: 0; background: transparent; color: var(--muted); font: inherit; font-size: 16px; width: 32px; height: 32px;
      border-radius: 50%; cursor: pointer; }
    .t-btn:hover, .t-now:hover { color: var(--gold); }
    .t-now { width: auto; padding: 0 12px; font-size: 12px; border-radius: 999px; color: var(--gold); letter-spacing: .08em; }
    .when-pill { position: relative; padding: 0 10px; cursor: pointer; }
    .when-text { font-family: var(--font-brand); letter-spacing: .12em; font-size: 13px; color: var(--text); white-space: nowrap; }
    .when-pill input { position: absolute; inset: 0; opacity: 0; cursor: pointer; width: 100%; }

    .panel-left, .panel-right { position: absolute; top: var(--panel-top); bottom: var(--edge); z-index: 2; border-radius: 22px;
      padding: 14px; display: flex; flex-direction: column; min-height: 0;
      transition: opacity .3s, transform .35s cubic-bezier(.2, .7, .2, 1), width .35s; }
    .panel-left { left: var(--edge); width: 268px; }
    .panel-left > app-layer-panel { flex: 1; min-height: 0; }
    .panel-right { right: var(--edge); width: min(460px, 36vw); }
    .panel-right > app-chain-panel { flex: 1; min-height: 0; }
    .panel-right.full { width: min(1100px, calc(100% - 2 * var(--edge))); right: 50%; transform: translateX(50%); }
    .hidden { opacity: 0; pointer-events: none; }
    .sheet-close { display: none; }

    .toast { position: absolute; left: 50%; bottom: 70px; transform: translateX(-50%); z-index: 3; display: flex; align-items: center;
      border-radius: 18px; padding: 4px 4px 4px 12px; width: min(460px, calc(100% - 820px)); min-width: 280px;
      border-color: color-mix(in srgb, var(--gold) 50%, transparent); animation: rise .6s ease-out; }
    .toast-main { flex: 1; display: flex; align-items: center; gap: 10px; border: 0; background: transparent; color: var(--text);
      font: inherit; text-align: left; cursor: pointer; padding: 6px 0; min-width: 0; }
    .toast-main app-icon { color: var(--gold); flex: none; }
    .toast-main b { display: block; font-family: var(--font-brand); font-weight: 400; color: var(--gold); font-size: 14px; }
    .toast-main small { display: block; font-size: 12px; color: var(--muted); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .toast-x { border: 0; background: transparent; color: var(--muted); width: 30px; height: 30px; border-radius: 50%; cursor: pointer; flex: none; }
    @keyframes rise { from { opacity: 0; transform: translate(-50%, 12px); } }

    .dock { display: none; }
    .error-float { position: absolute; top: 64px; left: 50%; transform: translateX(-50%); z-index: 4; padding: 8px 16px;
      border-radius: 12px; color: var(--mat-sys-error); }

    @media (max-width: 1180px) {
      .panel-left { width: 226px; }
      .toast { width: auto; max-width: calc(100% - 32px); left: var(--edge); right: var(--edge); transform: none; bottom: 64px; }
      @keyframes rise { from { opacity: 0; transform: translateY(12px); } }
    }

    /* Phone and narrow tablet: the body fills the screen, the panels are sheets from the bottom. */
    @media (max-width: 860px) {
      :host { --stage-toggle-bottom: 84px; }
      .timebar { top: 58px; max-width: calc(100% - 20px); }
      .schools-widget { top: 10px; left: 50%; transform: translateX(-50%); }
      .when-text { font-size: 11.5px; letter-spacing: .06em; }
      .panel-left, .panel-right, .panel-right.full { left: 0; right: 0; width: auto; max-width: none; top: auto; bottom: 0; height: 72vh;
        border-radius: 22px 22px 0 0; transform: translateY(105%); opacity: 1; pointer-events: none; padding-top: 18px; }
      .panel-left.open, .panel-right.open { transform: none; pointer-events: auto; z-index: 5; }
      .panel-right.full.open { height: calc(100% - 10px); }
      .sheet-close { display: grid; place-items: center; position: absolute; top: 8px; right: 10px; z-index: 1; width: 32px; height: 32px;
        border: 0; border-radius: 50%; background: transparent; color: var(--muted); }
      .panel-left .sheet-close { right: 54px; }
      .panel-right.open { padding-top: 46px; }
      .panel-right .sheet-close { top: 8px; right: 10px; }
      .stage.dim { filter: none; }
      .toast { bottom: 132px; }
      .dock { display: flex; position: absolute; left: 50%; bottom: 14px; transform: translateX(-50%); z-index: 4; gap: 4px;
        padding: 4px; border-radius: 999px; }
      .dock button { border: 0; background: transparent; color: var(--muted); font: inherit; font-size: 13px; display: flex;
        align-items: center; gap: 6px; padding: 8px 14px; border-radius: 999px; cursor: pointer; }
      .dock button.on { color: var(--text); background: color-mix(in srgb, var(--gold) 16%, transparent); }
    }
  `,
})
export class Now {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  protected readonly store = inject(ProfileStore);
  protected readonly consensus = inject(ConsensusStore);
  private readonly diary = inject(DiaryHints);

  // ---------- state, all in the URL (deep links; the browser's back = the panel's back) ----------

  private readonly params = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });

  /** null = live "now". */
  protected readonly at = computed(() => {
    const v = this.params().get('at');
    const d = v ? new Date(v) : null;
    return d && !isNaN(d.getTime()) ? d : null;
  });
  protected readonly sel = computed(() => (parseRef(this.params().get('sel')) ? (this.params().get('sel') as FlowRef) : null));
  protected readonly tab = computed(() => (this.params().get('tab') as PanelTab) || 'now');
  protected readonly flowView = computed(() => (this.params().get('fv') === 'tree' ? 'tree' : 'diagram'));
  protected readonly layer = computed(() => (this.params().get('layer') as BodyLayer) || 'soma');
  protected readonly isolate = computed(() => this.params().get('iso') === '1');
  protected readonly expanded = computed(() => this.params().get('full') === '1');
  protected readonly sheet = computed(() => this.params().get('sheet') as Sheet);
  protected readonly positions = computed(() => this.params().get('pos') === '1');
  protected readonly zone = computed(() => {
    const s = parseRef(this.sel());
    return s?.kind === 'node' ? s.id : null;
  });
  protected readonly visibleLayers = computed(() => (this.isolate() ? [this.layer()] : null));

  /** Panel navigations pushed in this visit: back goes through history while there are some. */
  private depth = 0;

  protected readonly data = signal<ReadingResponse | null>(null);
  /** The ±48 h around the moment: when each consequence and cause starts and ends. */
  protected readonly timeline = signal<TimelineResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly dismissed = signal(false);

  protected readonly when = computed(() =>
    new Date(this.data()?.reading.at ?? Date.now()).toLocaleString('it-IT', { dateStyle: 'medium', timeStyle: 'short' }),
  );
  protected readonly localValue = computed(() => toLocalInput(this.at() ?? new Date()));

  protected readonly hints = computed(() => {
    const d = this.data();
    this.diary.findings();
    return d ? this.diary.forReading(d.reading) : [];
  });
  protected readonly toast = computed(() => (this.dismissed() ? null : this.hints()[0] ?? null));

  constructor() {
    effect(() => {
      const p = this.store.current();
      const at = this.at();
      const consensus = this.consensus.mode();
      if (p) {
        void this.load(p.id, at, consensus);
        void this.diary.load(p.id, consensus);
      }
    });
  }

  private async load(id: number, at: Date | null, consensus: string) {
    this.loading.set(true);
    this.error.set(null);
    try {
      const r = await this.api.reading(id, at?.toISOString(), consensus);
      this.data.set(r);
      this.dismissed.set(false);
      void this.loadTimeline(id, new Date(r.reading.at), consensus);
    } catch (e) {
      this.error.set('Impossibile calcolare la lettura. Il backend è avviato?');
      console.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  private timelineKey = '';
  private async loadTimeline(id: number, at: Date, consensus: string) {
    // the live "now" moves every reading: refresh the window only when it moved by an hour or more
    const key = `${id}|${consensus}|${Math.floor(at.getTime() / 3600_000)}`;
    if (key === this.timelineKey) return;
    this.timelineKey = key;
    this.timeline.set(null);
    try {
      const h = 48 * 3600_000;
      this.timeline.set(await this.api.timeline(id, new Date(at.getTime() - h).toISOString(), new Date(at.getTime() + h).toISOString(), 3, consensus));
    } catch (e) {
      console.error('Finestra temporale non disponibile', e);
    }
  }

  /** Changes the URL; replace = no history entry (small toggles). */
  protected nav(changes: Record<string, string | null>, replace = false) {
    if (!replace) this.depth++;
    void this.router.navigate([], { queryParams: changes, queryParamsHandling: 'merge', replaceUrl: replace });
  }

  protected panelNav(e: { sel?: FlowRef | null; tab?: PanelTab; positions?: boolean; flowView?: 'diagram' | 'tree' }) {
    const changes: Record<string, string | null> = {};
    if (e.flowView !== undefined) { this.nav({ fv: e.flowView === 'tree' ? 'tree' : null }, true); return; }
    if (e.sel !== undefined) changes['sel'] = e.sel;
    if (e.tab !== undefined) changes['tab'] = e.tab === 'now' ? null : e.tab;
    if (e.positions !== undefined) { this.nav({ pos: e.positions ? '1' : null }, true); return; }
    this.nav(changes);
  }

  protected back() {
    if (this.depth > 0) {
      this.depth--;
      this.location.back();
    } else {
      this.nav({ sel: null }, true);
    }
  }

  protected pickZone(id: string) {
    const mobile = matchMedia('(max-width: 860px)').matches;
    this.nav({ sel: refOf('node', id), ...(mobile ? { sheet: 'chain' } : {}) });
  }

  protected openHint(h: DiaryHint) {
    this.nav({ sel: h.target.kind === 'zone' ? refOf('node', h.target.id) : refOf('signal', h.target.id), sheet: 'chain' });
  }

  protected toggleSheet(s: Sheet) {
    this.nav({ sheet: this.sheet() === s ? null : s });
  }

  protected setAt(d: Date | null) {
    this.nav({ at: d ? d.toISOString() : null });
  }

  protected shift(hours: number) {
    const base = this.at() ?? new Date();
    this.setAt(new Date(base.getTime() + hours * 3600_000));
  }

  protected setLocal(v: string) {
    if (v) this.setAt(new Date(v));
  }
}

function toLocalInput(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
