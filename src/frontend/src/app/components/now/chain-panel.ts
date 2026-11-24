import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CatalogStore } from '../../core/catalog-store';
import { DiaryHint } from '../../core/diary-hints';
import { FlowRef, buildFlow, familiesOf, parseRef, refOf, theoriesInCatalog, theoriesNow } from '../../core/flow-graph';
import { CatalogRule, Reading, Signal, SourceStatus, TimelineResponse, ZoneActivation } from '../../core/models';
import { Episode, episodeAt, when, windowText } from '../../core/episodes';
import { QUALITY_NAME, qualityColor } from '../../core/symbols';
import { Icon } from '../icon';
import { SkyWheel } from '../sky-wheel';
import { CauseTree } from './cause-tree';
import { FlowDiagram } from './flow-diagram';
import { TimeTrace } from './time-trace';

export type PanelTab = 'now' | 'flow' | 'theories' | 'influences' | 'sky';

/** Positions and clocks: facts, shown in the list of influences only on request. */
const POSITION_KINDS = new Set(['placement', 'decan', 'sidereal-placement', 'solar-hour']);

const TABS: { id: PanelTab; label: string; icon: string }[] = [
  { id: 'now', label: 'Ora', icon: 'schedule' },
  { id: 'flow', label: 'Flusso', icon: 'flow' },
  { id: 'theories', label: 'Teorie', icon: 'layers' },
  { id: 'influences', label: 'Influenze', icon: 'facts' },
  { id: 'sky', label: 'Cielo', icon: 'sky' },
];

/**
 * The right-hand panel of Adesso: cause → effect. Tabs show the whole picture (a flow, a tree, the theories several
 * traditions share, every active influence, the sky); a selection (from the URL) opens the detail of one thing:
 * a consequence with its causes and its course in time, a rule with its source and what it is doing now, a signal and
 * where it lands, an influence and its signals. Navigation goes through the URL, so the browser's back works.
 */
