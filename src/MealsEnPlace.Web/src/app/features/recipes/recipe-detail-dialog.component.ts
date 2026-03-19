import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RecipeDetailDto } from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';

@Component({
  selector: 'app-recipe-detail-dialog',
  standalone: true,
  imports: [
    MatButtonModule,
    MatChipsModule,
    MatDialogModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
  ],
  template: `
    <h2 mat-dialog-title>
      @if (recipe()) {
        {{ recipe()!.title }}
      } @else {
        Recipe Detail
      }
    </h2>

    <mat-dialog-content>
      @if (loading()) {
        <div class="spinner-container">
          <mat-progress-spinner mode="indeterminate" diameter="48" />
        </div>
      } @else if (error()) {
        <div class="error-message">
          <mat-icon>error_outline</mat-icon>
          Failed to load recipe detail.
        </div>
      } @else if (recipe(); as r) {
        <div class="recipe-meta">
          @if (r.cuisineType) {
            <span class="meta-chip cuisine-chip">{{ r.cuisineType }}</span>
          }
          @for (tag of r.dietaryTags; track tag) {
            <span class="meta-chip dietary-chip">{{ tag }}</span>
          }
          <span class="meta-chip serving-chip">{{ r.servingCount }} servings</span>
        </div>

        <h3>Ingredients</h3>
        <mat-table [dataSource]="r.ingredients" class="ingredient-table">
          <ng-container matColumnDef="ingredientName">
            <mat-header-cell *matHeaderCellDef>Ingredient</mat-header-cell>
            <mat-cell *matCellDef="let i">
              {{ i.ingredientName }}
              @if (i.notes) {
                <mat-icon
                  class="notes-icon"
                  [matTooltip]="'Original: ' + i.notes"
                >info_outline</mat-icon>
              }
            </mat-cell>
          </ng-container>

          <ng-container matColumnDef="quantity">
            <mat-header-cell *matHeaderCellDef>Qty</mat-header-cell>
            <mat-cell *matCellDef="let i">{{ i.quantity }}</mat-cell>
          </ng-container>

          <ng-container matColumnDef="uomAbbreviation">
            <mat-header-cell *matHeaderCellDef>Unit</mat-header-cell>
            <mat-cell *matCellDef="let i">{{ i.uomAbbreviation }}</mat-cell>
          </ng-container>

          <ng-container matColumnDef="status">
            <mat-header-cell *matHeaderCellDef>Status</mat-header-cell>
            <mat-cell *matCellDef="let i">
              @if (i.isContainerResolved) {
                <mat-icon class="resolved-icon">check_circle</mat-icon>
              } @else {
                <mat-icon class="unresolved-icon" matTooltip="Needs container size declaration">warning</mat-icon>
              }
            </mat-cell>
          </ng-container>

          <mat-header-row *matHeaderRowDef="ingredientColumns" />
          <mat-row *matRowDef="let row; columns: ingredientColumns;" />
        </mat-table>

        @if (r.instructions) {
          <h3>Instructions</h3>
          <p class="instructions-text">{{ r.instructions }}</p>
        }

        @if (r.sourceUrl) {
          <div class="source-link">
            <a [href]="r.sourceUrl" target="_blank" rel="noopener noreferrer">
              <mat-icon>open_in_new</mat-icon>
              View Original Source
            </a>
          </div>
        }
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      @if (recipe() && recipe()!.isFullyResolved) {
        <button
          mat-flat-button
          color="accent"
          (click)="addToShoppingList()"
          [disabled]="addingToList()"
        >
          @if (addingToList()) {
            <mat-progress-spinner mode="indeterminate" diameter="18" />
          } @else {
            <mat-icon>shopping_cart</mat-icon>
          }
          Add to Shopping List
        </button>
      }
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .spinner-container {
        display: flex;
        justify-content: center;
        padding: 48px;
      }

      .error-message {
        display: flex;
        align-items: center;
        gap: 8px;
        color: #b91c1c;
        padding: 24px;
      }

      .recipe-meta {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
        margin-bottom: 16px;
      }

      .meta-chip {
        display: inline-block;
        padding: 2px 10px;
        border-radius: 12px;
        font-size: 12px;
        font-weight: 500;
      }

      .cuisine-chip {
        background: #dbeafe;
        color: #1e40af;
      }

      .dietary-chip {
        background: #ede9fe;
        color: #5b21b6;
      }

      .serving-chip {
        background: #f3f4f6;
        color: #374151;
      }

      .ingredient-table {
        width: 100%;
        margin-bottom: 16px;
      }

      .notes-icon {
        font-size: 16px;
        height: 16px;
        width: 16px;
        margin-left: 4px;
        vertical-align: middle;
        opacity: 0.5;
        cursor: help;
      }

      .resolved-icon {
        color: #16a34a;
        font-size: 18px;
      }

      .unresolved-icon {
        color: #dc2626;
        font-size: 18px;
      }

      h3 {
        margin: 16px 0 8px;
        font-size: 16px;
        font-weight: 500;
      }

      .instructions-text {
        white-space: pre-wrap;
        line-height: 1.6;
        font-size: 14px;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.7));
      }

      .source-link {
        margin-top: 16px;

        a {
          display: inline-flex;
          align-items: center;
          gap: 4px;
          color: #2563eb;
          text-decoration: none;
          font-size: 14px;

          &:hover {
            text-decoration: underline;
          }
        }
      }
    `,
  ],
})
export class RecipeDetailDialogComponent implements OnInit {
  protected readonly addingToList = signal(false);
  protected readonly error = signal(false);
  protected readonly ingredientColumns = [
    'ingredientName',
    'quantity',
    'uomAbbreviation',
    'status',
  ];
  protected readonly loading = signal(false);
  protected readonly recipe = signal<RecipeDetailDto | null>(null);

  private readonly data: { recipeId: string } = inject(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<RecipeDetailDialogComponent>);
  private readonly recipeService = inject(RecipeService);
  private readonly snackBar = inject(MatSnackBar);

  addToShoppingList(): void {
    this.addingToList.set(true);
    this.recipeService.addToShoppingList(this.data.recipeId).subscribe({
      error: () => {
        this.addingToList.set(false);
        this.snackBar.open('Failed to add to shopping list.', 'Dismiss', {
          duration: 4000,
        });
      },
      next: (items) => {
        this.addingToList.set(false);
        this.snackBar.open(
          `Added ${items.length} item(s) to shopping list.`,
          'OK',
          { duration: 3000 }
        );
      },
    });
  }

  ngOnInit(): void {
    this.loading.set(true);
    this.recipeService.getRecipeDetail(this.data.recipeId).subscribe({
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
      next: (detail) => {
        this.loading.set(false);
        this.recipe.set(detail);
      },
    });
  }
}
