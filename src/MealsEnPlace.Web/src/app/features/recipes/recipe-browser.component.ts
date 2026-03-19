import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterModule } from '@angular/router';
import {
  DietaryTag,
  RecipeListItemDto,
  RecipeMatchResponse,
} from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';
import { RecipeDetailDialogComponent } from './recipe-detail-dialog.component';
import { RecipeMatchResultsComponent } from './recipe-match-results.component';

@Component({
  selector: 'app-recipe-browser',
  standalone: true,
  imports: [
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
    RecipeMatchResultsComponent,
    RouterModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Recipes</h1>
      <div class="header-actions">
        <button mat-flat-button color="primary" routerLink="/recipes/create">
          <mat-icon>add</mat-icon>
          Create Recipe
        </button>
        <button mat-stroked-button routerLink="/recipes/import">
          <mat-icon>cloud_download</mat-icon>
          Import Recipes
        </button>
      </div>
    </div>

    <mat-tab-group animationDuration="200ms">
      <!-- ── My Recipes tab ── -->
      <mat-tab label="My Recipes">
        <ng-template matTabContent>
          <div class="tab-content">
            @if (libraryLoading()) {
              <div class="spinner-container">
                <mat-progress-spinner mode="indeterminate" diameter="48" />
              </div>
            } @else if (libraryError()) {
              <div class="state-message error-message">
                <mat-icon>error_outline</mat-icon>
                <span>Failed to load recipes. Please try again.</span>
                <button mat-button color="primary" (click)="loadLibrary()">Retry</button>
              </div>
            } @else if (library().length === 0) {
              <div class="state-message">
                <mat-icon>menu_book</mat-icon>
                <span>No recipes yet. Import some to get started.</span>
              </div>
            } @else {
              <mat-table [dataSource]="library()" class="recipe-table">
                <ng-container matColumnDef="title">
                  <mat-header-cell *matHeaderCellDef>Title</mat-header-cell>
                  <mat-cell *matCellDef="let r">{{ r.title }}</mat-cell>
                </ng-container>

                <ng-container matColumnDef="cuisineType">
                  <mat-header-cell *matHeaderCellDef>Cuisine</mat-header-cell>
                  <mat-cell *matCellDef="let r">{{ r.cuisineType }}</mat-cell>
                </ng-container>

                <ng-container matColumnDef="totalIngredients">
                  <mat-header-cell *matHeaderCellDef>Ingredients</mat-header-cell>
                  <mat-cell *matCellDef="let r">{{ r.totalIngredients }}</mat-cell>
                </ng-container>

                <ng-container matColumnDef="dietaryTags">
                  <mat-header-cell *matHeaderCellDef>Dietary Tags</mat-header-cell>
                  <mat-cell *matCellDef="let r">
                    @for (tag of r.dietaryTags; track tag) {
                      <span class="dietary-chip">{{ tag }}</span>
                    }
                  </mat-cell>
                </ng-container>

                <ng-container matColumnDef="status">
                  <mat-header-cell *matHeaderCellDef>Status</mat-header-cell>
                  <mat-cell *matCellDef="let r">
                    @if (r.isFullyResolved) {
                      <span class="status-badge resolved-badge">Resolved</span>
                    } @else {
                      <span
                        class="status-badge awaiting-badge"
                        [matTooltip]="r.unresolvedCount + ' container reference(s) need declaration'"
                      >
                        Awaiting Resolution ({{ r.unresolvedCount }})
                      </span>
                    }
                  </mat-cell>
                </ng-container>

                <mat-header-row *matHeaderRowDef="libraryColumns" />
                <mat-row
                  *matRowDef="let row; columns: libraryColumns;"
                  [class.awaiting-row]="!row.isFullyResolved"
                  class="clickable-row"
                  (click)="openRecipeDetail(row)"
                />
              </mat-table>
            }
          </div>
        </ng-template>
      </mat-tab>

      <!-- ── What Can I Make? tab ── -->
      <mat-tab label="What Can I Make?">
        <ng-template matTabContent>
          <div class="tab-content">
            <div class="match-toolbar">
              <button
                mat-flat-button
                color="primary"
                (click)="findMatches()"
                [disabled]="matchLoading()"
              >
                @if (matchLoading()) {
                  <mat-progress-spinner mode="indeterminate" diameter="18" />
                } @else {
                  <mat-icon>search</mat-icon>
                }
                Find Matches
              </button>
              <mat-chip-listbox
                [multiple]="true"
                (change)="onDietaryFilterChange($event)"
                aria-label="Dietary tag filter"
              >
                @for (tag of allDietaryTags; track tag) {
                  <mat-chip-option [value]="tag">{{ tag }}</mat-chip-option>
                }
              </mat-chip-listbox>
            </div>

            <app-recipe-match-results
              [loading]="matchLoading()"
              [error]="matchError()"
              [results]="matchResults()"
            />
          </div>
        </ng-template>
      </mat-tab>
    </mat-tab-group>
  `,
  styles: [
    `
      :host {
        display: block;
        padding: 24px;
      }

      .page-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 16px;
      }

      .page-title {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }

      .tab-content {
        padding-top: 20px;
      }

      .spinner-container {
        display: flex;
        justify-content: center;
        padding: 48px;
      }

      .state-message {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 32px 16px;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        font-size: 14px;

        mat-icon { opacity: 0.5; }
      }

      .error-message {
        color: #b91c1c;
        mat-icon { opacity: 1; color: #b91c1c; }
      }

      .recipe-table {
        width: 100%;
      }

      .status-badge {
        display: inline-block;
        padding: 2px 10px;
        border-radius: 12px;
        font-size: 12px;
        font-weight: 500;
        white-space: nowrap;
      }

      .resolved-badge {
        background: #dcfce7;
        color: #166534;
      }

      .awaiting-badge {
        background: #fee2e2;
        color: #991b1b;
        cursor: default;
      }

      .awaiting-row {
        opacity: 0.7;
      }

      .clickable-row {
        cursor: pointer;
      }

      .clickable-row:hover {
        background: rgba(0, 0, 0, 0.04);
      }

      .header-actions {
        display: flex;
        gap: 8px;
      }

      .match-toolbar {
        display: flex;
        align-items: center;
        gap: 12px;
        margin-bottom: 20px;
        flex-wrap: wrap;
      }

      .dietary-chip {
        display: inline-block;
        padding: 1px 7px;
        border-radius: 10px;
        font-size: 11px;
        font-weight: 500;
        background: #ede9fe;
        color: #5b21b6;
        margin-right: 4px;
      }
    `,
  ],
})
export class RecipeBrowserComponent implements OnInit {
  readonly allDietaryTags: DietaryTag[] = [
    'Vegetarian', 'Vegan', 'Carnivore', 'LowCarb', 'GlutenFree', 'DairyFree',
  ];
  protected readonly libraryColumns = [
    'title',
    'cuisineType',
    'dietaryTags',
    'totalIngredients',
    'status',
  ];
  protected readonly libraryError = signal(false);
  protected readonly libraryLoading = signal(false);
  protected readonly library = signal<RecipeListItemDto[]>([]);
  protected readonly matchError = signal(false);
  protected readonly matchLoading = signal(false);
  protected readonly matchResults = signal<RecipeMatchResponse | null>(null);

  private readonly dialog = inject(MatDialog);
  private readonly recipeService = inject(RecipeService);
  private readonly snackBar = inject(MatSnackBar);
  private selectedDietaryTags: string[] = [];

  findMatches(): void {
    this.matchError.set(false);
    this.matchLoading.set(true);
    this.recipeService.matchRecipes(undefined, this.selectedDietaryTags.length > 0 ? this.selectedDietaryTags : undefined).subscribe({
      error: () => {
        this.matchLoading.set(false);
        this.matchError.set(true);
        this.snackBar.open('Failed to load matches.', 'Dismiss', {
          duration: 4000,
        });
      },
      next: (results) => {
        this.matchLoading.set(false);
        this.matchResults.set(results);
      },
    });
  }

  loadLibrary(): void {
    this.libraryError.set(false);
    this.libraryLoading.set(true);
    this.recipeService.getRecipes().subscribe({
      error: () => {
        this.libraryLoading.set(false);
        this.libraryError.set(true);
      },
      next: (recipes) => {
        this.libraryLoading.set(false);
        this.library.set(recipes);
      },
    });
  }

  ngOnInit(): void {
    this.loadLibrary();
  }

  onDietaryFilterChange(event: any): void {
    this.selectedDietaryTags = event.value ?? [];
  }

  openRecipeDetail(recipe: RecipeListItemDto): void {
    this.dialog.open(RecipeDetailDialogComponent, {
      data: { recipeId: recipe.id },
      maxWidth: '700px',
      width: '90vw',
    });
  }
}
