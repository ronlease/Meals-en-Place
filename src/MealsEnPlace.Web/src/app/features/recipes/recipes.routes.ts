import { Routes } from '@angular/router';
import { RecipeBrowserComponent } from './recipe-browser.component';
import { RecipeCreateComponent } from './recipe-create.component';
import { RecipeImportComponent } from './recipe-import.component';

export const recipesRoutes: Routes = [
  {
    component: RecipeBrowserComponent,
    path: '',
  },
  {
    component: RecipeCreateComponent,
    path: 'create',
  },
  {
    component: RecipeImportComponent,
    path: 'import',
  },
];
