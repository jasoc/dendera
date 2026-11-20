import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { Api } from '../core/api';
import { ProfileStore } from '../core/profile-store';
import { NatalChart, Place, Profile, ProfileInput, RuleSetInfo } from '../core/models';
import { BODY_GLYPH } from '../core/symbols';

interface Form {
  id: number | null;
  name: string;
  birthDate: string;
  birthTime: string;
  birthTimeKnown: boolean;
  offsetMinutes: number;
  birthPlace: Place | null;
  currentPlace: Place | null;
  sameAsBirth: boolean;
  houseSystem: string;
  ruleSetId: string;
}

/** Profile: birth data (date, time, place) and where the person lives now. */
@Component({
  selector: 'app-profile',
  imports: [DecimalPipe],
  template: `
    <div class="profile-grid">
      <section class="panel">
        <h2>{{ form().id ? 'Modifica profilo' : 'Nuovo profilo' }}</h2>

        <label class="field"><span>Nome</span>
          <input [value]="form().name" (input)="patch({ name: $any($event.target).value })" />
        </label>

        <div class="row2">
          <label class="field"><span>Data di nascita</span>
            <input type="date" [value]="form().birthDate" (change)="patchTime({ birthDate: $any($event.target).value })" />
          </label>
          <label class="field"><span>Ora di nascita (locale)</span>
            <input type="time" [value]="form().birthTime" [disabled]="!form().birthTimeKnown"
                   (change)="patchTime({ birthTime: $any($event.target).value })" />
          </label>
        </div>
        <label class="check">
          <input type="checkbox" [checked]="!form().birthTimeKnown"
                 (change)="patchTime({ birthTimeKnown: !form().birthTimeKnown, birthTime: form().birthTimeKnown ? '12:00' : form().birthTime })" />
          non conosco l'ora (uso mezzogiorno: Ascendente e case non affidabili)
        </label>

        <div class="field"><span>Luogo di nascita</span>
          <div class="place">
            <input [value]="birthQuery()" (input)="search('birth', $any($event.target).value)" placeholder="Cerca città…" />
            @if (results().kind === 'birth' && results().places.length) {
              <ul class="suggest">
                @for (p of results().places; track $index) {
                  <li (click)="pickPlace('birth', p)">{{ placeLabel(p) }}</li>
                }
              </ul>
            }
          </div>
          @if (form().birthPlace; as bp) {
            <small class="muted">{{ bp.latitude | number: '1.4-4' }}, {{ bp.longitude | number: '1.4-4' }} · {{ bp.timeZone }}</small>
          }
        </div>

        <label class="field"><span>Fuso orario alla nascita (UTC {{ offsetLabel() }})</span>
          <input type="number" step="15" [value]="form().offsetMinutes"
                 (change)="patch({ offsetMinutes: +$any($event.target).value })" />
          <small class="muted">Minuti rispetto a UTC, ora legale compresa. Viene proposto in automatico: controllalo per le nascite prima degli anni '80.</small>
        </label>

        <label class="check">
          <input type="checkbox" [checked]="form().sameAsBirth" (change)="patch({ sameAsBirth: !form().sameAsBirth })" />
          vivo nel luogo di nascita
        </label>

        @if (!form().sameAsBirth) {
          <div class="field"><span>Dove vivi ora (luce del giorno, meteo)</span>
            <div class="place">
              <input [value]="currentQuery()" (input)="search('current', $any($event.target).value)" placeholder="Cerca città…" />
              @if (results().kind === 'current' && results().places.length) {
                <ul class="suggest">
                  @for (p of results().places; track $index) {
                    <li (click)="pickPlace('current', p)">{{ placeLabel(p) }}</li>
                  }
                </ul>
              }
            </div>
          </div>
        }

        <div class="row2">
          <label class="field"><span>Sistema di case</span>
            <select [value]="form().houseSystem" (change)="patch({ houseSystem: $any($event.target).value })">
              <option value="Placidus">Placidus</option>
              <option value="WholeSign">Segno intero (ellenistico)</option>
              <option value="Porphyry">Porfirio</option>
            </select>
          </label>
          <label class="field"><span>Regole (scuola)</span>
            <select [value]="form().ruleSetId" (change)="patch({ ruleSetId: $any($event.target).value })">
              @for (r of rulesets(); track r.id) { <option [value]="r.id">{{ r.name }}</option> }
            </select>
          </label>
        </div>

        <div class="actions">
          <button class="btn primary" (click)="save()" [disabled]="!valid() || saving()">Salva</button>
          @if (form().id) { <button class="btn" (click)="newProfile()">Nuovo profilo</button> }
          @if (error()) { <span class="error">{{ error() }}</span> }
        </div>
      </section>

      <section class="panel">
        @if (store.profiles().length > 1) {
          <h2>Profili</h2>
          <ul class="profiles">
            @for (p of store.profiles(); track p.id) {
              <li [class.current]="p.id === store.current()?.id">
                <button class="link" (click)="edit(p)">{{ p.name }}</button>
                <button class="link danger" (click)="remove(p)" title="Elimina">×</button>
              </li>
            }
          </ul>
        }

        @if (natal(); as n) {
          <h2>Tema natale <small>{{ n.houseSystem }}</small></h2>
          <table class="data">
            <thead><tr><th></th><th>Punto</th><th>Posizione</th><th class="num">Casa</th></tr></thead>
            <tbody>
              @for (p of n.points; track p.id) {
                <tr>
                  <td class="glyph">{{ glyph[p.id] }}</td>
                  <td>{{ p.label }}{{ p.retrograde ? ' ℞' : '' }}</td>
                  <td>{{ p.position }}</td>
                  <td class="num">{{ p.house }}</td>
                </tr>
              }
            </tbody>
          </table>
        } @else {
          <p class="muted">Il tema natale comparirà qui dopo il salvataggio.</p>
        }
      </section>
    </div>
  `,
})
export class ProfilePage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly store = inject(ProfileStore);

  protected readonly glyph = BODY_GLYPH;
  protected readonly form = signal<Form>(emptyForm());
  protected readonly rulesets = signal<RuleSetInfo[]>([]);
  protected readonly natal = signal<NatalChart | null>(null);
  protected readonly birthQuery = signal('');
  protected readonly currentQuery = signal('');
  protected readonly results = signal<{ kind: 'birth' | 'current' | null; places: Place[] }>({ kind: null, places: [] });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly valid = computed(() => {
    const f = this.form();
    return !!f.name.trim() && !!f.birthDate && !!f.birthPlace && (f.sameAsBirth || !!f.currentPlace);
  });

  protected readonly offsetLabel = computed(() => {
    const m = this.form().offsetMinutes;
    const sign = m < 0 ? '−' : '+';
    const a = Math.abs(m);
    return `${sign}${String(Math.floor(a / 60)).padStart(2, '0')}:${String(a % 60).padStart(2, '0')}`;
  });

  constructor() {
    void this.api.rulesets().then((r) => this.rulesets.set(r)).catch(() => {});
    // Load the active profile into the form once the store is ready.
    effect(() => {
      const p = this.store.current();
      if (p && untracked(() => this.form().id) === null) this.edit(p);
    });
  }

  protected patch(p: Partial<Form>) {
    this.form.update((f) => ({ ...f, ...p }));
  }

  /** Date/time changed: re-derive the UTC offset from the birth place's time zone. */
  protected patchTime(p: Partial<Form>) {
    this.patch(p);
    void this.refreshOffset();
  }

  protected search(kind: 'birth' | 'current', q: string) {
    (kind === 'birth' ? this.birthQuery : this.currentQuery).set(q);
    clearTimeout(this.searchTimer);
    if (q.trim().length < 2) {
      this.results.set({ kind: null, places: [] });
      return;
    }
    this.searchTimer = setTimeout(async () => {
      try {
        this.results.set({ kind, places: await this.api.geocode(q) });
      } catch {
        this.results.set({ kind: null, places: [] });
      }
    }, 300);
  }

  protected pickPlace(kind: 'birth' | 'current', p: Place) {
    this.results.set({ kind: null, places: [] });
    if (kind === 'birth') {
      this.birthQuery.set(this.placeLabel(p));
      this.patch({ birthPlace: p });
      void this.refreshOffset();
    } else {
      this.currentQuery.set(this.placeLabel(p));
      this.patch({ currentPlace: p });
    }
  }

  private async refreshOffset() {
    const f = this.form();
    if (!f.birthPlace?.timeZone || !f.birthDate) return;
    try {
      const r = await this.api.tzOffset(f.birthPlace.timeZone, `${f.birthDate}T${f.birthTime || '12:00'}:00`);
      this.patch({ offsetMinutes: r.offsetMinutes });
    } catch {
      /* keep manual value */
    }
  }

  protected placeLabel(p: Place) {
    return [p.name, p.region, p.country].filter(Boolean).join(', ');
  }

  protected edit(p: Profile) {
    const [date, time] = p.birthLocal.split('T');
    const birth: Place = { name: p.birthPlace, region: null, country: null, latitude: p.birthLatitude, longitude: p.birthLongitude, timeZone: p.birthTimeZone };
    const current: Place = { name: p.currentPlace, region: null, country: null, latitude: p.currentLatitude, longitude: p.currentLongitude, timeZone: p.currentTimeZone };
    const same = p.birthLatitude === p.currentLatitude && p.birthLongitude === p.currentLongitude;
    this.form.set({
      id: p.id,
      name: p.name,
      birthDate: date,
      birthTime: (time ?? '12:00').slice(0, 5),
      birthTimeKnown: p.birthTimeKnown,
      offsetMinutes: p.birthUtcOffsetMinutes,
      birthPlace: birth,
      currentPlace: current,
      sameAsBirth: same,
      houseSystem: p.houseSystem,
      ruleSetId: p.ruleSetId,
    });
    this.birthQuery.set(p.birthPlace);
    this.currentQuery.set(p.currentPlace);
    this.store.select(p.id);
    void this.api.natal(p.id).then((n) => this.natal.set(n)).catch(() => this.natal.set(null));
  }

  protected newProfile() {
    this.form.set({ ...emptyForm(), id: 0 });
    this.birthQuery.set('');
    this.currentQuery.set('');
    this.natal.set(null);
  }

  protected async save() {
    const f = this.form();
    if (!this.valid()) return;
    const birth = f.birthPlace!;
    const current = f.sameAsBirth ? birth : f.currentPlace!;
    const input: ProfileInput = {
      name: f.name.trim(),
      birthLocal: `${f.birthDate}T${f.birthTimeKnown ? f.birthTime || '12:00' : '12:00'}:00`,
      birthUtcOffsetMinutes: f.offsetMinutes,
      birthTimeKnown: f.birthTimeKnown,
      birthPlace: this.birthQuery() || birth.name,
      birthLatitude: birth.latitude,
      birthLongitude: birth.longitude,
      birthTimeZone: birth.timeZone,
      currentPlace: f.sameAsBirth ? this.birthQuery() || birth.name : this.currentQuery() || current.name,
      currentLatitude: current.latitude,
      currentLongitude: current.longitude,
      currentTimeZone: current.timeZone,
      houseSystem: f.houseSystem,
      ruleSetId: f.ruleSetId,
    };

    this.saving.set(true);
    this.error.set(null);
    try {
      const saved = f.id ? await this.api.updateProfile(f.id, input) : await this.api.createProfile(input);
      await this.store.load();
      this.edit(saved);
      if (!f.id) void this.router.navigate(['/']);
    } catch (e) {
      this.error.set('Salvataggio non riuscito.');
      console.error(e);
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(p: Profile) {
    if (!confirm(`Eliminare il profilo di ${p.name} e tutto il suo diario?`)) return;
    await this.api.deleteProfile(p.id);
    await this.store.load();
    const next = this.store.profiles()[0];
    if (next) this.edit(next);
    else this.newProfile();
  }
}

function emptyForm(): Form {
  return {
    id: null, name: '', birthDate: '', birthTime: '12:00', birthTimeKnown: true, offsetMinutes: 60,
    birthPlace: null, currentPlace: null, sameAsBirth: true, houseSystem: 'Placidus', ruleSetId: 'melothesia-classic',
  };
}