@Component({
  selector: 'app-chain-panel',
  imports: [Icon, MatTooltipModule, SkyWheel, FlowDiagram, CauseTree, TimeTrace],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="panel-head">
      <div class="panel-head-top">
        <span class="panel-title">Cause ed effetti</span>
        <button class="full-btn" (click)="fullscreen.emit(!expanded())">
          <app-icon [name]="expanded() ? 'collapse' : 'expand'" [size]="16" />
          <span>{{ expanded() ? 'Riduci' : 'Tutto schermo' }}</span>
        </button>
      </div>
      <nav class="tabs" role="tablist">
        @for (t of tabs; track t.id) {
          <button role="tab" [class.on]="!sel() && tab() === t.id" (click)="go({ tab: t.id, sel: null })">
            <app-icon [name]="t.icon" [size]="20" />
            <span class="t-label">{{ t.label }}</span>
          </button>
        }
      </nav>
    </header>

    <div class="body">
      @if (sel(); as s) {
        <button class="back" (click)="back.emit()"><app-icon name="back" [size]="15" /> indietro</button>

        @if (s.kind === 'node' && nodeView(); as n) {
          <section class="hero t-node" [style.--q]="color(n.z.dominantQuality)">
            <span class="type"><span class="shape node-shape"></span> conseguenza · {{ n.layer }}</span>
            <h2 class="title">{{ n.z.label }}</h2>
            <div class="stat"><span class="big mono">{{ pct(n.z.score) }}</span><span>più sollecitato che nel {{ pct(n.z.score) }} dei tuoi momenti.
              Sopra il 50% vuol dire «più del solito» per te, sopra il 90% è raro.</span></div>
            @if (n.description) { <p class="desc">{{ n.description }}</p> }
            @if (n.z.natalBaseline > 0.05) { <p class="note">Il tuo tema lo rende sensibile in generale ({{ pct(n.z.natalBaseline) }}).</p> }
            @if (n.window) { <p class="c-when big-when"><app-icon name="schedule" [size]="14" /> {{ n.window }}</p> }
            @if (n.agreement; as a) { <p class="agree" [class.cross]="a.families > 1">{{ a.text }}</p> }
          </section>
          <h3 class="sec">Le cause</h3>
          <ul class="causes">
            @for (k of n.allCauses; track k.key) {
              <li>
                <button class="cause" (click)="openRef(signalRef(k.key))">
                  <span class="shape influence-shape small" [matTooltip]="k.influence"></span>
                  <span class="k-text"><span class="k-label">{{ k.label }}</span>
                    <span class="k-detail">{{ k.influence }} · {{ k.detail }}@if (k.window) { · attivo {{ k.window }} }</span></span>
                </button>
                <span class="via">via
                  @for (r of k.rules; track r.ref) {
                    <button class="rule-link" (click)="openRef(r.ref)"><span class="shape rule-shape small"></span> {{ r.label }}</button>
                  }
                </span>
              </li>
            }
          </ul>
          <h3 class="sec">Nel tempo</h3>
          <app-time-trace [timeline]="timeline()" [at]="reading().at" [zone]="n.z.zone" [zoneLabel]="n.z.label" [causes]="n.causes" />
          <h3 class="sec">Da dove arriva</h3>
          @if (expanded()) { <app-flow-diagram [graph]="focusGraph()" [focus]="selRef()" (open)="openRef($event)" /> }
          @else { <app-cause-tree [graph]="focusGraph()" (pick)="openRef($event)" /> }
          <h3 class="sec">Per scuola</h3>
          <ul class="schools">
            @for (s of n.schools; track s.id) {
              <li [class.silent]="s.score <= 0"><app-icon [name]="schoolIcon(s.id)" [size]="15" /> {{ schoolName(s.id) }}
                <small>{{ family(s.id) }}</small><span class="mono">{{ pct(s.score) }}</span></li>
            }
          </ul>
        }

        @if (s.kind === 'rule' && ruleView(); as r) {
          <section class="hero t-rule">
            <span class="type"><span class="shape rule-shape"></span> regola · <app-icon [name]="r.school.icon" [size]="13" /> {{ r.school.name }}</span>
            <h2 class="title">{{ r.rule?.name || r.ruleId }}</h2>
            @if (r.rule?.about) { <p class="desc">{{ r.rule?.about }}</p> }
            @if (r.rule?.source) { <p class="source"><span>fonte</span> {{ r.rule?.source }}</p> }
            <span class="status" [class.on]="r.active">{{ r.active ? 'attiva ora' : 'silenziosa ora' }}</span>
            @if (r.rule; as rule) {
              <dl class="facts">
                <dt>legge</dt><dd>{{ rule.kindLabel }}@for (c of rule.conditions; track c) { · {{ c }} }</dd>
                @if (rule.minIntensity > 0) { <dt>soglia</dt><dd>intensità ≥ {{ pct(rule.minIntensity) }}</dd> }
                <dt>scrive su</dt><dd>{{ zonesText(rule) }}</dd>
                <dt>peso</dt><dd class="mono">{{ rule.weight }}@if (rule.qualityLabel) { · {{ rule.qualityLabel }} }</dd>
              </dl>
            }
          </section>
          @if (!r.schoolActive) { <p class="note">La scuola è spenta: la regola non viene valutata.</p> }
          @if (r.active) {
            <h3 class="sec">Cosa sta generando</h3>
            @if (expanded()) { <app-flow-diagram [graph]="focusGraph()" [focus]="selRef()" (open)="openRef($event)" /> }
            @else { <app-cause-tree [graph]="focusGraph()" (pick)="openRef($event)" /> }
          }
          @if (r.candidates.length) {
            <h3 class="sec">Segnali che potrebbe leggere, ora</h3>
            <ul class="checks">
              @for (c of r.candidates; track c.signal.key) {
                <li [class.miss]="!c.ok">
                  <span class="mark">{{ c.ok ? '✓' : '✗' }}</span>
                  <button class="pill t-signal" (click)="openRef(signalRef(c.signal.key))">{{ c.signal.label }}</button>
                  <span class="why">{{ c.why }}</span>
                </li>
              }
            </ul>
          } @else if (r.rule && r.rule.kind !== 'node') {
            <p class="note">Nessun segnale «{{ r.rule.kindLabel }}» è attivo adesso: per questo la regola tace.</p>
          }
        }

        @if (s.kind === 'signal' && signalView(); as v) {
          <section class="hero t-signal">
            <span class="type"><span class="shape signal-shape"></span> segnale
              @if (v.influence) { · <button class="link" (click)="openRef(influenceRef(v.influence.id))">{{ v.influence.name }}</button> }</span>
            <h2 class="title">{{ v.label }}</h2>
            <p class="desc">{{ v.detail }}</p>
            @if (v.window) { <p class="c-when big-when"><app-icon name="schedule" [size]="14" /> attivo {{ v.window }}</p> }
            <div class="stat"><span class="big mono sig">{{ pct(v.intensity) }}</span>
              <span>intensità@if (v.value !== null) { · valore <b class="mono">{{ v.value }} {{ v.unit }}</b> }</span></div>
          </section>
          @for (h of v.hints; track h.finding.metric) {
            <div class="dhint static"><app-icon name="hint" [size]="15" /><span><b>{{ h.outcome }}</b><small>{{ h.evidence }}</small></span></div>
          }
          @if (v.reads) {
            <h3 class="sec">Dove arriva</h3>
            @if (expanded()) { <app-flow-diagram [graph]="focusGraph()" [focus]="selRef()" (open)="openRef($event)" /> }
            @else { <app-cause-tree [graph]="focusGraph()" (pick)="openRef($event)" /> }
          } @else {
            <p class="note">Nessuna scuola attiva lo trasforma in una conseguenza: è un fatto osservato e basta.</p>
          }
        }

        @if (s.kind === 'influence' && influenceView(); as i) {
          <section class="hero t-influence">
            <span class="type"><span class="shape influence-shape"></span> influenza · {{ i.info.providerName }}</span>
            <h2 class="title"><app-icon [name]="i.info.icon" [size]="20" /> {{ i.info.name }}</h2>
            <p class="desc">{{ i.info.description }}</p>
            <p class="note">{{ i.info.reliability === 'derived' ? 'Fonte derivata' : 'Fonte primaria' }} · {{ i.info.license }}
              @if (i.status; as st) { · {{ st.ok ? st.samples + ' campioni' : 'errore: ' + st.error }} }</p>
          </section>
          <h3 class="sec">I suoi segnali, ora <small>{{ i.signals.length }}</small></h3>
          <ul class="sigs">
            @for (x of i.signals; track x.key) {
              <li><button class="sig-row" (click)="openRef(signalRef(x.key))">
                <span class="data mono">{{ pct(x.intensity) }}</span>
                <span class="sr"><span class="l1">{{ x.label }}</span><span class="l2">{{ x.detail }}</span></span>
              </button></li>
            }
          </ul>
        }
      } @else {
        @switch (tab()) {
          @case ('now') {
            @if (hints().length) {
              <h3 class="sec"><app-icon name="hint" [size]="14" /> Dal tuo diario, ora</h3>
              @for (h of hints(); track h.finding.signalKey + h.finding.metric) {
                <button class="dhint" (click)="openHint(h)"><app-icon name="hint" [size]="15" />
                  <span><b>{{ h.outcome }}</b><small>{{ h.now }} — {{ h.evidence }}</small></span></button>
              }
            }
            <h3 class="sec">Le conseguenze più forti, adesso</h3>
            @for (c of cards(); track c.z.zone) {
              <article class="card" [style.--q]="color(c.z.dominantQuality)">
                <button class="card-head" (click)="openRef(nodeRef(c.z.zone))">
                  <span class="shape node-shape"></span>
                  <span class="c-name">{{ c.z.label }}</span>
                  <small class="c-layer">{{ c.layer }}</small>
                  <span class="c-pct mono">{{ pct(c.z.score) }}</span>
                </button>
                @if (c.window) { <p class="c-when"><app-icon name="schedule" [size]="13" /> {{ c.window }}</p> }
                <ul class="causes">
                  @for (k of c.causes; track k.key) {
                    <li>
                      <button class="cause" (click)="openRef(signalRef(k.key))">
                        <span class="shape influence-shape small" [matTooltip]="k.influence"></span>
                        <span class="k-text"><span class="k-label">{{ k.label }}</span>
                          <span class="k-detail">{{ k.influence }} · {{ k.detail }}@if (k.window) { · attivo {{ k.window }} }</span></span>
                      </button>
                      <span class="via">via
                        @for (r of k.rules; track r.ref) {
                          <button class="rule-link" (click)="openRef(r.ref)"><span class="shape rule-shape small"></span> {{ r.label }}</button>
                        }
                      </span>
                    </li>
                  }
                </ul>
              </article>
            } @empty { <p class="note">Nessuna conseguenza sopra lo zero, adesso.</p> }
            <h3 class="sec">I segnali che influiscono di più</h3>
            <ol class="rank">
              @for (r of ranking(); track r.key) {
                <li><button class="rank-row" (click)="openRef(signalRef(r.key))">
                  <span class="r-n mono">{{ $index + 1 }}</span>
                  <span class="r-main">
                    <span class="r-label"><span class="shape signal-shape small"></span> {{ r.label }}</span>
                    <span class="r-sub">{{ r.influence }} · {{ r.detail }}@if (r.window) { · {{ r.window }} }</span>
                    <span class="r-nodes">tocca {{ r.nodes.length }} {{ r.nodes.length === 1 ? 'parte' : 'parti' }}: {{ r.nodes.slice(0, 5).join(', ') }}@if (r.nodes.length > 5) { … }</span>
                  </span>
                  <span class="r-amt mono">{{ r.amount.toFixed(2) }}</span>
                </button></li>
              }
            </ol>
          }
          @case ('flow') {
            <div class="seg">
              <button [class.on]="flowView() === 'diagram'" (click)="navigate.emit({ flowView: 'diagram' })">Diagramma</button>
              <button [class.on]="flowView() === 'tree'" (click)="navigate.emit({ flowView: 'tree' })">Albero</button>
            </div>
            @if (flowView() === 'diagram') {
              <p class="lead">Da sinistra a destra: le <b class="c-inf">influenze</b> producono <b class="c-sig">segnali</b>, le
                <b class="c-rule">regole</b> delle scuole li leggono e li portano sulle <b class="c-node">parti del corpo</b>, a destra.
                Le bande sono larghe quanto il contributo; passa sopra una voce per vedere la sua catena.</p>
              <app-flow-diagram [graph]="overview()" (open)="openRef($event)" />
            } @else {
              <p class="lead">Ogni catena dalla causa all'effetto: <b class="c-inf">influenza</b> ▸ <b class="c-sig">segnale</b> ▸
                <b class="c-rule">regola</b> ▸ <b class="c-node">parte del corpo</b>.</p>
              <app-cause-tree [graph]="overview()" (pick)="openRef($event)" />
            }
          }
          @case ('theories') {
            <p class="lead">Dove tradizioni di famiglie diverse, partendo dalla stessa causa, arrivano alla stessa parte del corpo:
              teorie comuni, non la stessa idea ripetuta.</p>
            <h3 class="sec">Ora, nei segnali attivi <small>{{ nowTheories().length }}</small></h3>
            @for (t of nowTheories().slice(0, 30); track t.cause + t.node) {
              <div class="theory">
                <button class="pill t-signal" (click)="t.causeRef && openRef(t.causeRef)">{{ t.cause }}</button>
                <span class="arr">→</span>
                <button class="pill t-node" (click)="openRef(t.nodeRef)">{{ t.node }}</button>
                <span class="fam"><b class="mono">{{ t.families.length }}</b> famiglie: {{ t.families.join(', ') }}</span>
                <span class="sch">{{ t.schools.join(' · ') }}</span>
              </div>
            } @empty { <p class="note">Nessuna convergenza tra famiglie diverse, adesso.</p> }
            <h3 class="sec">Nel catalogo delle scuole <small>{{ catalogTheories().length }}</small></h3>
            <p class="note">Simboli (pianeti, segni) che scuole di famiglie diverse legano alla stessa parte del corpo.</p>
            @for (t of catalogTheories().slice(0, 60); track t.cause + t.node) {
              <div class="theory">
                <span class="pill t-sym">{{ t.cause }}</span><span class="arr">→</span>
                <button class="pill t-node" (click)="openRef(t.nodeRef)">{{ t.node }}</button>
                <span class="fam"><b class="mono">{{ t.families.length }}</b> famiglie: {{ t.families.join(', ') }}</span>
              </div>
            }
          }
          @case ('influences') {
            @if (hints().length) {
              <h3 class="sec"><app-icon name="hint" [size]="14" /> Dal tuo diario, ora</h3>
              @for (h of hints(); track h.finding.signalKey + h.finding.metric) {
                <button class="dhint" (click)="openHint(h)"><app-icon name="hint" [size]="15" />
                  <span><b>{{ h.outcome }}</b><small>{{ h.now }} — {{ h.evidence }}</small></span></button>
              }
            }
            <label class="all"><input type="checkbox" [checked]="showPositions()" (change)="togglePositions()" />
              mostra anche posizioni e orologi</label>
            @for (g of sourceGroups(); track g.id) {
              <section class="src">
                <button class="src-head" (click)="openRef(influenceRef(g.id))">
                  <span class="shape influence-shape small"></span>
                  <app-icon [name]="g.icon" [size]="16" /><span class="name">{{ g.name }}</span>
                  @if (g.derived) { <span class="badge-derived">derivata</span> }
                  <span class="state mono" [class.bad]="!g.ok">{{ g.ok ? g.signals.length : 'errore' }}</span>
                </button>
                <ul class="sigs">
                  @for (x of g.signals; track x.key) {
                    <li><button class="sig-row" (click)="openRef(signalRef(x.key))">
                      <span class="data mono">{{ pct(x.intensity) }}</span>
                      <span class="sr"><span class="l1">@if (hintKeys().has(x.key)) { <app-icon name="hint" [size]="12" class="bulb" /> } {{ x.label }}</span>
                        <span class="l2">{{ x.detail }}</span></span>
                      @if (readers(x.key); as rs) {
                        <span class="readers">@for (r of rs; track r) { <app-icon [name]="schoolIcon(r)" [size]="13" [matTooltip]="'Letto da ' + schoolName(r)" /> }</span>
                      }
                    </button></li>
                  }
                </ul>
              </section>
            }
          }
          @case ('sky') {
            <app-sky-wheel [sky]="reading().sky" [natal]="reading().natal" [signals]="reading().signals" />
          }
        }
      }
    </div>
  `,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 0; height: 100%; }
    .panel-head { flex: none; display: flex; flex-direction: column; gap: 8px; padding-bottom: 10px; border-bottom: 1px solid var(--line); }
    .panel-head-top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    .panel-title { font-family: var(--font-title); font-size: 11px; font-weight: 700; letter-spacing: .16em; text-transform: uppercase; color: var(--accent); }
    .full-btn { display: inline-flex; align-items: center; gap: 6px; height: 30px; padding: 0 12px; border-radius: 999px; cursor: pointer;
      border: 1px solid var(--line-strong); background: transparent; color: var(--muted); font-family: var(--font-ui); font-size: 12px; }
    .full-btn:hover { color: var(--text); border-color: var(--accent); }
    .tabs { width: 100%; display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 6px; }
    .tabs button { min-width: 0; height: 58px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px;
      padding: 6px 2px; border-radius: 12px; cursor: pointer; border: 1px solid var(--line-strong);
      background: color-mix(in srgb, #0a1626 70%, transparent); color: var(--text);
      font-family: var(--font-ui); font-size: 12.5px; font-weight: 600; letter-spacing: .01em; line-height: 1.1;
      transition: color .15s, border-color .15s, background .15s; }
    .tabs button app-icon { color: var(--muted); }
    .tabs button:hover { border-color: var(--accent); }
    .tabs button.on { color: var(--accent); border-color: var(--accent); background: color-mix(in srgb, var(--accent) 14%, transparent);
      box-shadow: 0 0 14px color-mix(in srgb, var(--accent) 30%, transparent); }
    .tabs button.on app-icon { color: var(--accent); }
    .t-label { display: block; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .body { overflow-y: auto; min-height: 0; flex: 1; padding: 10px 2px 8px; }
    .back { border: 0; background: transparent; color: var(--muted); font: inherit; font-size: 12px; cursor: pointer;
      display: inline-flex; align-items: center; gap: 4px; padding: 0 0 8px; }
    .lead { font-size: 12.5px; color: var(--muted); line-height: 1.55; margin: 0 0 10px; }
    .c-inf { color: var(--c-influence); } .c-sig { color: var(--c-signal); } .c-rule { color: var(--c-rule); } .c-node { color: var(--c-node); }
    .sec { display: flex; align-items: center; gap: 6px; margin: 16px 0 8px; }
    .sec small { color: var(--muted); font-family: var(--font-mono); }
    .note { font-size: 12px; color: var(--muted); line-height: 1.5; margin: 6px 0; }
    .mono { font-family: var(--font-mono); }

    /* the four types: one colour and one shape each, everywhere */
    .shape { display: inline-block; width: 10px; height: 10px; vertical-align: -1px; margin-right: 2px; }
    .influence-shape { background: var(--c-influence); clip-path: polygon(50% 0, 100% 25%, 100% 75%, 50% 100%, 0 75%, 0 25%); }
    .signal-shape { background: var(--c-signal); border-radius: 2px; }
    .rule-shape { background: var(--c-rule); transform: rotate(45deg) scale(.8); }
    .node-shape { background: var(--q, var(--c-node)); border-radius: 50%; box-shadow: 0 0 8px var(--q, var(--c-node)); }
    .shape.small { width: 8px; height: 8px; }
    .hero { position: relative; padding: 12px 14px; border-radius: 12px; border: 1px solid var(--line-strong);
      background: linear-gradient(135deg, color-mix(in srgb, var(--k) 10%, transparent), transparent 60%); }
    .hero::before { content: ''; position: absolute; left: 0; top: 12px; bottom: 12px; width: 3px; border-radius: 2px; background: var(--k); }
    .t-node { --k: var(--q, var(--c-node)); } .t-rule { --k: var(--c-rule); } .t-signal { --k: var(--c-signal); } .t-influence { --k: var(--c-influence); }
    .type { font-family: var(--font-title); font-size: 9.5px; letter-spacing: .14em; text-transform: uppercase; color: var(--k);
      display: inline-flex; align-items: center; gap: 4px; }
    .title { margin: 6px 0 4px; font-size: 16px; line-height: 1.3; text-transform: none; letter-spacing: .02em; display: flex; align-items: center; gap: 8px; }
    .stat { display: flex; align-items: baseline; gap: 10px; font-size: 12px; color: var(--muted); }
    .big { font-size: 26px; color: var(--k); }
    .big.sig { color: var(--c-signal); }
    .desc { font-size: 13px; line-height: 1.55; color: color-mix(in srgb, var(--text) 85%, var(--muted)); margin: 6px 0; }
    .source { font-size: 12px; font-style: italic; color: var(--text); margin: 6px 0; padding: 6px 10px; border-radius: 8px;
      background: color-mix(in srgb, var(--c-rule) 10%, transparent); }
    .source span { font-style: normal; font-family: var(--font-title); font-size: 9px; letter-spacing: .14em; text-transform: uppercase;
      color: var(--c-rule); margin-right: 6px; }
    .agree { font-size: 12px; color: var(--muted); margin: 6px 0 0; }
    .agree.cross { color: var(--c-node); }
    .status { display: inline-block; margin-top: 6px; font-size: 11px; padding: 2px 10px; border-radius: 999px; border: 1px solid var(--line-strong); color: var(--muted); }
    .status.on { color: var(--c-node); border-color: color-mix(in srgb, var(--c-node) 60%, transparent); }
    .facts { display: grid; grid-template-columns: auto 1fr; gap: 4px 12px; font-size: 12px; margin: 10px 0 0; }
    .facts dt { color: var(--muted); }
    .facts dd { margin: 0; }
    .link { border: 0; background: transparent; color: var(--c-influence); font: inherit; padding: 0; cursor: pointer; text-decoration: underline; }

    .seg { display: inline-flex; border: 1px solid var(--line-strong); border-radius: 10px; overflow: hidden; margin-bottom: 10px; }
    .seg button { border: 0; background: transparent; color: var(--muted); font-family: var(--font-title); font-size: 10px; letter-spacing: .1em;
      text-transform: uppercase; padding: 8px 14px; cursor: pointer; }
    .seg button.on { color: var(--accent); background: color-mix(in srgb, var(--accent) 12%, transparent); }
    .card { border: 1px solid var(--line); border-left: 3px solid var(--q, var(--c-node)); border-radius: 12px; padding: 10px 12px;
      margin-bottom: 8px; background: linear-gradient(90deg, color-mix(in srgb, var(--q, var(--c-node)) 8%, transparent), transparent 55%); }
    .card-head { width: 100%; display: flex; align-items: center; gap: 8px; border: 0; background: transparent; color: var(--text);
      font: inherit; padding: 0; cursor: pointer; text-align: left; }
    .c-name { font-family: var(--font-title); font-weight: 700; font-size: 13px; letter-spacing: .06em; text-transform: uppercase; }
    .c-layer { color: var(--muted); font-size: 10.5px; text-transform: uppercase; letter-spacing: .08em; }
    .c-pct { margin-left: auto; font-size: 20px; color: var(--q, var(--c-node)); }
    .c-when { display: flex; align-items: center; gap: 5px; font-size: 12px; color: var(--accent); margin: 6px 0 4px; }
    .big-when { font-size: 13px; }
    .causes { list-style: none; margin: 4px 0 0; padding: 0; display: flex; flex-direction: column; gap: 6px; }
    .causes li { padding-left: 10px; border-left: 1px solid var(--line-strong); }
    .cause { display: flex; align-items: flex-start; gap: 8px; border: 0; background: transparent; color: var(--text);
      font: inherit; text-align: left; padding: 0; cursor: pointer; width: 100%; }
    .k-label { font-size: 12.5px; color: color-mix(in srgb, var(--c-signal) 50%, var(--text)); display: inline-flex; align-items: center; gap: 6px; }
    .cause .shape { margin-top: 4px; flex: none; }
    .k-text { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
    .k-detail { font-size: 11px; color: var(--muted); }
    .via { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; font-size: 10.5px; color: var(--muted); margin-top: 2px; }
    .rule-link { border: 0; background: transparent; color: color-mix(in srgb, var(--c-rule) 70%, var(--text)); font: inherit; font-size: 11px;
      padding: 0; cursor: pointer; text-align: left; }
    .rule-link:hover, .cause:hover .k-label { text-decoration: underline; }
    .rank { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }
    .rank-row { width: 100%; display: flex; gap: 10px; align-items: flex-start; border: 1px solid var(--line); border-radius: 10px; background: transparent;
      color: var(--text); font: inherit; text-align: left; padding: 8px 10px; cursor: pointer; }
    .rank-row:hover { border-color: var(--line-strong); }
    .r-n { color: var(--c-signal); font-size: 13px; min-width: 18px; }
    .r-main { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
    .r-label { font-size: 12.5px; }
    .r-sub, .r-nodes { font-size: 11px; color: var(--muted); }
    .r-nodes { color: color-mix(in srgb, var(--c-node) 60%, var(--muted)); }
    .r-amt { color: var(--muted); font-size: 11px; }
    .schools { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }
    .schools li { display: flex; align-items: center; gap: 8px; font-size: 12.5px; }
    .schools li small { color: var(--muted); font-size: 10.5px; }
    .schools li .mono { margin-left: auto; color: var(--c-node); }
    .schools li.silent { opacity: .5; }

    .checks { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }
    .checks li { display: flex; align-items: center; gap: 6px; font-size: 12px; flex-wrap: wrap; }
    .checks li.miss { opacity: .7; }
    .mark { width: 14px; color: var(--c-node); }
    .miss .mark { color: var(--muted); }
    .why { font-size: 11px; color: var(--muted); }
    .pill { border: 1px solid color-mix(in srgb, var(--k, var(--accent)) 55%, transparent); background: color-mix(in srgb, var(--k, var(--accent)) 10%, transparent);
      color: var(--text); font: inherit; font-size: 12px; border-radius: 6px; padding: 3px 9px; cursor: pointer; }
    .pill.t-node { border-radius: 999px; }
    .pill.t-sym { --k: var(--accent-2); cursor: default; }

    .theory { display: grid; grid-template-columns: auto auto minmax(0, 1fr); gap: 4px 8px; align-items: center;
      padding: 8px 0; border-bottom: 1px solid var(--line); }
    .theory .arr { color: var(--muted); }
    .theory .fam { grid-column: 1 / -1; font-size: 11.5px; color: var(--c-node); }
    .theory .fam b { font-weight: 400; }
    .theory .sch { grid-column: 1 / -1; font-size: 11px; color: var(--muted); }

    .dhint { width: 100%; text-align: left; display: flex; gap: 10px; align-items: flex-start; border: 1px solid color-mix(in srgb, var(--warn) 45%, transparent);
      background: color-mix(in srgb, var(--warn) 8%, transparent); color: var(--text); border-radius: 10px; padding: 8px 10px; font: inherit;
      cursor: pointer; margin-bottom: 6px; }
    .dhint.static { cursor: default; }
    .dhint app-icon { color: var(--warn); }
    .dhint b { display: block; font-weight: 500; color: var(--warn); }
    .dhint small { display: block; font-size: 11.5px; color: var(--muted); }
    .all { display: flex; gap: 6px; align-items: center; font-size: 12px; color: var(--muted); margin: 6px 0; }
    .src { margin-top: 10px; }
    .src-head { width: 100%; display: flex; align-items: center; gap: 8px; border: 0; background: transparent; color: var(--c-influence);
      font-family: var(--font-title); font-size: 10px; letter-spacing: .12em; text-transform: uppercase; padding: 4px 0; cursor: pointer; }
    .src-head .name { flex: 1; text-align: left; }
    .state { color: var(--muted); }
    .state.bad { color: var(--mat-sys-error); }
    .sigs { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 2px; }
    .sig-row { width: 100%; display: flex; gap: 10px; align-items: center; border: 1px solid transparent; background: transparent; color: var(--text);
      font: inherit; text-align: left; padding: 5px 6px; border-radius: 8px; cursor: pointer; }
    .sig-row:hover { border-color: var(--line-strong); }
    .data { flex: none; min-width: 40px; text-align: center; font-size: 11px; padding: 2px 4px; border-radius: 4px; color: var(--c-signal);
      border: 1px solid color-mix(in srgb, var(--c-signal) 55%, transparent); }
    .sr { display: flex; flex-direction: column; min-width: 0; flex: 1; }
    .sr .l1 { font-size: 12.5px; }
    .sr .l2 { font-size: 10.5px; color: var(--muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .readers { display: inline-flex; gap: 2px; color: var(--c-rule); }
    .bulb { color: var(--warn); }
  `,
})
export class ChainPanel {
  private readonly catalog = inject(CatalogStore);

