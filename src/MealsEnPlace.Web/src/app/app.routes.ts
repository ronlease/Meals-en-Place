import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    loadChildren: () =>
      import('./features/inventory/inventory.routes').then(
        (m) => m.inventoryRoutes
      ),
    path: 'inventory',
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'inventory',
  },
];
