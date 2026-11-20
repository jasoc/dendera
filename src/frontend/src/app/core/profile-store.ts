import { Injectable, computed, inject, signal } from '@angular/core';
import { Api } from './api';
import { Profile } from './models';

const KEY = 'dendera.profileId';

/** Which profile is active. Remembered per browser (a convenience only; data lives in the API). */
@Injectable({ providedIn: 'root' })
export class ProfileStore {
  private readonly api = inject(Api);

  readonly profiles = signal<Profile[]>([]);
  readonly loaded = signal(false);
  readonly currentId = signal<number | null>(readStored());
  readonly current = computed(
    () => this.profiles().find((p) => p.id === this.currentId()) ?? this.profiles()[0] ?? null,
  );
  /** Active schools of the current profile. Pages reload through current(), which changes on every toggle. */
  readonly schools = computed(() => this.current()?.schools ?? []);
  readonly saving = signal(false);

  async load(): Promise<void> {
    try {
      this.profiles.set(await this.api.profiles());
    } finally {
      this.loaded.set(true);
    }
  }

  /** Turns a school on/off for the current profile (saved, so correlations stay reproducible). Keeps at least one. */
  async toggle(schoolId: string): Promise<void> {
    const p = this.current();
    if (!p || this.saving()) return;
    const active = p.schools.includes(schoolId) ? p.schools.filter((s) => s !== schoolId) : [...p.schools, schoolId];
    if (active.length === 0) return;
    this.saving.set(true);
    try {
      this.replace(await this.api.setSchools(p.id, active));
    } finally {
      this.saving.set(false);
    }
  }

  replace(profile: Profile): void {
    this.profiles.update((list) => list.map((x) => (x.id === profile.id ? profile : x)));
  }

  select(id: number): void {
    this.currentId.set(id);
    try {
      localStorage.setItem(KEY, String(id));
    } catch {
      /* storage unavailable: fine */
    }
  }
}

function readStored(): number | null {
  try {
    const v = localStorage.getItem(KEY);
    return v ? Number(v) : null;
  } catch {
    return null;
  }
}
