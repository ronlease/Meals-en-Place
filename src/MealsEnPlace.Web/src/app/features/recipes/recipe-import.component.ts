import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
  RecipeImportResultDto,
  RecipeSearchResultDto,
} from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';

type CardStatus = 'idle' | 'importing' | 'done' | 'conflict' | 'error';

@Component({
  selector: 'app-recipe-import',
  standalone: true,
  imports: [
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    ReactiveFormsModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Import Recipes</h1>
    </div>

    <!-- Search bar -->
    <div class="search-bar">
      <mat-form-field appearance="outline" class="search-field">
        <mat-label>Search TheMealDB by name</mat-label>
        <input
          matInput
          [formControl]="queryControl"
          (keydown.enter)="search()"
          placeholder="e.g. Chicken Tikka, Pasta..."
        />
        @if (queryControl.value) {
          <button matSuffix mat-icon-button (click)="clearSearch()" matTooltip="Clear">
            <mat-icon>close</mat-icon>
          </button>
        }
      </mat-form-field>
      <button
        mat-flat-button
        color="primary"
        (click)="search()"
        [disabled]="searchLoading() || queryControl.invalid"
      >
        @if (searchLoading()) {
          <mat-progress-spinner mode="indeterminate" diameter="18" />
        } @else {
          <ng-container><mat-icon>search</mat-icon></ng-container>
        }
        Search
      </button>
    </div>

    <!-- Search error -->
    @if (searchError()) {
      <div class="state-message error-message">
        <mat-icon>error_outline</mat-icon>
        <span>Search failed. Please try again.</span>
      </div>
    }

    <!-- No results -->
    @if (!searchLoading() && !searchError() && hasSearched() && results().length === 0) {
      <div class="state-message">
        <mat-icon>search_off</mat-icon>
        <span>No results found for "{{ lastQuery() }}".</span>
      </div>
    }

    <!-- Post-import resolution prompt -->
    @if (unresolvedAfterImport() > 0) {
      <div class="resolution-banner">
        <mat-icon>warning</mat-icon>
        <span>
          {{ unresolvedAfterImport() }} recipe{{ unresolvedAfterImport() === 1 ? '' : 's' }}
          need container sizes declared before they can be used for matching.
        </span>
        <button mat-flat-button color="warn" (click)="goToResolutionQueue()">
          Resolve Now
        </button>
      </div>
    }

    <!-- Results grid -->
    @if (results().length > 0) {
      <div class="results-grid">
        @for (r of results(); track r.id) {
          <mat-card class="recipe-card" [class.already-imported]="r.alreadyImported">
            @if (r.thumbnail) {
              <img mat-card-image [src]="r.thumbnail" [alt]="r.title" class="card-image" />
            } @else {
              <div class="card-image-placeholder">
                <mat-icon>no_photography</mat-icon>
              </div>
            }

            <mat-card-content>
              <div class="card-title">{{ r.title }}</div>
              <div class="card-category">{{ r.category }}</div>

              @if (r.alreadyImported) {
                <span class="status-chip imported-chip">Already Imported</span>
              }

              @let status = cardStatus(r.id);
              @if (status === 'done') {
                <span class="status-chip done-chip">
                  <mat-icon class="chip-icon">check_circle</mat-icon>
                  Imported
                </span>
              }
              @if (status === 'conflict') {
                <span class="status-chip conflict-chip">
                  <mat-icon class="chip-icon">info</mat-icon>
                  Already exists
                </span>
              }
              @if (status === 'error') {
                <span class="status-chip error-chip">
                  <mat-icon class="chip-icon">error</mat-icon>
                  Import failed
                </span>
              }
            </mat-card-content>

            <mat-card-actions>
              <button
                mat-flat-button
                color="primary"
                [disabled]="importDisabled(r)"
                (click)="importRecipe(r)"
              >
                @if (cardStatus(r.id) === 'importing') {
                  <mat-progress-spinner mode="indeterminate" diameter="18" />
                  Importing...
                } @else {
                  <ng-container><mat-icon>download</mat-icon> Import</ng-container>
                }
              </button>
            </mat-card-actions>
          </mat-card>
        }
      </div>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        padding: 24px;
      }

      .page-header {
        margin-bottom: 16px;
      }

      .page-title {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }

      .search-bar {
        display: flex;
        align-items: flex-start;
        gap: 12px;
        margin-bottom: 20px;
      }

      .search-field {
        flex: 1;
        max-width: 480px;
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

      .resolution-banner {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 12px 16px;
        background: #fef3c7;
        border: 1px solid #fde68a;
        border-radius: 8px;
        color: #92400e;
        margin-bottom: 20px;
        font-size: 14px;

        mat-icon { color: #d97706; }
        span { flex: 1; }
      }

      .results-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
        gap: 16px;
      }

      .recipe-card {
        display: flex;
        flex-direction: column;
        transition: opacity 0.2s;
      }

      .already-imported {
        opacity: 0.6;
      }

      .card-image {
        height: 160px;
        object-fit: cover;
      }

      .card-image-placeholder {
        height: 160px;
        background: #f3f4f6;
        display: flex;
        align-items: center;
        justify-content: center;
        color: rgba(0, 0, 0, 0.3);
        font-size: 40px;

        mat-icon { font-size: 40px; width: 40px; height: 40px; }
      }

      .card-title {
        font-weight: 500;
        font-size: 14px;
        margin-bottom: 4px;
        line-height: 1.3;
      }

      .card-category {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.54);
        margin-bottom: 8px;
      }

      .status-chip {
        display: inline-flex;
        align-items: center;
        gap: 4px;
        padding: 2px 8px;
        border-radius: 10px;
        font-size: 11px;
        font-weight: 500;
        margin-top: 4px;
      }

      .chip-icon {
        font-size: 13px;
        width: 13px;
        height: 13px;
      }

      .imported-chip { background: #e5e7eb; color: rgba(0, 0, 0, 0.54); }
      .done-chip { background: #dcfce7; color: #166534; }
      .conflict-chip { background: #fef3c7; color: #92400e; }
      .error-chip { background: #fee2e2; color: #991b1b; }
    `,
  ],
})
export class RecipeImportComponent {
  protected readonly hasSearched = signal(false);
  protected readonly lastQuery = signal('');
  protected readonly results = signal<RecipeSearchResultDto[]>([]);
  protected readonly searchError = signal(false);
  protected readonly searchLoading = signal(false);
  protected readonly unresolvedAfterImport = signal(0);

  readonly queryControl = new FormControl<string>('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(2)],
  });

  private readonly cardStatuses = signal<Map<string, CardStatus>>(new Map());
  private readonly importResults = signal<RecipeImportResultDto[]>([]);
  private readonly recipeService = inject(RecipeService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  cardStatus(id: string): CardStatus {
    return this.cardStatuses().get(id) ?? 'idle';
  }

  clearSearch(): void {
    this.queryControl.reset();
    this.hasSearched.set(false);
    this.results.set([]);
    this.searchError.set(false);
  }

  goToResolutionQueue(): void {
    this.router.navigate(['/recipes/resolve']);
  }

  importDisabled(r: RecipeSearchResultDto): boolean {
    const s = this.cardStatus(r.id);
    return (
      r.alreadyImported ||
      s === 'importing' ||
      s === 'done' ||
      s === 'conflict'
    );
  }

  importRecipe(r: RecipeSearchResultDto): void {
    this.setCardStatus(r.id, 'importing');

    this.recipeService.importRecipe(r.id).subscribe({
      error: (err) => {
        if (err?.status === 409) {
          this.setCardStatus(r.id, 'conflict');
          this.snackBar.open(
            `"${r.title}" has already been imported.`,
            'Dismiss',
            { duration: 4000 }
          );
        } else {
          this.setCardStatus(r.id, 'error');
          this.snackBar.open(
            `Failed to import "${r.title}".`,
            'Dismiss',
            { duration: 4000 }
          );
        }
      },
      next: (result) => {
        this.setCardStatus(r.id, 'done');
        this.importResults.update((list) => [...list, result]);
        this.recalcUnresolved();
        this.snackBar.open(`"${r.title}" imported successfully.`, undefined, {
          duration: 2500,
        });
      },
    });
  }

  search(): void {
    if (this.queryControl.invalid) {
      return;
    }
    const query = this.queryControl.value.trim();
    if (!query) {
      return;
    }
    this.searchError.set(false);
    this.searchLoading.set(true);
    this.hasSearched.set(true);
    this.lastQuery.set(query);

    this.recipeService.searchByQuery(query).subscribe({
      error: () => {
        this.searchLoading.set(false);
        this.searchError.set(true);
      },
      next: (results) => {
        this.searchLoading.set(false);
        this.results.set(results);
      },
    });
  }

  private recalcUnresolved(): void {
    const total = this.importResults().reduce(
      (sum, r) => sum + r.unresolvedCount,
      0
    );
    this.unresolvedAfterImport.set(total);
  }

  private setCardStatus(id: string, status: CardStatus): void {
    this.cardStatuses.update((map) => {
      const next = new Map(map);
      next.set(id, status);
      return next;
    });
  }
}