  readonly reading = input.required<Reading>();
  readonly sources = input<SourceStatus[]>([]);
  readonly hints = input<DiaryHint[]>([]);
  readonly selRef = input<FlowRef | null>(null);
  readonly tab = input<PanelTab>('now');
  /** The ±48 h around the moment (loaded by the page), for when things start and end. */
  readonly timeline = input<TimelineResponse | null>(null);
  /** Flow tab: diagram or tree. */
  readonly flowView = input<'diagram' | 'tree'>('diagram');
  readonly expanded = input(false);
  readonly showPositions = input(false);
  readonly profileId = input.required<number>();
  readonly consensus = input('plain');

  /** Every navigation goes up as URL changes (the page owns the URL). */
  readonly navigate = output<{ sel?: FlowRef | null; tab?: PanelTab; positions?: boolean; flowView?: 'diagram' | 'tree' }>();
  readonly back = output<void>();
  readonly fullscreen = output<boolean>();

  protected readonly tabs = TABS;
  protected readonly sel = computed(() => parseRef(this.selRef()));

  protected go(p: { sel?: FlowRef | null; tab?: PanelTab }) { this.navigate.emit(p); }
  protected openRef(ref: FlowRef) { this.navigate.emit({ sel: ref }); }
  protected openHint(h: DiaryHint) {
    this.openRef(h.target.kind === 'zone' ? refOf('node', h.target.id) : refOf('signal', h.target.id));
  }
  protected togglePositions() { this.navigate.emit({ positions: !this.showPositions() }); }
  protected signalRef = (key: string) => refOf('signal', key);
  protected influenceRef = (id: string) => refOf('influence', id);

