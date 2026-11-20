import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ProfileStore } from './core/profile-store';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
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
      <nav>
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Adesso</a>
        <a routerLink="/timeline" routerLinkActive="active">Timeline</a>
        <a routerLink="/diario" routerLinkActive="active">Diario</a>
        <a routerLink="/corrispondenze" routerLinkActive="active">Corrispondenze</a>
        <a routerLink="/profilo" routerLinkActive="active" class="who">{{ store.current()?.name ?? 'Profilo' }}</a>
      </nav>
    </header>
    <main>
      @if (store.loaded()) {
        <router-outlet />
      } @else {
        <p class="muted">Apertura del tempio…</p>
      }
    </main>
  `,
})
export class App {
  protected readonly store = inject(ProfileStore);
  protected readonly rays = Array.from({ length: 12 }, (_, i) => (i * Math.PI) / 6);
  protected cos = Math.cos;
  protected sin = Math.sin;

  constructor() {
    void this.store.load().catch((e) => console.error('Backend non raggiungibile', e));
  }
}
