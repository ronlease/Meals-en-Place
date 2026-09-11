import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { By } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CanonicalIngredientDto, UnitOfMeasureDto } from '../../core/models/inventory.models';
import { RecipeDetailDto } from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { IngredientAutocompleteComponent } from '../../shared/ingredient-autocomplete/ingredient-autocomplete.component';
import { RecipeCreateComponent } from './recipe-create.component';

describe('RecipeCreateComponent', () => {
  const INGREDIENT: CanonicalIngredientDto = {
    category: 'Canned',
    defaultUnitOfMeasureId: 'uom-oz',
    id: 'ing-1',
    name: 'Diced Tomatoes',
  };

  const UNITS: UnitOfMeasureDto[] = [
    { abbreviation: 'oz', id: 'uom-oz', name: 'Ounce', unitOfMeasureType: 'Weight' },
  ];

  let component: RecipeCreateComponent;
  let fixture: ComponentFixture<RecipeCreateComponent>;
  let navigate: ReturnType<typeof vi.fn>;
  let recipeServiceMock: { createRecipe: ReturnType<typeof vi.fn> };
  let referenceDataServiceMock: {
    createIngredient: ReturnType<typeof vi.fn>;
    getUnits: ReturnType<typeof vi.fn>;
    searchIngredients: ReturnType<typeof vi.fn>;
  };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };

  function makeRecipe(title = 'Marinara'): RecipeDetailDto {
    return {
      cuisineType: 'Italian',
      dietaryTags: [],
      id: 'recipe-1',
      ingredients: [],
      instructions: 'Simmer.',
      isFullyResolved: true,
      servingCount: 4,
      sourceUrl: null,
      title,
    };
  }

  /** Fills the top-level fields and the first ingredient row with valid values. */
  function fillValidForm(): void {
    component.form.patchValue({
      cuisineType: 'Italian',
      instructions: 'Simmer for 20 minutes.',
      servingCount: 4,
      title: 'Marinara',
    });
    component.ingredientControls.at(0).patchValue({
      canonicalIngredient: INGREDIENT,
      notes: '',
      quantity: 14.5,
      unitOfMeasureId: 'uom-oz',
    });
  }

  function saving(): boolean {
    return (component as unknown as { saving: () => boolean }).saving();
  }

  beforeEach(() => {
    navigate = vi.fn();
    recipeServiceMock = { createRecipe: vi.fn() };
    referenceDataServiceMock = {
      createIngredient: vi.fn(),
      getUnits: vi.fn().mockReturnValue(of(UNITS)),
      searchIngredients: vi.fn().mockReturnValue(of([])),
    };
    snackBarMock = { open: vi.fn() };

    TestBed.configureTestingModule({
      imports: [RecipeCreateComponent, NoopAnimationsModule],
      providers: [
        provideNativeDateAdapter(),
        provideRouter([]),
        { provide: RecipeService, useValue: recipeServiceMock },
        { provide: ReferenceDataService, useValue: referenceDataServiceMock },
      ],
    });

    TestBed.overrideProvider(MatSnackBar, { useValue: snackBarMock });
    TestBed.overrideProvider(Router, { useValue: { navigate } });

    fixture = TestBed.createComponent(RecipeCreateComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Initial state ───────────────────────────────────────────────────────────

  describe('initial state', () => {
    it('loads the units of measure reference data on init', () => {
      expect((component as unknown as { units: () => UnitOfMeasureDto[] }).units()).toEqual(UNITS);
    });

    it('starts with one blank ingredient row so the form is usable immediately', () => {
      expect(component.ingredientControls.length).toBe(1);
    });

    it('defaults the serving count to four', () => {
      expect(component.form.controls.servingCount.value).toBe(4);
    });

    it('is invalid until the required fields are filled', () => {
      expect(component.form.invalid).toBe(true);
    });
  });

  // ── Ingredient rows ─────────────────────────────────────────────────────────

  describe('ingredient rows', () => {
    it('adds a row', () => {
      component.addIngredient();

      expect(component.ingredientControls.length).toBe(2);
    });

    it('gives a new row a quantity of one and no unit', () => {
      component.addIngredient();

      const row = component.ingredientControls.at(1).value;
      expect(row.quantity).toBe(1);
      expect(row.unitOfMeasureId).toBeNull();
    });

    it('removes the row at the given index, keeping the others', () => {
      component.addIngredient();
      component.ingredientControls.at(0).patchValue({ canonicalIngredient: INGREDIENT });
      component.addIngredient();

      component.removeIngredient(1);

      expect(component.ingredientControls.length).toBe(2);
      expect(component.ingredientControls.at(0).value.canonicalIngredient).toEqual(INGREDIENT);
    });

    it('exposes a row as a FormGroup for the template', () => {
      const group = component.asFormGroup(component.ingredientControls.at(0));

      expect(group.controls['quantity']).toBeDefined();
    });
  });

  // ── Shared autocomplete component ──────────────────────────────────────────

  describe('shared autocomplete component', () => {
    it('renders the shared IngredientAutocompleteComponent for each ingredient row', () => {
      // Scenario: Recipe create rows use the shared autocomplete instead of a full-list select.
      fixture.detectChanges();

      const autocompletes = fixture.debugElement.queryAll(
        By.directive(IngredientAutocompleteComponent),
      );
      expect(autocompletes.length).toBe(1);
    });

    it('adds a second IngredientAutocompleteComponent when a new row is added', () => {
      component.addIngredient();
      fixture.detectChanges();

      const autocompletes = fixture.debugElement.queryAll(
        By.directive(IngredientAutocompleteComponent),
      );
      expect(autocompletes.length).toBe(2);
    });

    it('allowCreate defaults to false in each ingredient row autocomplete', () => {
      // Scenario: And the allowCreate input is disabled on the recipe create page.
      fixture.detectChanges();

      const autocomplete = fixture.debugElement.query(
        By.directive(IngredientAutocompleteComponent),
      );
      const instance = autocomplete.componentInstance as IngredientAutocompleteComponent;

      expect(instance.allowCreate()).toBe(false);
    });

    it('ingredient rows manage their own selection state independently (no cross-talk)', () => {
      // Scenario: Shared component is the single owner of search behaviour — each
      // IngredientAutocompleteComponent instance has its own signal state; setting a
      // value on row 0 must not affect row 1's autocomplete display text or form value.
      component.addIngredient();
      component.ingredientControls.at(0).patchValue({ canonicalIngredient: INGREDIENT });
      fixture.detectChanges();

      // Row 1's form value must remain null.
      expect(component.ingredientControls.at(1).value.canonicalIngredient).toBeNull();

      // Row 1's autocomplete display text must remain empty.
      const autocompletes = fixture.debugElement.queryAll(
        By.directive(IngredientAutocompleteComponent),
      );
      const ac1 = autocompletes[1].componentInstance as IngredientAutocompleteComponent;
      const displayText1 = (ac1 as unknown as { displayText: () => string }).displayText();
      expect(displayText1).toBe('');
    });
  });

  // ── Submission guards ───────────────────────────────────────────────────────

  describe('submission guards', () => {
    it('refuses to submit an invalid form', () => {
      component.onSubmit();

      expect(recipeServiceMock.createRecipe).not.toHaveBeenCalled();
    });

    it('refuses to submit a recipe with no ingredient rows', () => {
      fillValidForm();
      component.removeIngredient(0);

      component.onSubmit();

      expect(recipeServiceMock.createRecipe).not.toHaveBeenCalled();
    });
  });

  // ── Request construction ────────────────────────────────────────────────────

  describe('onSubmit', () => {
    it('posts the recipe with its ingredient rows, extracting the canonical ID from the DTO', () => {
      fillValidForm();
      recipeServiceMock.createRecipe.mockReturnValue(of(makeRecipe()));

      component.onSubmit();

      expect(recipeServiceMock.createRecipe).toHaveBeenCalledWith({
        cuisineType: 'Italian',
        ingredients: [
          {
            canonicalIngredientId: 'ing-1',
            notes: null,
            quantity: 14.5,
            unitOfMeasureId: 'uom-oz',
          },
        ],
        instructions: 'Simmer for 20 minutes.',
        servingCount: 4,
        title: 'Marinara',
      });
    });

    it('preserves a note when one is typed', () => {
      fillValidForm();
      component.ingredientControls.at(0).patchValue({ notes: '1 can chopped tomatoes' });
      recipeServiceMock.createRecipe.mockReturnValue(of(makeRecipe()));

      component.onSubmit();

      expect(recipeServiceMock.createRecipe.mock.calls[0][0].ingredients[0].notes).toBe(
        '1 can chopped tomatoes',
      );
    });

    it('sends a null unit when none was chosen, rather than an empty string', () => {
      fillValidForm();
      component.ingredientControls.at(0).patchValue({ unitOfMeasureId: null });
      recipeServiceMock.createRecipe.mockReturnValue(of(makeRecipe()));

      component.onSubmit();

      expect(
        recipeServiceMock.createRecipe.mock.calls[0][0].ingredients[0].unitOfMeasureId,
      ).toBeNull();
    });

    it('sends an empty string for an omitted cuisine', () => {
      fillValidForm();
      component.form.patchValue({ cuisineType: '' });
      recipeServiceMock.createRecipe.mockReturnValue(of(makeRecipe()));

      component.onSubmit();

      expect(recipeServiceMock.createRecipe.mock.calls[0][0].cuisineType).toBe('');
    });

    it('confirms and returns to the library on success', () => {
      fillValidForm();
      recipeServiceMock.createRecipe.mockReturnValue(of(makeRecipe('Marinara')));

      component.onSubmit();

      expect(snackBarMock.open).toHaveBeenCalledWith('Recipe "Marinara" created.', 'OK', {
        duration: 3000,
      });
      expect(navigate).toHaveBeenCalledWith(['/recipes']);
      expect(saving()).toBe(false);
    });

    it('stays on the form and reports the failure', () => {
      fillValidForm();
      recipeServiceMock.createRecipe.mockReturnValue(throwError(() => new Error('boom')));

      component.onSubmit();

      expect(navigate).not.toHaveBeenCalled();
      expect(saving()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to create recipe.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('posts canonical ingredient IDs for both rows when the form has two ingredient rows', () => {
      // Scenario: Selecting an ingredient in a recipe row sets that row's canonical ingredient ID.
      // Two rows; each must contribute its own canonicalIngredientId to the request.
      const secondIngredient: CanonicalIngredientDto = {
        category: 'Herbs',
        defaultUnitOfMeasureId: 'uom-oz',
        id: 'ing-2',
        name: 'Fresh Basil',
      };

      fillValidForm(); // seeds row 0 with INGREDIENT (id: 'ing-1')
      component.addIngredient();
      component.ingredientControls.at(1).patchValue({
        canonicalIngredient: secondIngredient,
        notes: '',
        quantity: 2,
        unitOfMeasureId: 'uom-oz',
      });
      recipeServiceMock.createRecipe.mockReturnValue(of(makeRecipe()));

      component.onSubmit();

      const payload = recipeServiceMock.createRecipe.mock.calls[0][0];
      expect(payload.ingredients).toHaveLength(2);
      expect(payload.ingredients[0].canonicalIngredientId).toBe('ing-1');
      expect(payload.ingredients[1].canonicalIngredientId).toBe('ing-2');
    });
  });

  // ── Cancel ──────────────────────────────────────────────────────────────────

  it('cancel returns to the library without saving', () => {
    component.cancel();

    expect(navigate).toHaveBeenCalledWith(['/recipes']);
    expect(recipeServiceMock.createRecipe).not.toHaveBeenCalled();
  });
});
