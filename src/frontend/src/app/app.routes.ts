import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/now').then((m) => m.Now), title: 'Dendera · Adesso' },
  { path: 'timeline', loadComponent: () => import('./pages/timeline').then((m) => m.Timeline), title: 'Dendera · Timeline' },
  { path: 'diario', loadComponent: () => import('./pages/journal').then((m) => m.Journal), title: 'Dendera · Diario' },
  {
    path: 'corrispondenze',
    loadComponent: () => import('./pages/insights').then((m) => m.Insights),
    title: 'Dendera · Corrispondenze',
  },
  { path: 'scuole', loadComponent: () => import('./pages/schools').then((m) => m.Schools), title: 'Dendera · Scuole' },
  { path: 'profilo', loadComponent: () => import('./pages/profile').then((m) => m.ProfilePage), title: 'Dendera · Profilo' },
  { path: '**', redirectTo: '' },
];
