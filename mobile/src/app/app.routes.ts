import { Routes } from '@angular/router';

export const appRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/listen/listen.page').then((m) => m.ListenPage)
  },
  {
    path: 'settings',
    loadComponent: () => import('./pages/settings/settings.page').then((m) => m.SettingsPage)
  },
  {
    path: '**',
    redirectTo: ''
  }
];
