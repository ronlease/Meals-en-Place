import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatChipListboxChange } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatPaginator } from '@angular/material/paginator';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  PagedResult,
  RecipeListItemDto,
  RecipeMatchResponse,
} from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';
import { RecipeBrowserComponent } from './recipe-browser.component';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makePagedResult(
  items: RecipeListItemDto[],
  page = 1,
  totalCount = 50,
): PagedResult<RecipeListItemDto> {
  return { items, page, pageSize: 25, totalCount, totalPages: Math.ceil(totalCount / 25) };
}

function makeRecipe(overrides: Partial<RecipeListItemDto> = {}): RecipeListItemDto {
  return {
    cuisineType: 'Italian',
    dietaryTags: [],
    id: 'abc-123',
    isFullyResolved: true,
    title: 'Test Recipe',
    totalIngredients: 5,
    unresolvedCount: 0,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('RecipeBrowserComponent', () => {
  let fixture: ComponentFixture<RecipeBrowserComponent>;
  let component: RecipeBrowserComponent;
  let dialogMock: { open: ReturnType<typeof vi.fn> };
  let recipeServiceSpy: {
    getRecipes: ReturnType<typeof vi.fn>;
    matchRecipes: ReturnType<typeof vi.fn>;
  };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    dialogMock = { open: vi.fn() };
    snackBarMock = { open: vi.fn() };
    recipeServiceSpy = { getRecipes: vi.fn(), matchRecipes: vi.fn() };
    recipeServiceSpy.getRecipes.mockReturnValue(of(makePagedResult([makeRecipe()], 1, 50)));

    await TestBed.configureTestingModule({
      imports: [RecipeBrowserComponent, NoopAnimationsModule],
      providers: [provideRouter([]), { provide: RecipeService, useValue: recipeServiceSpy }],
    }).compileComponents();

    // MatDialogModule is in the component's imports and supplies its own
    // MatDialog, which outranks a plain TestBed provider.
    TestBed.overrideProvider(MatDialog, { useValue: dialogMock });
    TestBed.overrideProvider(MatSnackBar, { useValue: snackBarMock });

    fixture = TestBed.createComponent(RecipeBrowserComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Initial load ────────────────────────────────────────────────────────

  it('calls getRecipes with page=1 and pageSize=25 on init', () => {
    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledWith(1, 25, undefined, undefined);
  });

  it('populates the library signal with items from the paged response', () => {
    expect((component as unknown as { library: () => RecipeListItemDto[] }).library().length).toBe(
      1,
    );
  });

  it('sets totalCount from the response', () => {
    expect((component as unknown as { totalCount: () => number }).totalCount()).toBe(50);
  });

  // ── Page navigation ─────────────────────────────────────────────────────

  it('fetches page 2 when the next-page button is clicked', () => {
    recipeServiceSpy.getRecipes.mockReturnValue(
      of(makePagedResult([makeRecipe({ id: 'page-2-recipe' })], 2, 50)),
    );

    const paginator: MatPaginator = fixture.debugElement.query(
      By.directive(MatPaginator),
    ).componentInstance;

    paginator.nextPage();
    fixture.detectChanges();

    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledWith(2, 25, undefined, undefined);
  });

  it('fetches page 1 again when the previous-page button is clicked from page 2', () => {
    // Advance to page 2 first.
    recipeServiceSpy.getRecipes.mockReturnValue(of(makePagedResult([makeRecipe()], 2, 50)));
    const paginator: MatPaginator = fixture.debugElement.query(
      By.directive(MatPaginator),
    ).componentInstance;
    paginator.nextPage();
    fixture.detectChanges();

    recipeServiceSpy.getRecipes.mockClear();
    recipeServiceSpy.getRecipes.mockReturnValue(of(makePagedResult([makeRecipe()], 1, 50)));
    paginator.previousPage();
    fixture.detectChanges();

    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledWith(1, 25, undefined, undefined);
  });

  // ── Deep-navigation constraints ─────────────────────────────────────────

  it('does not render first-page or last-page buttons', () => {
    // mat-paginator's first/last buttons carry specific aria-labels.
    const firstBtn = fixture.debugElement.query(By.css('button[aria-label="First page"]'));
    const lastBtn = fixture.debugElement.query(By.css('button[aria-label="Last page"]'));
    expect(firstBtn).toBeNull();
    expect(lastBtn).toBeNull();
  });

  it('does not render a page-size selector', () => {
    // The page-size form field is omitted via hidePageSize.
    const pageSizeSelect = fixture.debugElement.query(
      By.css('mat-select[aria-label*="Items per page"]'),
    );
    expect(pageSizeSelect).toBeNull();
  });

  it('does not render a direct page-number input', () => {
    // There should be no text input inside the paginator for jumping to a page.
    const paginator = fixture.debugElement.query(By.directive(MatPaginator));
    const pageInput = paginator?.query(By.css('input[type="number"]'));
    expect(pageInput).toBeNull();
  });

  // ── Error path ──────────────────────────────────────────────────────────

  it('shows the error message when getRecipes fails', async () => {
    recipeServiceSpy.getRecipes.mockReturnValue(throwError(() => new Error('network error')));
    component.loadLibrary();
    fixture.detectChanges();

    const errorEl = fixture.debugElement.query(By.css('.error-message'));
    expect(errorEl).not.toBeNull();
    expect(errorEl.nativeElement.textContent).toContain(
      'Failed to load recipes. Please try again.',
    );
  });

  it('does not leave the spinner visible after a failed request', () => {
    recipeServiceSpy.getRecipes.mockReturnValue(throwError(() => new Error('network error')));
    component.loadLibrary();
    fixture.detectChanges();

    const spinner = fixture.debugElement.query(By.css('mat-progress-spinner'));
    expect(spinner).toBeNull();
  });

  it('re-fetches the current page when the retry button is clicked', () => {
    recipeServiceSpy.getRecipes.mockReturnValue(throwError(() => new Error('network error')));
    component.loadLibrary();
    fixture.detectChanges();

    recipeServiceSpy.getRecipes.mockReturnValue(of(makePagedResult([makeRecipe()], 1, 50)));
    recipeServiceSpy.getRecipes.mockClear();

    const retryBtn: HTMLButtonElement = fixture.debugElement.query(
      By.css('.error-message button'),
    ).nativeElement;
    retryBtn.click();
    fixture.detectChanges();

    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledTimes(1);
    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledWith(1, 25, undefined, undefined);
  });

  it('clears the error state and shows data after a successful retry', () => {
    recipeServiceSpy.getRecipes.mockReturnValue(throwError(() => new Error('network error')));
    component.loadLibrary();
    fixture.detectChanges();

    recipeServiceSpy.getRecipes.mockReturnValue(of(makePagedResult([makeRecipe()], 1, 50)));
    const retryBtn: HTMLButtonElement = fixture.debugElement.query(
      By.css('.error-message button'),
    ).nativeElement;
    retryBtn.click();
    fixture.detectChanges();

    const errorEl = fixture.debugElement.query(By.css('.error-message'));
    expect(errorEl).toBeNull();
  });
  // ── Empty library ───────────────────────────────────────────────────────────

  it('shows an empty-state message when the library has no recipes', () => {
    recipeServiceSpy.getRecipes.mockReturnValue(of(makePagedResult([], 1, 0)));
    component.loadLibrary();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No recipes yet.');
  });

  // ── Resolution status badges ────────────────────────────────────────────────

  describe('resolution status', () => {
    it('badges a fully-resolved recipe as Resolved', () => {
      expect(fixture.debugElement.query(By.css('.resolved-badge'))).not.toBeNull();
      expect(fixture.debugElement.query(By.css('.awaiting-badge'))).toBeNull();
    });

    it('badges an unresolved recipe with its outstanding count', () => {
      // Unresolved recipes are excluded from matching, so the count is the call
      // to action, not decoration.
      recipeServiceSpy.getRecipes.mockReturnValue(
        of(makePagedResult([makeRecipe({ isFullyResolved: false, unresolvedCount: 3 })], 1, 1)),
      );
      component.loadLibrary();
      fixture.detectChanges();

      const badge = fixture.debugElement.query(By.css('.awaiting-badge'));
      expect(badge.nativeElement.textContent).toContain('Awaiting Resolution (3)');
    });

    it('marks the unresolved row so it reads differently from the rest', () => {
      recipeServiceSpy.getRecipes.mockReturnValue(
        of(makePagedResult([makeRecipe({ isFullyResolved: false })], 1, 1)),
      );
      component.loadLibrary();
      fixture.detectChanges();

      expect(fixture.debugElement.query(By.css('mat-row.awaiting-row'))).not.toBeNull();
    });

    it('renders a chip per dietary tag', () => {
      recipeServiceSpy.getRecipes.mockReturnValue(
        of(makePagedResult([makeRecipe({ dietaryTags: ['Vegan', 'GlutenFree'] })], 1, 1)),
      );
      component.loadLibrary();
      fixture.detectChanges();

      const chips = fixture.debugElement
        .queryAll(By.css('mat-cell .dietary-chip'))
        .map((chip) => (chip.nativeElement as HTMLElement).textContent?.trim());
      expect(chips).toEqual(['Vegan', 'GlutenFree']);
    });
  });

  // ── Recipe detail ───────────────────────────────────────────────────────────

  describe('openRecipeDetail', () => {
    it('opens the detail dialog for the clicked recipe', () => {
      component.openRecipeDetail(makeRecipe({ id: 'recipe-42' }));

      expect(dialogMock.open).toHaveBeenCalledWith(expect.anything(), {
        data: { recipeId: 'recipe-42' },
        maxWidth: '700px',
        width: '90vw',
      });
    });

    it('opens on a row click', () => {
      const openRecipeDetail = vi.spyOn(component, 'openRecipeDetail');

      (fixture.debugElement.query(By.css('mat-row')).nativeElement as HTMLElement).click();

      expect(openRecipeDetail).toHaveBeenCalled();
    });
  });

  // ── Matching ────────────────────────────────────────────────────────────────

  describe('findMatches', () => {
    const EMPTY_MATCH: RecipeMatchResponse = {
      claudeFeasibilityApplied: true,
      fullMatches: [],
      nearMatches: [],
      partialMatches: [],
    };

    function matchResults(): RecipeMatchResponse | null {
      return (
        component as unknown as { matchResults: () => RecipeMatchResponse | null }
      ).matchResults();
    }

    it('requests matches with no dietary filter by default', () => {
      recipeServiceSpy.matchRecipes.mockReturnValue(of(EMPTY_MATCH));

      component.findMatches();

      expect(recipeServiceSpy.matchRecipes).toHaveBeenCalledWith(undefined, undefined);
      expect(matchResults()).toEqual(EMPTY_MATCH);
    });

    it('applies the selected dietary tags to the request', () => {
      component.onDietaryFilterChange({ value: ['Vegan', 'LowCarb'] } as MatChipListboxChange);
      recipeServiceSpy.matchRecipes.mockReturnValue(of(EMPTY_MATCH));

      component.findMatches();

      expect(recipeServiceSpy.matchRecipes).toHaveBeenCalledWith(undefined, ['Vegan', 'LowCarb']);
    });

    it('sends undefined rather than an empty array when the filter is cleared', () => {
      component.onDietaryFilterChange({ value: ['Vegan'] } as MatChipListboxChange);
      component.onDietaryFilterChange({ value: [] } as MatChipListboxChange);
      recipeServiceSpy.matchRecipes.mockReturnValue(of(EMPTY_MATCH));

      component.findMatches();

      expect(recipeServiceSpy.matchRecipes).toHaveBeenCalledWith(undefined, undefined);
    });

    it('treats a null selection as no tags', () => {
      component.onDietaryFilterChange({ value: null } as unknown as MatChipListboxChange);
      recipeServiceSpy.matchRecipes.mockReturnValue(of(EMPTY_MATCH));

      component.findMatches();

      expect(recipeServiceSpy.matchRecipes).toHaveBeenCalledWith(undefined, undefined);
    });

    it('reports a failed match request without discarding the library', () => {
      recipeServiceSpy.matchRecipes.mockReturnValue(throwError(() => new Error('boom')));

      component.findMatches();

      expect((component as unknown as { matchError: () => boolean }).matchError()).toBe(true);
      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to load matches.', 'Dismiss', {
        duration: 4000,
      });
      expect(
        (component as unknown as { library: () => RecipeListItemDto[] }).library().length,
      ).toBe(1);
    });

    it('clears a previous error when a retry succeeds', () => {
      recipeServiceSpy.matchRecipes.mockReturnValue(throwError(() => new Error('boom')));
      component.findMatches();

      recipeServiceSpy.matchRecipes.mockReturnValue(of(EMPTY_MATCH));
      component.findMatches();

      expect((component as unknown as { matchError: () => boolean }).matchError()).toBe(false);
    });
  });

  // ── Page-number translation ─────────────────────────────────────────────────

  it('translates the paginator 0-based index to the API 1-based page number', () => {
    // A silent off-by-one here would page the whole library incorrectly.
    component.onPageChange({ length: 50, pageIndex: 3, pageSize: 25, previousPageIndex: 2 });

    expect(recipeServiceSpy.getRecipes).toHaveBeenLastCalledWith(4, 25, undefined, undefined);
  });
});
