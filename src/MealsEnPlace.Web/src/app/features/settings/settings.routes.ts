import { Routes } from '@angular/router';

export const settingsRoutes: Routes = [
  {
    loadComponent: () =>
      import('./settings-page.component').then((m) => m.SettingsPageComponent),
    path: '',
  },
];