  // ---------- graphs ----------

  protected readonly overview = computed(() => buildFlow(this.reading(), this.catalog, null, { nodes: this.expanded() ? 12 : 7 }));
  protected readonly focusGraph = computed(() => buildFlow(this.reading(), this.catalog, this.selRef()));
  protected readonly nowTheories = computed(() => theoriesNow(this.reading(), this.catalog));
  protected readonly catalogTheories = computed(() => theoriesInCatalog(this.catalog));

  // ---------- detail views ----------

  private readonly signalByKey = computed(() => new Map(this.reading().signals.map((s) => [s.key, s])));

  protected readonly nodeView = computed(() => {
    const s = this.sel();
    if (s?.kind !== 'node') return null;
    const z = this.reading().zones.find((x) => x.zone === s.id);
    if (!z) return null;
    const meta = this.catalog.catalog()?.zones.find((n) => n.id === z.zone);
    const schools = Object.entries(z.bySchool).sort((a, b) => b[1] - a[1]).map(([id, score]) => ({ id, score }));
    const above = schools.filter((x) => x.score >= 0.5).map((x) => x.id);
    const fam = new Set(familiesOf(above, this.catalog).values());
    const agreement = above.length > 1
      ? { families: fam.size, text: fam.size > 1
          ? `Lo vedono più sollecitato del solito ${above.length} scuole di ${fam.size} famiglie indipendenti: un incrocio di fonti.`
          : `Lo vedono più sollecitato del solito ${above.length} scuole, ma della stessa famiglia: la stessa idea letta più volte.` }
      : null;
    const byKey = new Map<string, { key: string; label: string; amount: number }>();
    for (const c of z.contributions) {
      if (!this.signalByKey().has(c.signalKey)) continue;
      const e = byKey.get(c.signalKey) ?? { key: c.signalKey, label: c.signalLabel, amount: 0 };
      e.amount += c.amount;
      byKey.set(c.signalKey, e);
    }
    const causes = [...byKey.values()].sort((a, b) => b.amount - a.amount).slice(0, 3);
    return {
      z, layer: meta?.layerLabel ?? z.layer, description: meta?.description ?? '', schools, agreement, causes,
      window: this.nodeWindow(z), allCauses: this.causesOf(z, 12),
    };
  });

