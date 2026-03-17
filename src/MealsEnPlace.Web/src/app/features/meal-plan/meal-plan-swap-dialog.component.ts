import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatListModule } from '@angular/material/list';
import { RecipeListItemDto } from '../../core/models/recipe.models';

export interface SwapDialogData {
  currentRecipeId: string;
  currentRecipeTitle: string;
  recipes: RecipeListItemDto[];
}

@Component({
  selector: 'app-meal-plan-swap-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatDialogModule, MatListModule],
  template: `
    <h2 mat-dialog-title>Swap Recipe</h2>
    <mat-dialog-content>
      <p class="current-label">
        Current: <strong>{{ data.currentRecipeTitle }}</strong>
      </p>
      <mat-selection-list [multiple]="false" (selectionChange)="onSelect($event)">
        @for (recipe of availableRecipes; track recipe.id) {
          <mat-list-option [value]="recipe.id">
            {{ recipe.title }}
            <span class="cuisine-tag">{{ recipe.cuisineType }}</span>
          </mat-list-option>
        }
      </mat-selection-list>
      @if (availableRecipes.length === 0) {
        <p class="no-recipes">No other recipes available.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      mat-dialog-content {
        min-width: 320px;
        max-height: 400px;
      }

      .current-label {
        margin-bottom: 12px;
        font-size: 14px;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
      }

      .cuisine-tag {
        margin-left: 8px;
        font-size: 12px;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
      }

      .no-recipes {
        padding: 16px;
        text-align: center;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
      }
    `,
  ],
})
export class MealPlanSwapDialogComponent {
  readonly data = inject<SwapDialogData>(MAT_DIALOG_DATA);
  readonly availableRecipes: RecipeListItemDto[];

  private readonly dialogRef = inject(MatDialogRef<MealPlanSwapDialogComponent>);

  constructor() {
    this.availableRecipes = this.data.recipes
      .filter((r) => r.id !== this.data.currentRecipeId && r.isFullyResolved)
      .sort((a, b) => a.title.localeCompare(b.title));
  }

  onSelect(event: any): void {
    const selected = event.options?.[0]?.value;
    if (selected) {
      this.dialogRef.close(selected);
    }
  }
}
