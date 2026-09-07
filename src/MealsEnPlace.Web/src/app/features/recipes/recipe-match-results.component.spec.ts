import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RecipeMatchDto, RecipeMatchResponse } from '../../core/models/recipe.models';
import { RecipeMatchResultsComponent } from './recipe-match-results.component';

describe('RecipeMatchResultsComponent', () => {
  let component: RecipeMatchResultsComponent;
  let fixture: ComponentFixture<RecipeMatchResultsComponent>;

  function makeMatch(overrides: Partial<RecipeMatchDto> = {}): RecipeMatchDto {
    return {
      cuisineType: 'Italian',
      finalScore: 1,
      matchScore: 1,
      matchTier: 'FullMatch',
      matchedIngredients: [],
      missingIngredients: [],
      recipeId: 'recipe-1',
      substitutionSuggestions: [],
      title: 'Marinara',
      ...overrides,
    };
  }

  function makeResults(overrides: Partial<RecipeMatchResponse> = {}): RecipeMatchResponse {
    return {
      claudeFeasibilityApplied: true,
      fullMatches: [],
      nearMatches: [],
      partialMatches: [],
      ...overrides,
    };
  }

  function createComponent(inputs: {
    error?: boolean;
    loading?: boolean;
    results?: RecipeMatchResponse | null;
  }): void {
    TestBed.configureTestingModule({
      imports: [RecipeMatchResultsComponent, NoopAnimationsModule],
    });

    fixture = TestBed.createComponent(RecipeMatchResultsComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('error', inputs.error ?? false);
    fixture.componentRef.setInput('loading', inputs.loading ?? false);
    fixture.componentRef.setInput('results', inputs.results ?? null);
    fixture.detectChanges();
  }

  function text(): string {
    return fixture.nativeElement.textContent as string;
  }

  // ── State precedence ────────────────────────────────────────────────────────
  //
  // The template is a single @if chain, so the order the states win matters.

  describe('states', () => {
    it('shows the spinner while loading', () => {
      createComponent({ loading: true });

      expect(fixture.nativeElement.querySelector('mat-progress-spinner')).not.toBeNull();
    });

    it('prefers the spinner over the error state while both are set', () => {
      createComponent({ error: true, loading: true });

      expect(fixture.nativeElement.querySelector('mat-progress-spinner')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('.error-message')).toBeNull();
    });

    it('shows the error message when the match request failed', () => {
      createComponent({ error: true });

      expect(fixture.nativeElement.querySelector('.error-message')).not.toBeNull();
    });

    it('prompts the user to search before any results exist', () => {
      createComponent({});

      expect(text()).toContain('Click "Find Matches"');
    });

    it('says no matches were found when every tier is empty', () => {
      createComponent({ results: makeResults() });

      expect(text()).toContain('No matches found.');
    });
  });

  // ── totalCount ──────────────────────────────────────────────────────────────

  describe('totalCount', () => {
    it('is zero before any results arrive', () => {
      createComponent({});

      expect(component.totalCount()).toBe(0);
    });

    it('sums all three tiers', () => {
      createComponent({
        results: makeResults({
          fullMatches: [makeMatch({ recipeId: 'a' })],
          nearMatches: [makeMatch({ recipeId: 'b' }), makeMatch({ recipeId: 'c' })],
          partialMatches: [makeMatch({ recipeId: 'd' })],
        }),
      });

      expect(component.totalCount()).toBe(4);
    });
  });

  // ── Score and ingredient arithmetic ─────────────────────────────────────────

  describe('scorePercent', () => {
    it('renders a whole score as 100%', () => {
      createComponent({});

      expect(component.scorePercent(1)).toBe(100);
    });

    it('rounds to the nearest whole percent', () => {
      createComponent({});

      expect(component.scorePercent(0.8765)).toBe(88);
    });

    it('renders a zero score as 0%', () => {
      createComponent({});

      expect(component.scorePercent(0)).toBe(0);
    });
  });

  describe('totalIngredients', () => {
    it('adds the matched and missing counts', () => {
      createComponent({});

      const recipe = makeMatch({
        matchedIngredients: [
          {
            availableQuantity: 1,
            availableUnitOfMeasure: 'oz',
            ingredientName: 'Tomatoes',
            isExpiryImminent: false,
            requiredQuantity: 1,
            requiredUnitOfMeasure: 'oz',
          },
        ],
        missingIngredients: [
          { ingredientName: 'Basil', requiredQuantity: 2, requiredUnitOfMeasure: 'g' },
          { ingredientName: 'Garlic', requiredQuantity: 1, requiredUnitOfMeasure: 'ea' },
        ],
      });

      expect(component.totalIngredients(recipe)).toBe(3);
    });

    it('is zero for a recipe with no ingredients on either side', () => {
      createComponent({});

      expect(component.totalIngredients(makeMatch())).toBe(0);
    });
  });

  // ── Tier sections ───────────────────────────────────────────────────────────

  describe('tier sections', () => {
    it('renders only the tiers that have results', () => {
      createComponent({
        results: makeResults({ fullMatches: [makeMatch()] }),
      });

      expect(fixture.nativeElement.querySelectorAll('.full-match').length).toBe(1);
      expect(fixture.nativeElement.querySelector('.near-match-heading')).toBeNull();
      expect(fixture.nativeElement.querySelector('.partial-match-heading')).toBeNull();
    });

    it('counts the recipes in each section heading', () => {
      createComponent({
        results: makeResults({
          fullMatches: [makeMatch({ recipeId: 'a' }), makeMatch({ recipeId: 'b' })],
        }),
      });

      expect(fixture.nativeElement.querySelector('.full-match-heading').textContent).toContain(
        'Full Matches (2)',
      );
    });

    it('renders all three tiers together', () => {
      createComponent({
        results: makeResults({
          fullMatches: [makeMatch({ recipeId: 'a' })],
          nearMatches: [makeMatch({ recipeId: 'b', matchTier: 'NearMatch' })],
          partialMatches: [makeMatch({ recipeId: 'c', matchTier: 'PartialMatch' })],
        }),
      });

      expect(fixture.nativeElement.querySelectorAll('.match-card').length).toBe(3);
      expect(fixture.nativeElement.querySelector('.near-match-heading')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('.partial-match-heading')).not.toBeNull();
    });

    it('shows the ingredient ratio and rounded score on a card', () => {
      createComponent({
        results: makeResults({
          fullMatches: [
            makeMatch({
              finalScore: 0.923,
              matchedIngredients: [
                {
                  availableQuantity: 1,
                  availableUnitOfMeasure: 'oz',
                  ingredientName: 'Tomatoes',
                  isExpiryImminent: true,
                  requiredQuantity: 1,
                  requiredUnitOfMeasure: 'oz',
                },
              ],
              missingIngredients: [
                { ingredientName: 'Basil', requiredQuantity: 2, requiredUnitOfMeasure: 'g' },
              ],
            }),
          ],
        }),
      });

      const meta = fixture.nativeElement.querySelector('.match-meta').textContent;
      expect(meta).toContain('1 / 2 ingredients');
      expect(meta).toContain('92%');
    });
  });

  // ── Near-match extras ───────────────────────────────────────────────────────

  describe('near-match detail', () => {
    it('lists the missing ingredients', () => {
      createComponent({
        results: makeResults({
          nearMatches: [
            makeMatch({
              matchTier: 'NearMatch',
              missingIngredients: [
                { ingredientName: 'Basil', requiredQuantity: 2, requiredUnitOfMeasure: 'g' },
              ],
            }),
          ],
        }),
      });

      expect(fixture.nativeElement.querySelector('.missing-chip').textContent).toContain('Basil');
    });

    it('shows a substitution with its note', () => {
      createComponent({
        results: makeResults({
          nearMatches: [
            makeMatch({
              matchTier: 'NearMatch',
              substitutionSuggestions: [
                {
                  confidence: 'High',
                  missingIngredientName: 'Basil',
                  notes: 'Use half the quantity',
                  suggestedSubstitute: 'Dried basil',
                },
              ],
            }),
          ],
        }),
      });

      const row = fixture.nativeElement.querySelector('.sub-row').textContent;
      expect(row).toContain('Basil');
      expect(row).toContain('Dried basil');
      expect(fixture.nativeElement.querySelector('.sub-notes').textContent).toContain(
        'Use half the quantity',
      );
    });

    it('omits the note span when a substitution carries none', () => {
      createComponent({
        results: makeResults({
          nearMatches: [
            makeMatch({
              matchTier: 'NearMatch',
              substitutionSuggestions: [
                {
                  confidence: 'Low',
                  missingIngredientName: 'Basil',
                  notes: '',
                  suggestedSubstitute: 'Oregano',
                },
              ],
            }),
          ],
        }),
      });

      expect(fixture.nativeElement.querySelector('.sub-notes')).toBeNull();
    });

    it('omits both lists when a near match has neither gaps nor suggestions', () => {
      createComponent({
        results: makeResults({ nearMatches: [makeMatch({ matchTier: 'NearMatch' })] }),
      });

      expect(fixture.nativeElement.querySelector('.missing-list')).toBeNull();
      expect(fixture.nativeElement.querySelector('.sub-list')).toBeNull();
    });
  });

  // ── AI-unavailable note ─────────────────────────────────────────────────────

  describe('AI availability note', () => {
    it('explains that substitutions are unavailable when Claude did not run', () => {
      createComponent({
        results: makeResults({
          claudeFeasibilityApplied: false,
          fullMatches: [makeMatch()],
        }),
      });

      const note = fixture.nativeElement.querySelector('.ai-disabled-note');
      expect(note).not.toBeNull();
      expect(note.textContent).toContain('Deterministic matching is unchanged.');
    });

    it('stays silent when Claude did run', () => {
      createComponent({
        results: makeResults({ fullMatches: [makeMatch()] }),
      });

      expect(fixture.nativeElement.querySelector('.ai-disabled-note')).toBeNull();
    });

    it('announces the note politely to assistive technology', () => {
      createComponent({
        results: makeResults({
          claudeFeasibilityApplied: false,
          fullMatches: [makeMatch()],
        }),
      });

      const note = fixture.nativeElement.querySelector('.ai-disabled-note');
      expect(note.getAttribute('role')).toBe('status');
      expect(note.getAttribute('aria-live')).toBe('polite');
    });
  });
});