  protected readonly ruleView = computed(() => {
    const s = this.sel();
    if (s?.kind !== 'rule') return null;
    const [schoolId, ruleId] = s.id.split('/');
    const school = this.catalog.school(schoolId);
    if (!school) return null;
    const rule = school.rules.find((r) => r.id === ruleId) ?? null;
    const active = this.reading().zones.some((z) => z.contributions.some((c) => c.schoolId === schoolId && c.ruleId === ruleId));
    const candidates = rule && rule.kind !== 'node'
      ? this.reading().signals.filter((x) => x.kind === rule.kind).map((x) => this.check(rule, x))
      : [];
    return { school, rule, ruleId, active, candidates, schoolActive: this.reading().schoolIds.includes(schoolId) };
  });

  private check(rule: CatalogRule, s: Signal): { signal: Signal; ok: boolean; why: string } {
    if (s.intensity < rule.minIntensity)
      return { signal: s, ok: false, why: `intensità ${this.pct(s.intensity)} sotto la soglia ${this.pct(rule.minIntensity)}` };
    for (const [tag, accepted] of Object.entries(rule.tags ?? {})) {
      const value = s.tags[tag];
      if (value === undefined || !(accepted.includes(value) || accepted.includes('*')))
        return { signal: s, ok: false, why: `${tag} = ${value ?? '—'}, la regola vuole ${accepted.join(', ')}` };
    }
    return { signal: s, ok: true, why: `intensità ${this.pct(s.intensity)}` };
  }

