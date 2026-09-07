import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { RecipeDetailDto, RecipeIngredientDetailDto } from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';
import { RecipeDetailDialogComponent } from './recipe-detail-dialog.component';

describe('RecipeDetailDialogComponent', () => {
  let component: RecipeDetailDialogComponent;
  let fixture: ComponentFixture<RecipeDetailDialogComponent>;
  let recipeServiceMock: {
    addToShoppingList: ReturnType<typeof vi.fn>;
    getRecipeDetail: ReturnType<typeof vi.fn>;
  };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };

  function makeIngredient(
    overrides: Partial<RecipeIngredientDetailDto> = {},
  ): RecipeIngredientDetailDto {
    return {
      canonicalIngredientId: 'ing-1',
      id: 'ri-1',
      ingredientName: 'Diced Tomatoes',
      isContainerResolved: true,
      notes: null,
      quantity: 14.5,
      unitOfMeasureAbbreviation: 'oz',
      unitOfMeasureId: 'uom-oz',
      ...overrides,
    };
  }

  function makeRecipe(overrides: Partial<RecipeDetailDto> = {}): RecipeDetailDto {
    return {
      cuisineType: 'Italian',
      dietaryTags: [],
      id: 'recipe-1',
      ingredients: [makeIngredient()],
      instructions: 'Simmer for 20 minutes.',
      isFullyResolved: true,
      servingCount: 4,
      sourceUrl: null,
      title: 'Marinara',
      ...overrides,
    };
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [RecipeDetailDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: { recipeId: 'recipe-1' } },
        { provide: MatDialogRef, useValue: { close: vi.fn() } },
        { provide: RecipeService, useValue: recipeServiceMock },
      ],
    });

    TestBed.overrideProvider(MatSnackBar, { useValue: snackBarMock });

    fixture = TestBed.createComponent(RecipeDetailDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  function addingToList(): boolean {
    return (component as unknown as { addingToList: () => boolean }).addingToList();
  }

  function addToListButton(): HTMLButtonElement | undefined {
    return (
      Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
    ).find((button) => button.textContent?.includes('Add to Shopping List'));
  }

  beforeEach(() => {
    recipeServiceMock = { addToShoppingList: vi.fn(), getRecipeDetail: vi.fn() };
    snackBarMock = { open: vi.fn() };
    recipeServiceMock.getRecipeDetail.mockReturnValue(of(makeRecipe()));
  });

  // ── Load ────────────────────────────────────────────────────────────────────

  describe('load', () => {
    it('fetches the detail for the recipe named in the dialog data', () => {
      createComponent();

      expect(recipeServiceMock.getRecipeDetail).toHaveBeenCalledWith('recipe-1');
    });

    it('shows the recipe title once loaded', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('h2').textContent).toContain('Marinara');
    });

    it('falls back to a generic title before the recipe arrives', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(throwError(() => new Error('boom')));

      createComponent();

      expect(fixture.nativeElement.querySelector('h2').textContent).toContain('Recipe Detail');
    });

    it('shows an error message when the fetch fails', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(throwError(() => new Error('boom')));

      createComponent();

      expect(fixture.nativeElement.querySelector('.error-message')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('mat-progress-spinner')).toBeNull();
    });
  });

  // ── Rendering ───────────────────────────────────────────────────────────────

  describe('rendering', () => {
    it('renders a row per ingredient', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(
        of(
          makeRecipe({
            ingredients: [makeIngredient(), makeIngredient({ id: 'ri-2' })],
          }),
        ),
      );

      createComponent();

      expect(fixture.nativeElement.querySelectorAll('mat-row').length).toBe(2);
    });

    it('marks a resolved ingredient with the resolved icon', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('.resolved-icon')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('.unresolved-icon')).toBeNull();
    });

    it('flags an ingredient whose container size is still undeclared', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(
        of(
          makeRecipe({
            ingredients: [makeIngredient({ isContainerResolved: false })],
          }),
        ),
      );

      createComponent();

      expect(fixture.nativeElement.querySelector('.unresolved-icon')).not.toBeNull();
    });

    it('shows the notes icon when the original recipe text was preserved', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(
        of(
          makeRecipe({
            ingredients: [makeIngredient({ notes: '1 can chopped tomatoes' })],
          }),
        ),
      );

      createComponent();

      expect(fixture.nativeElement.querySelector('.notes-icon')).not.toBeNull();
    });

    it('omits the notes icon when there are no notes', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('.notes-icon')).toBeNull();
    });

    it('renders a chip per dietary tag alongside the cuisine and serving chips', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(
        of(makeRecipe({ dietaryTags: ['Vegetarian', 'GlutenFree'] })),
      );

      createComponent();

      const chips = Array.from(fixture.nativeElement.querySelectorAll('.meta-chip')).map((chip) =>
        (chip as HTMLElement).textContent?.trim(),
      );
      expect(chips).toEqual(['Italian', 'Vegetarian', 'GlutenFree', '4 servings']);
    });

    it('omits the cuisine chip when the recipe has no cuisine', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(of(makeRecipe({ cuisineType: '' })));

      createComponent();

      expect(fixture.nativeElement.querySelector('.cuisine-chip')).toBeNull();
    });

    it('renders the instructions', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('.instructions-text').textContent).toContain(
        'Simmer for 20 minutes.',
      );
    });

    it('omits the instructions block when there are none', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(of(makeRecipe({ instructions: '' })));

      createComponent();

      expect(fixture.nativeElement.querySelector('.instructions-text')).toBeNull();
    });

    it('links to the original source, opening it safely in a new tab', () => {
      recipeServiceMock.getRecipeDetail.mockReturnValue(
        of(makeRecipe({ sourceUrl: 'https://example.com/marinara' })),
      );

      createComponent();

      const link = fixture.nativeElement.querySelector('.source-link a') as HTMLAnchorElement;
      expect(link.getAttribute('href')).toBe('https://example.com/marinara');
      expect(link.getAttribute('rel')).toBe('noopener noreferrer');
      expect(link.getAttribute('target')).toBe('_blank');
    });

    it('omits the source link when the recipe has no source URL', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('.source-link')).toBeNull();
    });
  });

  // ── Add to shopping list ────────────────────────────────────────────────────

  describe('addToShoppingList', () => {
    it('is offered for a fully-resolved recipe', () => {
      createComponent();

      expect(addToListButton()).toBeDefined();
    });

    it('is withheld for a recipe with unresolved container references', () => {
      // An unresolved recipe cannot produce correct quantities, so it must not
      // reach the shopping list.
      recipeServiceMock.getRecipeDetail.mockReturnValue(of(makeRecipe({ isFullyResolved: false })));

      createComponent();

      expect(addToListButton()).toBeUndefined();
    });

    it('reports how many items were added', () => {
      createComponent();
      recipeServiceMock.addToShoppingList.mockReturnValue(of([{}, {}, {}]));

      component.addToShoppingList();

      expect(recipeServiceMock.addToShoppingList).toHaveBeenCalledWith('recipe-1');
      expect(snackBarMock.open).toHaveBeenCalledWith('Added 3 item(s) to shopping list.', 'OK', {
        duration: 3000,
      });
      expect(addingToList()).toBe(false);
    });

    it('reports zero when the recipe adds nothing new to the list', () => {
      createComponent();
      recipeServiceMock.addToShoppingList.mockReturnValue(of([]));

      component.addToShoppingList();

      expect(snackBarMock.open).toHaveBeenCalledWith('Added 0 item(s) to shopping list.', 'OK', {
        duration: 3000,
      });
    });

    it('reports a failure and clears the in-flight state', () => {
      createComponent();
      recipeServiceMock.addToShoppingList.mockReturnValue(throwError(() => new Error('boom')));

      component.addToShoppingList();

      expect(addingToList()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to add to shopping list.', 'Dismiss', {
        duration: 4000,
      });
    });
  });
});
