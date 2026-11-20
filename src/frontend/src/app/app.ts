import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ProfileStore } from './core/profile-store';
import { CatalogStore, SCHOOL_SHORT } from './core/catalog-store';
import { Icon } from './components/icon';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, MatChipsModule, MatProgressBarModule, MatTooltipModule],
  template: `
    <header class="topbar">
      <a routerLink="/" class="brand" title="Dendera">
        <svg viewBox="0 0 32 32" class="logo" aria-hidden="true">
          <circle cx="16" cy="16" r="14" />
          <circle cx="16" cy="16" r="9" />
          @for (i of rays; track i) {
            <line [attr.x1]="16 + 9 * cos(i)" [attr.y1]="16 + 9 * sin(i)" [attr.x2]="16 + 14 * cos(i)" [attr.y2]="16 + 14 * sin(i)" />
          }
          <circle cx="16" cy="16" r="2.5" class="sun" />
        </svg>
        <span>Dendera</span>
      </a>

      @if (store.current() && catalog.schools().length) {
        <mat-chip-listbox class="school-toggles" multiple aria-label="Scuole attive">
          @for (s of catalog.schools(); track s.id) {
            <mat-chip-option
              [selected]="isOn(s.id)"
              [disabled]="store.saving() || (isOn(s.id) && store.schools().length === 1)"
              [matTooltip]="(isOn(s.id) ? 'Attiva: ' : 'Spenta: ') + s.name + ' — ' + s.tradition"
              (selectionChange)="$event.isUserInput && store.toggle(s.id)">
              <app-icon matChipAvatar [name]="s.icon" [size]="18" />
              {{ short(s.id, s.name) }}
            </mat-chip-option>
          }
        </mat-chip-listbox>
      }

      <nav class="main-nav">
        @for (n of nav; track n.path) {
          <a [routerLink]="n.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: n.path === '/' }" [matTooltip]="n.label">
            <app-icon [name]="n.icon" [size]="19" /><span class="label">{{ n.label }}</span>
          </a>
        }
        <a routerLink="/profilo" routerLinkActive="active" class="who" matTooltip="Profilo">
          <app-icon name="profile" [size]="19" /><span class="label">{{ store.current()?.name ?? 'Profilo' }}</span>
        </a>
      </nav>
    </header>
    @if (store.saving()) { <mat-progress-bar mode="indeterminate" class="top-progress" /> }
    <main>
      @if (store.loaded()) {
        <router-outlet />
      } @else {
        <p class="muted">Apertura del tempio…</p>
      }
    </main>
  `,
  styles: `
    .top-progress { position: sticky; top: 0; z-index: 11; }
  `,
})
export class App {
  protected readonly store = inject(ProfileStore);
  protected readonly catalog = inject(CatalogStore);
  protected readonly short = (id: string, name: string) => SCHOOL_SHORT[id] ?? name;
  protected readonly isOn = (id: string) => this.store.schools().includes(id);
  protected readonly nav = [
    { path: '/', label: 'Adesso', icon: 'now' },
    { path: '/timeline', label: 'Timeline', icon: 'timeline' },
    { path: '/diario', label: 'Diario', icon: 'journal' },
    { path: '/corrispondenze', label: 'Corrispondenze', icon: 'correlations' },
    { path: '/scuole', label: 'Scuole', icon: 'catalog' },
  ];
  protected readonly rays = Array.from({ length: 12 }, (_, i) => (i * Math.PI) / 6);
  protected cos = Math.cos;
  protected sin = Math.sin;

  constructor() {
    void this.store
      .load()
      .then(() => this.catalog.load())
      .catch((e) => console.error('Backend non raggiungibile', e));
  }
}