  protected readonly signalView = computed(() => {
    const s = this.sel();
    if (s?.kind !== 'signal') return null;
    const signal = this.signalByKey().get(s.id) ?? null;
    const reads = this.reading().zones.some((z) => z.contributions.some((c) => c.signalKey === s.id));
    return {
      label: signal?.label ?? s.id,
      detail: signal?.detail ?? 'nodo attivato da una scuola (secondo passaggio)',
      intensity: signal?.intensity ?? 0,
      value: signal?.value ?? null,
      unit: signal?.unit ?? '',
      influence: signal ? this.catalog.influence(signal.source) ?? null : null,
      window: this.signalWindow(s.id),
      reads,
      hints: this.hints().filter((h) => h.target.kind === 'signal' && h.target.id === s.id),
    };
  });

  protected readonly influenceView = computed(() => {
    const s = this.sel();
    if (s?.kind !== 'influence') return null;
    const info = this.catalog.influence(s.id);
    if (!info) return null;
    return {
      info,
      status: this.sources().find((x) => x.source === s.id) ?? null,
      signals: this.reading().signals.filter((x) => x.source === s.id).sort((a, b) => b.intensity - a.intensity),
    };
  });

  // ---------- time: when things start and end ----------

  private readonly atMs = computed(() => new Date(this.reading().at).getTime());

