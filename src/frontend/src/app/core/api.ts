import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CorrelationResponse,
  JournalDay,
  NatalChart,
  Place,
  Profile,
  ProfileInput,
  ReadingResponse,
  Catalog,
  TimelineResponse,
} from './models';

/** Thin typed client over the Dendera API (proxied to :5180 in dev, same origin in production). */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  private get<T>(url: string, params: Record<string, string | number | undefined> = {}): Promise<T> {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== '') p = p.set(k, v);
    return firstValueFrom(this.http.get<T>(`${this.base}${url}`, { params: p }));
  }

  profiles = () => this.get<Profile[]>('/profiles');
  createProfile = (p: ProfileInput) => firstValueFrom(this.http.post<Profile>(`${this.base}/profiles`, p));
  updateProfile = (id: number, p: ProfileInput) =>
    firstValueFrom(this.http.put<Profile>(`${this.base}/profiles/${id}`, p));
  deleteProfile = (id: number) => firstValueFrom(this.http.delete<void>(`${this.base}/profiles/${id}`));
  natal = (id: number) => this.get<NatalChart>(`/profiles/${id}/natal`);

  /** The active schools are the profile's own (the toggle saves them with setSchools). */
  reading = (id: number, at?: string, consensus?: string) =>
    this.get<ReadingResponse>(`/profiles/${id}/reading`, { at, consensus });

  setSchools = (id: number, schools: string[]) =>
    firstValueFrom(this.http.put<Profile>(`${this.base}/profiles/${id}/schools`, { schools }));

  timeline = (id: number, from: string, to: string, stepHours = 6, consensus?: string) =>
    this.get<TimelineResponse>(`/profiles/${id}/timeline`, { from, to, stepHours, consensus });

  journal = (id: number, from?: string, to?: string) => this.get<JournalDay[]>(`/profiles/${id}/journal`, { from, to });
  saveJournal = (id: number, day: JournalDay) =>
    firstValueFrom(this.http.put<JournalDay>(`${this.base}/profiles/${id}/journal/${day.date}`, day));
  deleteJournal = (id: number, date: string) =>
    firstValueFrom(this.http.delete<void>(`${this.base}/profiles/${id}/journal/${date}`));

  correlations = (id: number, consensus?: string) =>
    this.get<CorrelationResponse>(`/profiles/${id}/correlations`, { consensus });

  geocode = (q: string) => this.get<Place[]>('/geocode', { q });
  tzOffset = (tz: string, local: string) => this.get<{ offsetMinutes: number }>('/timezone/offset', { tz, local });
  catalog = () => this.get<Catalog>('/catalog');
}
