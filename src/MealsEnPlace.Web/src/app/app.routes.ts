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
      import('./features/meal-plan/meal-plan.routes').then(
        (m) => m.mealPlanRoutes
      ),
    path: 'meal-plan',
  },
  {
    loadChildren: () =>
      import('./features/recipes/recipes.routes').then(
        (m) => m.recipesRoutes
      ),
    path: 'recipes',
  },
  {
    loadChildren: () =>
      import('./features/seasonal-produce/seasonal-produce.routes').then(
        (m) => m.seasonalProduceRoutes
      ),
    path: 'seasonal-produce',
  },
  {
    loadChildren: () =>
      import('./features/settings/settings.routes').then(
        (m) => m.settingsRoutes
      ),
    path: 'settings',
  },
  {
    loadChildren: () =>
      import('./features/shopping-list/shopping-list.routes').then(
        (m) => m.shoppingListRoutes
      ),
    path: 'shopping-list',
  },
  {
    loadChildren: () =>
      import('./features/waste-alerts/waste-alerts.routes').then(
        (m) => m.wasteAlertsRoutes
      ),
    path: 'waste-alerts',
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'inventory',
  },
];
