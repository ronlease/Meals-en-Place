import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatPaginator } from '@angular/material/paginator';
import { By } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';
import { PagedResult, RecipeListItemDto } from '../../core/models/recipe.models';
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
  let recipeServiceSpy: jasmine.SpyObj<RecipeService>;

  beforeEach(async () => {
    recipeServiceSpy = jasmine.createSpyObj('RecipeService', [
      'getRecipes',
      'matchRecipes',
    ]);
    recipeServiceSpy.getRecipes.and.returnValue(
      of(makePagedResult([makeRecipe()], 1, 50)),
    );

    await TestBed.configureTestingModule({
      imports: [RecipeBrowserComponent, NoopAnimationsModule],
      providers: [{ provide: RecipeService, useValue: recipeServiceSpy }],
    }).compileComponents();

    fixture = TestBed.createComponent(RecipeBrowserComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Initial load ────────────────────────────────────────────────────────

  it('calls getRecipes with page=1 and pageSize=25 on init', () => {
    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledWith(1, 25);
  });

  it('populates the library signal with items from the paged response', () => {
    expect((component as unknown as { library: () => RecipeListItemDto[] }).library().length).toBe(1);
  });

  it('sets totalCount from the response', () => {
    expect(
      (component as unknown as { totalCount: () => number }).totalCount(),
    ).toBe(50);
  });

  // ── Page navigation ─────────────────────────────────────────────────────

  it('fetches page 2 when the next-page button is clicked', () => {
    recipeServiceSpy.getRecipes.and.returnValue(
      of(makePagedResult([makeRecipe({ id: 'page-2-recipe' })], 2, 50)),
    );

    const paginator: MatPaginator = fixture.debugElement
      .query(By.directive(MatPaginator))
      .componentInstance;

    paginator.nextPage();
    fixture.detectChanges();

    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledWith(2, 25);
  });

  it('fetches page 1 again when the previous-page button is clicked from page 2', () => {
    // Advance to page 2 first.
    recipeServiceSpy.getRecipes.and.returnValue(
      of(makePagedResult([makeRecipe()], 2, 50)),
    );
    const paginator: MatPaginator = fixture.debugElement
      .query(By.directive(MatPaginator))
      .componentInstance;
    paginator.nextPage();
    fixture.detectChanges();

    recipeServiceSpy.getRecipes.calls.reset();
    recipeServiceSpy.getRecipes.and.returnValue(
      of(makePagedResult([makeRecipe()], 1, 50)),
    );
    paginator.previousPage();
    fixture.detectChanges();

    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledWith(1, 25);
  });

  // ── Deep-navigation constraints ─────────────────────────────────────────

  it('does not render first-page or last-page buttons', () => {
    // mat-paginator's first/last buttons carry specific aria-labels.
    const firstBtn = fixture.debugElement.query(
      By.css('button[aria-label="First page"]'),
    );
    const lastBtn = fixture.debugElement.query(
      By.css('button[aria-label="Last page"]'),
    );
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
    recipeServiceSpy.getRecipes.and.returnValue(
      throwError(() => new Error('network error')),
    );
    component.loadLibrary();
    fixture.detectChanges();

    const errorEl = fixture.debugElement.query(By.css('.error-message'));
    expect(errorEl).not.toBeNull();
    expect(errorEl.nativeElement.textContent).toContain(
      'Failed to load recipes. Please try again.',
    );
  });

  it('does not leave the spinner visible after a failed request', () => {
    recipeServiceSpy.getRecipes.and.returnValue(
      throwError(() => new Error('network error')),
    );
    component.loadLibrary();
    fixture.detectChanges();

    const spinner = fixture.debugElement.query(
      By.css('mat-progress-spinner'),
    );
    expect(spinner).toBeNull();
  });

  it('re-fetches the current page when the retry button is clicked', () => {
    recipeServiceSpy.getRecipes.and.returnValue(
      throwError(() => new Error('network error')),
    );
    component.loadLibrary();
    fixture.detectChanges();

    recipeServiceSpy.getRecipes.and.returnValue(
      of(makePagedResult([makeRecipe()], 1, 50)),
    );
    recipeServiceSpy.getRecipes.calls.reset();

    const retryBtn: HTMLButtonElement = fixture.debugElement.query(
      By.css('.error-message button'),
    ).nativeElement;
    retryBtn.click();
    fixture.detectChanges();

    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledTimes(1);
    expect(recipeServiceSpy.getRecipes).toHaveBeenCalledWith(1, 25);
  });

  it('clears the error state and shows data after a successful retry', () => {
    recipeServiceSpy.getRecipes.and.returnValue(
      throwError(() => new Error('network error')),
    );
    component.loadLibrary();
    fixture.detectChanges();

    recipeServiceSpy.getRecipes.and.returnValue(
      of(makePagedResult([makeRecipe()], 1, 50)),
    );
    const retryBtn: HTMLButtonElement = fixture.debugElement.query(
      By.css('.error-message button'),
    ).nativeElement;
    retryBtn.click();
    fixture.detectChanges();

    const errorEl = fixture.debugElement.query(By.css('.error-message'));
    expect(errorEl).toBeNull();
  });
});
