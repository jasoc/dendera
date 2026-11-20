import { Injectable, computed, inject, signal } from '@angular/core';
import { Api } from './api';
import { Catalog, CatalogSchool, InfluenceInfo } from './models';

/** What the backend implements: influences, schools, body zones. Loaded once, refreshable. */
@Injectable({ providedIn: 'root' })
export class CatalogStore {
  private readonly api = inject(Api);

  readonly catalog = signal<Catalog | null>(null);
  readonly schools = computed(() => this.catalog()?.schools ?? []);
  readonly influences = computed(() => this.catalog()?.influences ?? []);

  private readonly schoolById = computed(() => new Map(this.schools().map((s) => [s.id, s])));
  private readonly influenceById = computed(() => new Map(this.influences().map((i) => [i.info.id, i.info])));

  async load(): Promise<void> {
    try {
      this.catalog.set(await this.api.catalog());
    } catch (e) {
      console.error('Catalogo non disponibile', e);
    }
  }

  school(id: string): CatalogSchool | undefined {
    return this.schoolById().get(id);
  }

  influence(id: string): InfluenceInfo | undefined {
    return this.influenceById().get(id);
  }
}

/** Short school names for chips and narrow places. */
export const SCHOOL_SHORT: Record<string, string> = {
  melothesia: 'Melotesia',
  biometeorology: 'Biometeo',
  tcm: 'MTC',
  'human-design': 'Human Design',
  'tantric-yoga': 'Yoga',
};

export const PROVIDER_KIND: Record<string, string> = { library: 'libreria', api: 'API', computed: 'calcolato' };

/** "zone:{school}:{zone}" → school id, for per-school zone tracks (timeline, correlations). */
export function schoolOfTrack(key: string): string | null {
  const parts = key.split(':');
  return parts[0] === 'zone' && parts.length === 3 ? parts[1] : null;
}
