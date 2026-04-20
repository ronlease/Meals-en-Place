import { Routes } from '@angular/router';
import { ContainerResolutionPageComponent } from './container-resolution-page.component';
import { RecipeBrowserComponent } from './recipe-browser.component';
import { RecipeCreateComponent } from './recipe-create.component';

export const recipesRoutes: Routes = [
  {
    component: RecipeBrowserComponent,
    path: '',
  },
  {
    component: ContainerResolutionPageComponent,
    path: 'container-resolution',
  },
  {
    component: RecipeCreateComponent,
    path: 'create',
  },
];
