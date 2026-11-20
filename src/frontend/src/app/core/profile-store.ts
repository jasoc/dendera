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

  async load(): Promise<void> {
    try {
      this.profiles.set(await this.api.profiles());
    } finally {
      this.loaded.set(true);
    }
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
