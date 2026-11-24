import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CatalogStore } from '../core/catalog-store';
import { ProfileStore } from '../core/profile-store';
import { Icon } from './icon';

/**
 * The active schools of the profile, as a widget in the page: a compact button that opens the list, with "all" and
 * "none". "None" is kept local until at least one school is chosen (a reading needs one); every choice is saved on
 * the profile, so correlations stay reproducible.
 */
@Component({
  selector: 'app-school-picker',
  imports: [Icon, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button class="trigger" (click)="open.set(!open())" [attr.aria-expanded]="open()">
      <app-icon name="catalog" [size]="16" />
      <span class="t-label">Scuole</span>
      <span class="t-count">{{ activeCount() }}/{{ catalog.schools().length }}</span>
    </button>
    @if (open()) {
      <div class="pop" [class.inline]="inline()">
        <div class="sp-actions">
          <button class="act" (click)="all()" [disabled]="store.saving()">Tutte</button>
          <button class="act" (click)="none()" [disabled]="store.saving()">Nessuna</button>
          <button class="close" (click)="open.set(false)" aria-label="Chiudi"><app-icon name="close" [size]="16" /></button>
        </div>
        @if (empty()) { <p class="warn">Scegli almeno una scuola: la lettura ne ha bisogno.</p> }
        <ul>
          @for (s of catalog.schools(); track s.id) {
            <li>
              <button class="school" [class.on]="isOn(s.id)" (click)="toggle(s.id)" [disabled]="store.saving()"
                      [matTooltip]="s.tradition" matTooltipPosition="right">
                <span class="box"></span>
                <app-icon [name]="s.icon" [size]="18" />
                <span class="name">{{ s.name }}</span>
                <span class="family">{{ s.lineage.name }}</span>
              </button>
            </li>
          }
        </ul>
      </div>
    }
  `,
  styles: `
    :host { position: relative; display: inline-block; }
    .trigger { display: inline-flex; align-items: center; gap: 8px; height: 36px; padding: 0 14px; border-radius: 999px; cursor: pointer;
      border: 1px solid var(--line-strong); background: var(--hud-glass); color: var(--text); font: inherit; font-size: 13px;
      backdrop-filter: blur(14px); }
    .t-label { font-family: var(--font-title); font-size: 11px; letter-spacing: .14em; text-transform: uppercase; }
    .t-count { font-family: var(--font-mono); color: var(--accent); font-size: 12px; }
    .pop { position: absolute; top: calc(100% + 8px); left: 0; z-index: 20; width: min(360px, 86vw); max-height: 70vh; overflow: auto;
      padding: 10px; border-radius: 16px; border: 1px solid var(--line-strong); background: var(--hud-glass-strong);
      backdrop-filter: blur(18px); box-shadow: 0 18px 50px rgba(0, 0, 0, .45); }
    .pop.inline { position: static; margin-top: 8px; }
    .sp-actions { display: flex; gap: 6px; align-items: center; margin-bottom: 6px; }
    .act { border: 1px solid var(--line-strong); background: transparent; color: var(--text); font: inherit; font-size: 12px;
      padding: 4px 12px; border-radius: 999px; cursor: pointer; }
    .close { margin-left: auto; border: 0; background: transparent; color: var(--muted); cursor: pointer; width: 28px; height: 28px; border-radius: 50%; }
    .warn { font-size: 12px; color: var(--warn); margin: 2px 4px 6px; }
    ul { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 2px; }
    .school { width: 100%; display: grid; grid-template-columns: 14px 20px minmax(0, 1fr) auto; gap: 10px; align-items: center;
      border: 0; background: transparent; color: var(--muted); font: inherit; font-size: 13px; text-align: left; padding: 7px 8px;
      border-radius: 10px; cursor: pointer; }
    .school:hover { background: color-mix(in srgb, var(--accent) 8%, transparent); }
    .school.on { color: var(--text); }
    .box { width: 12px; height: 12px; border-radius: 3px; border: 1px solid var(--line-strong); }
    .school.on .box { background: var(--accent); border-color: var(--accent); box-shadow: 0 0 8px var(--accent); }
    .family { font-size: 10.5px; color: var(--muted); text-transform: uppercase; letter-spacing: .06em; }
  `,
})
export class SchoolPicker {
  protected readonly store = inject(ProfileStore);
  protected readonly catalog = inject(CatalogStore);
  /** Opens below in the flow (pages) instead of floating (the stage). */
  readonly inline = input(false);

  protected readonly open = signal(false);
  /** "None" chosen, nothing saved yet. */
  protected readonly empty = signal(false);

  protected readonly activeCount = computed(() => (this.empty() ? 0 : this.store.schools().length));
  protected isOn = (id: string) => !this.empty() && this.store.schools().includes(id);

  protected async toggle(id: string) {
    if (this.empty()) {
      this.empty.set(false);
      await this.store.setSchools([id]);
      return;
    }
    if (this.store.schools().length === 1 && this.isOn(id)) {
      this.empty.set(true);
      return;
    }
    await this.store.toggle(id);
  }

  protected async all() {
    this.empty.set(false);
    await this.store.setSchools(this.catalog.schools().map((s) => s.id));
  }

  protected none() {
    this.empty.set(true);
  }
}
