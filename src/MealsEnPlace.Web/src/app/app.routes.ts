import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    loadChildren: () =>
      import('./features/expiration/expiration.routes').then(
        (m) => m.expirationRoutes
      ),
    path: 'expiration',
  },
  {
    loadChildren: () =>
      import('./features/inventory/inventory.routes').then(
        (m) => m.inventoryRoutes
      ),
    path: 'inventory',
  },
  {
    loadChildren: () =>
      import('./features/recipes/recipes.routes').then(
        (m) => m.recipesRoutes
      ),
    path: 'recipes',
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'inventory',
  },
];