  private episode(key: string, zone: boolean): Episode | null {
    const t = this.timeline()?.timeline;
    if (!t) return null;
    const track = zone ? t.zones.find((x) => x.key === key) : t.signals.find((x) => x.key === key);
    return episodeAt(track, this.atMs(), zone ? 0.5 : 0.05);
  }

  /** "più sollecitato del solito da oggi 14:00 a domani 16:00 · picco …" (rarity above 50% = above the person's median). */
  private nodeWindow(z: ZoneActivation): string {
    const e = this.episode(`zone:${z.zone}`, true);
    if (!e) return this.timeline() ? '' : 'ricostruisco le 96 ore intorno…';
    if (!e.now) return `nella sua norma; ${windowText(e, this.atMs())}`;
    const peak = e.peak.t === this.atMs() ? '' : ` · picco ${when(e.peak.t, this.atMs())} (${this.pct(e.peak.v)})`;
    return `più sollecitato del solito ${windowText(e, this.atMs())}${peak}`;
  }

  private signalWindow(key: string): string {
    const e = this.episode(key, false);
    return e?.now ? windowText(e, this.atMs()) : '';
  }

  /** The causes of a node, strongest first: one per signal, with the rules that carried it and when it is active. */
  private causesOf(z: ZoneActivation, limit: number) {
    const groups = new Map<string, { key: string; amount: number; rules: Map<string, { ref: FlowRef; label: string }> }>();
    for (const c of z.contributions) {
      if (!this.signalByKey().has(c.signalKey)) continue;
      const g = groups.get(c.signalKey) ?? { key: c.signalKey, amount: 0, rules: new Map() };
      g.amount += c.amount;
      const school = this.catalog.school(c.schoolId);
      const rule = school?.rules.find((r) => r.id === c.ruleId);
      g.rules.set(`${c.schoolId}/${c.ruleId}`, { ref: refOf('rule', `${c.schoolId}/${c.ruleId}`), label: `${school?.name ?? c.schoolId}: ${rule?.name ?? c.ruleId}` });
      groups.set(c.signalKey, g);
    }
    return [...groups.values()].sort((a, b) => b.amount - a.amount).slice(0, limit).map((g) => {
      const s = this.signalByKey().get(g.key)!;
      return {
        key: g.key, label: s.label, detail: s.detail, amount: g.amount,
        influence: this.catalog.influence(s.source)?.name ?? s.source,
        window: this.signalWindow(g.key),
        rules: [...g.rules.values()],
      };
    });
  }

