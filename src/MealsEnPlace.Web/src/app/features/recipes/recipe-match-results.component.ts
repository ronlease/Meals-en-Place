import { Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterModule } from '@angular/router';
import {
  RecipeMatchDto,
  RecipeMatchResponse,
} from '../../core/models/recipe.models';

@Component({
  selector: 'app-recipe-match-results',
  standalone: true,
  imports: [MatChipsModule, MatIconModule, MatProgressSpinnerModule, RouterModule],
  template: `
    @if (loading()) {
      <div class="spinner-container">
        <mat-progress-spinner mode="indeterminate" diameter="48" />
      </div>
    } @else if (error()) {
      <div class="state-message error-message">
        <mat-icon>error_outline</mat-icon>
        <span>Failed to load matches. Please try again.</span>
      </div>
    } @else if (!results()) {
      <div class="state-message">
        <mat-icon>search</mat-icon>
        <span>Click "Find Matches" to see what you can make with your current inventory.</span>
      </div>
    } @else if (totalCount() === 0) {
      <div class="state-message">
        <mat-icon>no_meals</mat-icon>
        <span>No matches found. Try adding more items to your inventory.</span>
      </div>
    } @else {
      @if (!results()!.claudeFeasibilityApplied) {
        <div class="ai-disabled-note" role="status" aria-live="polite">
          <mat-icon fontIcon="info" inline></mat-icon>
          <span>
            AI-suggested substitutions are unavailable — add a Claude API key in
            Settings to enable them. Deterministic matching is unchanged.
          </span>
        </div>
      }
      @if (results()!.fullMatches.length > 0) {
        <section class="match-section">
          <h2 class="section-heading full-match-heading">
            <mat-icon>check_circle</mat-icon>
            Full Matches ({{ results()!.fullMatches.length }})
          </h2>
          <div class="match-grid">
            @for (recipe of results()!.fullMatches; track recipe.recipeId) {
              <div class="match-card full-match">
                <div class="match-card-header">
                  <span class="match-title">{{ recipe.title }}</span>
                  <span class="match-pill full-match-pill">Full Match</span>
                </div>
                <div class="match-meta">
                  <span>{{ recipe.cuisineType }}</span>
                  <span class="dot-sep">·</span>
                  <span>{{ recipe.matchedIngredients.length }} / {{ totalIngredients(recipe) }} ingredients</span>
                  <span class="dot-sep">·</span>
                  <span>{{ scorePercent(recipe.finalScore) }}%</span>
                </div>
              </div>
            }
          </div>
        </section>
      }

      @if (results()!.nearMatches.length > 0) {
        <section class="match-section">
          <h2 class="section-heading near-match-heading">
            <mat-icon>tune</mat-icon>
            Near Matches ({{ results()!.nearMatches.length }})
          </h2>
          <div class="match-grid">
            @for (recipe of results()!.nearMatches; track recipe.recipeId) {
              <div class="match-card near-match">
                <div class="match-card-header">
                  <span class="match-title">{{ recipe.title }}</span>
                  <span class="match-pill near-match-pill">Near Match</span>
                </div>
                <div class="match-meta">
                  <span>{{ recipe.cuisineType }}</span>
                  <span class="dot-sep">·</span>
                  <span>{{ recipe.matchedIngredients.length }} / {{ totalIngredients(recipe) }} ingredients</span>
                  <span class="dot-sep">·</span>
                  <span>{{ scorePercent(recipe.finalScore) }}%</span>
                </div>
                @if (recipe.missingIngredients.length > 0) {
                  <div class="missing-list">
                    <span class="missing-label">Missing:</span>
                    @for (m of recipe.missingIngredients; track m.ingredientName) {
                      <span class="missing-chip">{{ m.ingredientName }}</span>
                    }
                  </div>
                }
                @if (recipe.substitutionSuggestions.length > 0) {
                  <div class="sub-list">
                    <span class="sub-label">Substitutions:</span>
                    @for (s of recipe.substitutionSuggestions; track s.missingIngredientName) {
                      <div class="sub-row">
                        <mat-icon class="sub-icon">swap_horiz</mat-icon>
                        <span>{{ s.missingIngredientName }} → {{ s.suggestedSubstitute }}</span>
                        @if (s.notes) {
                          <span class="sub-notes">({{ s.notes }})</span>
                        }
                      </div>
                    }
                  </div>
                }
              </div>
            }
          </div>
        </section>
      }

      @if (results()!.partialMatches.length > 0) {
        <section class="match-section">
          <h2 class="section-heading partial-match-heading">
            <mat-icon>incomplete_circle</mat-icon>
            Partial Matches ({{ results()!.partialMatches.length }})
          </h2>
          <div class="match-grid">
            @for (recipe of results()!.partialMatches; track recipe.recipeId) {
              <div class="match-card partial-match">
                <div class="match-card-header">
                  <span class="match-title">{{ recipe.title }}</span>
                  <span class="match-pill partial-match-pill">Partial Match</span>
                </div>
                <div class="match-meta">
                  <span>{{ recipe.cuisineType }}</span>
                  <span class="dot-sep">·</span>
                  <span>{{ recipe.matchedIngredients.length }} / {{ totalIngredients(recipe) }} ingredients</span>
                  <span class="dot-sep">·</span>
                  <span>{{ scorePercent(recipe.finalScore) }}%</span>
                </div>
              </div>
            }
          </div>
        </section>
      }
    }
  `,
  styles: [
    `
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

      .ai-disabled-note {
        align-items: center;
        background: rgba(230, 150, 60, 0.08);
        border-left: 3px solid rgba(230, 150, 60, 0.6);
        border-radius: 4px;
        color: #8a5a14;
        display: flex;
        font-size: 13px;
        gap: 8px;
        margin-bottom: 16px;
        padding: 10px 12px;
      }

      .match-section {
        margin-bottom: 32px;
      }

      .section-heading {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 16px;
        font-weight: 500;
        margin: 0 0 12px;
      }

      .full-match-heading { color: #166534; }
      .near-match-heading { color: #92400e; }
      .partial-match-heading { color: rgba(0, 0, 0, 0.54); }

      .match-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
        gap: 12px;
      }

      .match-card {
        border-radius: 8px;
        padding: 14px 16px;
        border: 1px solid;
      }

      .full-match { border-color: #bbf7d0; background: #f0fdf4; }
      .near-match { border-color: #fde68a; background: #fffbeb; }
      .partial-match { border-color: #e5e7eb; background: #f9fafb; }

      .match-card-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
        margin-bottom: 6px;
      }

      .match-title {
        font-weight: 500;
        font-size: 14px;
        flex: 1;
      }

      .match-pill {
        font-size: 11px;
        font-weight: 600;
        padding: 2px 8px;
        border-radius: 10px;
        white-space: nowrap;
      }

      .full-match-pill { background: #dcfce7; color: #166534; }
      .near-match-pill { background: #fef3c7; color: #92400e; }
      .partial-match-pill { background: #f3f4f6; color: rgba(0, 0, 0, 0.54); }

      .match-meta {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.6);
        display: flex;
        gap: 4px;
        flex-wrap: wrap;
      }

      .dot-sep { opacity: 0.4; }

      .missing-list {
        margin-top: 8px;
        display: flex;
        flex-wrap: wrap;
        gap: 4px;
        align-items: center;
        font-size: 12px;
      }

      .missing-label {
        font-weight: 500;
        color: #b91c1c;
        margin-right: 2px;
      }

      .missing-chip {
        background: #fee2e2;
        color: #991b1b;
        border-radius: 10px;
        padding: 1px 7px;
      }

      .sub-list {
        margin-top: 8px;
        font-size: 12px;
      }

      .sub-label {
        font-weight: 500;
        color: #78350f;
        display: block;
        margin-bottom: 4px;
      }

      .sub-row {
        display: flex;
        align-items: center;
        gap: 4px;
        margin-bottom: 2px;
      }

      .sub-icon {
        font-size: 14px;
        width: 14px;
        height: 14px;
        color: #92400e;
      }

      .sub-notes {
        color: rgba(0, 0, 0, 0.54);
        font-style: italic;
      }
    `,
  ],
})
export class RecipeMatchResultsComponent {
  readonly error = input(false);
  readonly loading = input(false);
  readonly results = input<RecipeMatchResponse | null>(null);
  readonly totalCount = computed(() => {
    const r = this.results();
    if (!r) return 0;
    return r.fullMatches.length + r.nearMatches.length + r.partialMatches.length;
  });

  scorePercent(score: number): number {
    return Math.round(score * 100);
  }

  totalIngredients(recipe: RecipeMatchDto): number {
    return recipe.matchedIngredients.length + recipe.missingIngredients.length;
  }
}
