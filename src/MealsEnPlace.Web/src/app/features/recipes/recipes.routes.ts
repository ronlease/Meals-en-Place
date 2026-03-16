import { Routes } from '@angular/router';
import { RecipeBrowserComponent } from './recipe-browser.component';
import { RecipeImportComponent } from './recipe-import.component';

export const recipesRoutes: Routes = [
  {
    component: RecipeBrowserComponent,
    path: '',
  },
  {
    component: RecipeImportComponent,
    path: 'import',
  },
];
