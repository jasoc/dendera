import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { JournalDay, METRICS, Metric } from '../core/models';
import { METRIC_HINT, METRIC_NAME } from '../core/symbols';

/** The daily diary: 10 seconds a day. This is the data that tests every rule. */
@Component({
  selector: 'app-journal',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatTooltipModule],
  template: `
    <div class="journal-grid">
      <section class="panel">
        <h2>Diario <small>come stai oggi?</small></h2>
        <label class="field inline">
          <span>Giorno</span>
          <input type="date" [value]="date()" (change)="pickDate($any($event.target).value)" />
        </label>

        <div class="metrics">
          @for (m of metrics; track m) {
            <div class="metric">
              <div class="m-head"><b>{{ names[m] }}</b> <span class="muted">{{ hints[m] }}</span></div>
              <div class="scale">
                @for (v of [1, 2, 3, 4, 5]; track v) {
                  <button type="button" class="dot-btn" [class.on]="entry()[m] === v" (click)="set(m, v)">{{ v }}</button>
                }
                @if (entry()[m] != null) {
                  <button type="button" matIconButton class="clear" (click)="set(m, null)" matTooltip="Non registrato">
                    <span class="material-symbols-rounded">close</span>
                  </button>
                }
              </div>
            </div>
          }
        </div>

        <mat-form-field appearance="outline">
          <mat-label>Note (cosa è successo, sogni, sensazioni)</mat-label>
          <textarea matInput rows="3" [ngModel]="entry().notes" (ngModelChange)="patchNotes($event)"></textarea>
        </mat-form-field>

        <div class="actions">
          <button matButton="filled" (click)="save()" [disabled]="saving()">Salva</button>
          @if (saved()) { <span class="ok">Salvato ✓</span> }
          @if (error()) { <span class="error">{{ error() }}</span> }
        </div>
      </section>

      <section class="panel">
        <h2>Giorni registrati <small>{{ history().length }}</small></h2>
        @if (history().length === 0) {
          <p class="muted">Ancora niente. Bastano pochi secondi al giorno: dopo 10 giorni partono le correlazioni.</p>
        }
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr><th>Giorno</th>@for (m of metrics; track m) { <th [title]="names[m]">{{ names[m].slice(0, 3) }}</th> }<th></th></tr>
            </thead>
            <tbody>
              @for (d of history(); track d.date) {
                <tr [class.current]="d.date === date()" (click)="pickDate(d.date)">
                  <td>{{ d.date }}</td>
                  @for (m of metrics; track m) { <td class="num">{{ d[m] ?? '·' }}</td> }
                  <td><button matIconButton class="danger" (click)="remove(d.date, $event)" matTooltip="Elimina">
                    <span class="material-symbols-rounded">delete</span></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    </div>
  `,
})
export class Journal {
  private readonly api = inject(Api);
  private readonly store = inject(ProfileStore);

  protected readonly metrics = METRICS;
  protected readonly names = METRIC_NAME;
  protected readonly hints = METRIC_HINT;

  protected readonly date = signal(today());
  protected readonly entry = signal<JournalDay>(blank(today()));
  protected readonly history = signal<JournalDay[]>([]);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const p = this.store.current();
      if (p) void this.refresh(p.id);
    });
  }

  private async refresh(id: number) {
    try {
      this.history.set(await this.api.journal(id));
      this.pickDate(this.date());
    } catch (e) {
      this.error.set('Impossibile caricare il diario.');
      console.error(e);
    }
  }

  protected pickDate(d: string) {
    if (!d) return;
    this.date.set(d);
    this.saved.set(false);
    const existing = this.history().find((h) => h.date === d);
    this.entry.set(existing ? { ...existing } : blank(d));
  }

  protected set(m: Metric, v: number | null) {
    this.entry.update((e) => ({ ...e, [m]: v }));
    this.saved.set(false);
  }

  protected patchNotes(notes: string) {
    this.entry.update((e) => ({ ...e, notes }));
    this.saved.set(false);
  }

  protected async save() {
    const p = this.store.current();
    if (!p) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.api.saveJournal(p.id, { ...this.entry(), date: this.date() });
      this.saved.set(true);
      this.history.set(await this.api.journal(p.id));
    } catch (e) {
      this.error.set('Salvataggio non riuscito.');
      console.error(e);
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(date: string, ev: Event) {
    ev.stopPropagation();
    const p = this.store.current();
    if (!p || !confirm(`Eliminare il diario del ${date}?`)) return;
    await this.api.deleteJournal(p.id, date);
    this.history.set(await this.api.journal(p.id));
    if (date === this.date()) this.entry.set(blank(date));
  }
}

function today() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function blank(date: string): JournalDay {
  return { date, notes: null, energy: null, mood: null, sleep: null, focus: null, stress: null, gut: null, head: null, body: null };
}
