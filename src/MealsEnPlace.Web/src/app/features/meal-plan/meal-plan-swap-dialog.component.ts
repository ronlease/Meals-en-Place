import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatListModule, MatSelectionListChange } from '@angular/material/list';
import { RecipeListItemDto } from '../../core/models/recipe.models';

export interface SwapDialogData {
  currentRecipeId: string;
  currentRecipeTitle: string;
  /** True when the recipe fetch failed; the list is empty for that reason, not because none exist. */
  loadFailed: boolean;
  recipes: RecipeListItemDto[];
  /** Total recipes in the library. Larger than `recipes.length` means the list is a partial page. */
  totalCount: number;
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
        <p class="no-recipes">
          {{
            data.loadFailed
              ? 'Could not load recipes. Close and reopen to try again.'
              : 'No other recipes available.'
          }}
        </p>
      }
      @if (isPartialList) {
        <p class="partial-note">
          Showing {{ availableRecipes.length }} of {{ data.totalCount | number }} recipes.
          Search is not available yet — see MEP-046.
        </p>
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

      .partial-note {
        border-top: 1px solid var(--mat-sys-outline-variant, rgba(0, 0, 0, 0.12));
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        font-size: 12px;
        margin: 8px 0 0;
        padding-top: 8px;
      }
    `,
  ],
})
export class MealPlanSwapDialogComponent {
  readonly availableRecipes: RecipeListItemDto[];
  readonly data = inject<SwapDialogData>(MAT_DIALOG_DATA);
  readonly isPartialList: boolean;

  private readonly dialogRef = inject(MatDialogRef<MealPlanSwapDialogComponent>);

  constructor() {
    this.availableRecipes = this.data.recipes
      .filter((r) => r.id !== this.data.currentRecipeId && r.isFullyResolved)
      .sort((a, b) => a.title.localeCompare(b.title));

    // Compare against what was fetched, not against availableRecipes — the latter
    // is filtered to fully-resolved recipes, so it is smaller for reasons unrelated
    // to paging.
    this.isPartialList = this.data.recipes.length < this.data.totalCount;
  }

  onSelect(event: MatSelectionListChange): void {
    const selected = event.options?.[0]?.value;
    if (selected) {
      this.dialogRef.close(selected);
    }
  }
}