  protected readonly cards = computed(() => {
    this.timeline();
    return [...this.reading().zones]
      .filter((z) => z.score > 0.02)
      .sort((a, b) => b.score - a.score)
      .slice(0, 10)
      .map((z) => ({
        z,
        layer: this.catalog.catalog()?.zones.find((n) => n.id === z.zone)?.layerLabel ?? z.layer,
        window: this.nodeWindow(z),
        causes: this.causesOf(z, 3),
      }));
  });

  /** Signals by how much they move the body in all, with the parts they touch. */
  protected readonly ranking = computed(() => {
    this.timeline();
    const map = new Map<string, { key: string; amount: number; nodes: Map<string, number> }>();
    for (const z of this.reading().zones)
      for (const c of z.contributions) {
        if (!this.signalByKey().has(c.signalKey)) continue;
        const e = map.get(c.signalKey) ?? { key: c.signalKey, amount: 0, nodes: new Map() };
        e.amount += c.amount;
        e.nodes.set(z.label, (e.nodes.get(z.label) ?? 0) + c.amount);
        map.set(c.signalKey, e);
      }
    return [...map.values()].sort((a, b) => b.amount - a.amount).slice(0, 12).map((e) => {
      const s = this.signalByKey().get(e.key)!;
      return {
        key: e.key, label: s.label, detail: s.detail, amount: e.amount,
        influence: this.catalog.influence(s.source)?.name ?? s.source,
        window: this.signalWindow(e.key),
        nodes: [...e.nodes.entries()].sort((a, b) => b[1] - a[1]).map(([n]) => n),
      };
    });
  });

  protected nodeRef = (id: string) => refOf('node', id);

  // ---------- influences tab ----------

  private readonly readersByKey = computed(() => {
    const map = new Map<string, Set<string>>();
    for (const z of this.reading().zones)
      for (const c of z.contributions) map.set(c.signalKey, (map.get(c.signalKey) ?? new Set()).add(c.schoolId));
    return map;
  });
  protected readers = (key: string) => {
    const r = this.readersByKey().get(key);
    return r ? [...r] : null;
  };
  protected readonly hintKeys = computed(() => new Set(this.hints().filter((h) => h.target.kind === 'signal').map((h) => h.target.id)));

  protected readonly sourceGroups = computed(() => {
    const order = this.catalog.influences().map((i) => i.info.id);
    const all = this.showPositions();
    return this.sources()
      .map((st) => {
        const info = this.catalog.influence(st.source);
        const signals = this.reading().signals
          .filter((s) => s.source === st.source && (all || (!POSITION_KINDS.has(s.kind) && s.intensity > 0.02)))
          .sort((a, b) => b.intensity - a.intensity);
        return { id: st.source, name: info?.name ?? st.source, icon: info?.icon ?? st.source, derived: info?.reliability === 'derived', ok: st.ok, signals };
      })
      .sort((a, b) => order.indexOf(a.id) - order.indexOf(b.id));
  });

  // ---------- labels ----------

  protected schoolName = (id: string) => this.catalog.school(id)?.name ?? id;
  protected schoolIcon = (id: string) => this.catalog.school(id)?.icon ?? id;
  protected family = (id: string) => this.catalog.school(id)?.lineage.name ?? '';
  protected quality = (q: string) => QUALITY_NAME[q] ?? q;
  protected zonesText = (r: CatalogRule) =>
    [r.zones.map((z) => z.label).join(', '), r.zonesFromText].filter((x) => x).join(' · ') || '—';
  protected color = qualityColor;
  protected pct = (v: number) => `${Math.round(v * 100)}%`;
}
