import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FlowGraph, FlowItem, FlowKind, FlowRef } from '../../core/flow-graph';
import { qualityColor } from '../../core/symbols';
import { Icon } from '../icon';

interface TreeNode { item: FlowItem; amount: number; children: TreeNode[]; key: string }

/**
 * The same chains as a tree, cause first: influence ▸ signal ▸ rule ▸ consequence. Each level has its own shape and
 * colour (hexagon influence, amber signal with its value, violet rule with its source, consequence in its quality
 * colour). Click the chevron to fold, the row to open the thing.
 */
@Component({
  selector: 'app-cause-tree',
  imports: [Icon, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="tree">
      @for (n of roots(); track n.key) {
        @let open = !closed().has(n.key);
        <li class="lvl influence">
          <div class="row">
            <button class="chev" (click)="toggle(n.key)" [class.open]="open">›</button>
            <button class="item" (click)="pick.emit(n.item.ref)">
              <span class="tb hex"><app-icon [name]="n.item.icon" [size]="14" /></span>
              <span class="txt"><span class="l1">{{ n.item.label }}</span><span class="l2">{{ n.item.sub }}</span></span>
              <span class="amt">{{ n.amount.toFixed(2) }}</span>
            </button>
          </div>
          @if (open) {
            <ul>
              @for (s of n.children; track s.key) {
                @let sOpen = !closed().has(s.key);
                <li class="lvl signal">
                  <div class="row">
                    <button class="chev" (click)="toggle(s.key)" [class.open]="sOpen">›</button>
                    <button class="item" (click)="pick.emit(s.item.ref)">
                      <span class="tb data">{{ s.item.level !== null ? pct(s.item.level) : '·' }}</span>
                      <span class="txt"><span class="l1">{{ s.item.label }}</span><span class="l2">{{ s.item.sub }}</span></span>
                    </button>
                  </div>
                  @if (sOpen) {
                    <ul>
                      @for (r of s.children; track r.key) {
                        <li class="lvl rule">
                          <div class="row">
                            <span class="chev ghost"></span>
                            <button class="item" (click)="pick.emit(r.item.ref)">
                              <span class="tb rule-b"><app-icon [name]="r.item.icon" [size]="13" /></span>
                              <span class="txt"><span class="l1">{{ r.item.label }}</span><span class="l2 src">{{ r.item.sub }}</span></span>
                            </button>
                          </div>
                          <ul class="leaves">
                            @for (t of r.children; track t.key) {
                              <li class="lvl node">
                                <button class="item leaf" (click)="pick.emit(t.item.ref)" [style.--q]="color(t.item.quality)">
                                  <span class="tb node-b">{{ t.item.level !== null ? pct(t.item.level) : '' }}</span>
                                  <span class="txt"><span class="l1">{{ t.item.label }}</span></span>
                                  <span class="amt">+{{ t.amount.toFixed(2) }}</span>
                                </button>
                              </li>
                            }
                          </ul>
                        </li>
                      }
                    </ul>
                  }
                </li>
              }
            </ul>
          }
        </li>
      }
    </ul>
  `,
  styles: `
    :host { display: block; }
    ul { list-style: none; margin: 0; padding: 0; }
    ul ul { margin-left: 13px; padding-left: 12px; border-left: 1px solid var(--line); }
    .lvl { position: relative; }
    ul ul > .lvl::before { content: ''; position: absolute; left: -12px; top: 17px; width: 10px; border-top: 1px solid var(--line); }
    .row { display: flex; align-items: center; gap: 2px; }
    .chev { width: 18px; height: 18px; flex: none; border: 0; background: transparent; color: var(--muted); cursor: pointer;
      font-size: 14px; transition: transform .15s; padding: 0; }
    .chev.open { transform: rotate(90deg); }
    .chev.ghost { cursor: default; }
    .item { flex: 1; min-width: 0; display: flex; align-items: center; gap: 8px; border: 1px solid transparent; background: transparent;
      color: var(--text); font: inherit; text-align: left; padding: 4px 6px; border-radius: 8px; cursor: pointer; margin: 1px 0; }
    .item:hover { border-color: var(--line-strong); background: color-mix(in srgb, var(--accent) 5%, transparent); }
    .txt { display: flex; flex-direction: column; min-width: 0; flex: 1; }
    .l1 { font-size: 12.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .l2 { font-size: 10.5px; color: var(--muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .l2.src { font-style: italic; }
    .amt { font-family: var(--font-mono); font-size: 11px; color: var(--muted); }
    .tb { flex: none; display: grid; place-items: center; }
    .hex { width: 24px; height: 26px; color: #021018; background: var(--c-influence);
      clip-path: polygon(50% 0, 100% 25%, 100% 75%, 50% 100%, 0 75%, 0 25%); }
    .influence > .row .l1 { font-family: var(--font-title); font-size: 10px; letter-spacing: .12em; text-transform: uppercase; color: var(--c-influence); }
    .data { min-width: 36px; height: 20px; padding: 0 4px; border-radius: 4px; font-family: var(--font-mono); font-size: 11px;
      color: var(--c-signal); border: 1px solid color-mix(in srgb, var(--c-signal) 60%, transparent);
      background: color-mix(in srgb, var(--c-signal) 10%, transparent); }
    .rule-b { width: 22px; height: 22px; border-radius: 6px; color: var(--c-rule); transform: rotate(45deg);
      border: 1px solid color-mix(in srgb, var(--c-rule) 70%, transparent); background: color-mix(in srgb, var(--c-rule) 12%, transparent); }
    .rule-b app-icon { transform: rotate(-45deg); }
    .rule > .row .l1 { color: color-mix(in srgb, var(--c-rule) 60%, var(--text)); }
    .leaves { margin-left: 33px; }
    .node-b { min-width: 36px; height: 20px; border-radius: 999px; font-family: var(--font-mono); font-size: 11px; color: #03140c;
      background: var(--q, var(--c-node)); box-shadow: 0 0 10px color-mix(in srgb, var(--q, var(--c-node)) 60%, transparent); }
  `,
})
export class CauseTree {
  readonly graph = input.required<FlowGraph>();
  readonly pick = output<FlowRef>();

  /** Folded rows (influences and signals); everything starts open. */
  protected readonly closed = signal(new Set<string>());

  protected readonly roots = computed<TreeNode[]>(() => {
    const g = this.graph();
    const level = (kind: FlowKind, paths = g.paths, prefix = ''): TreeNode[] => {
      const next: Record<FlowKind, FlowKind | null> = { influence: 'signal', signal: 'rule', rule: 'node', node: null };
      const groups = new Map<FlowRef, typeof paths>();
      for (const p of paths) groups.set(p[kind], [...(groups.get(p[kind]) ?? []), p]);
      return [...groups.entries()]
        .map(([ref, ps]) => {
          const key = `${prefix}/${ref}`;
          const nk = next[kind];
          return { item: g.items.get(ref)!, amount: ps.reduce((s, p) => s + p.amount, 0), children: nk ? level(nk, ps, key) : [], key };
        })
        .sort((a, b) => b.amount - a.amount);
    };
    return level('influence');
  });

  protected toggle(key: string) {
    this.closed.update((s) => {
      const n = new Set(s);
      if (n.has(key)) n.delete(key); else n.add(key);
      return n;
    });
  }

  protected color = qualityColor;
  protected pct = (v: number) => `${Math.round(v * 100)}%`;
}
