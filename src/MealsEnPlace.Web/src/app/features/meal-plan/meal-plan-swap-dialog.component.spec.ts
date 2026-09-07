import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatListOption, MatSelectionListChange } from '@angular/material/list';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RecipeListItemDto } from '../../core/models/recipe.models';
import { MealPlanSwapDialogComponent, SwapDialogData } from './meal-plan-swap-dialog.component';

describe('MealPlanSwapDialogComponent', () => {
  let component: MealPlanSwapDialogComponent;
  let dialogRefMock: { close: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<MealPlanSwapDialogComponent>;

  function makeRecipe(overrides: Partial<RecipeListItemDto> = {}): RecipeListItemDto {
    return {
      cuisineType: 'Italian',
      dietaryTags: [],
      id: 'recipe-1',
      isFullyResolved: true,
      title: 'Pasta',
      totalIngredients: 5,
      unresolvedCount: 0,
      ...overrides,
    };
  }

  function makeData(overrides: Partial<SwapDialogData> = {}): SwapDialogData {
    return {
      currentRecipeId: 'recipe-current',
      currentRecipeTitle: 'Current Dish',
      loadFailed: false,
      recipes: [],
      totalCount: 0,
      ...overrides,
    };
  }

  function createComponent(data: SwapDialogData): void {
    dialogRefMock = { close: vi.fn() };

    TestBed.configureTestingModule({
      imports: [MealPlanSwapDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefMock },
      ],
    });

    fixture = TestBed.createComponent(MealPlanSwapDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  /** Builds a selection-change event carrying the given option value. */
  function selectionChange(value: string | undefined): MatSelectionListChange {
    return {
      options: value === undefined ? [] : [{ value } as MatListOption],
    } as MatSelectionListChange;
  }

  // ── Candidate filtering ─────────────────────────────────────────────────────

  describe('available recipes', () => {
    it('excludes the recipe currently in the slot', () => {
      createComponent(
        makeData({
          recipes: [
            makeRecipe({ id: 'recipe-current', title: 'Current Dish' }),
            makeRecipe({ id: 'recipe-1', title: 'Pasta' }),
          ],
          totalCount: 2,
        }),
      );

      expect(component.availableRecipes.map((r) => r.id)).toEqual(['recipe-1']);
    });

    it('excludes recipes with unresolved container references', () => {
      // Unresolved recipes are outside the matching pool, so they must not be
      // offered as a swap target either.
      createComponent(
        makeData({
          recipes: [
            makeRecipe({ id: 'recipe-1', isFullyResolved: false, title: 'Awaiting' }),
            makeRecipe({ id: 'recipe-2', title: 'Ready' }),
          ],
          totalCount: 2,
        }),
      );

      expect(component.availableRecipes.map((r) => r.title)).toEqual(['Ready']);
    });

    it('sorts the candidates alphabetically by title', () => {
      createComponent(
        makeData({
          recipes: [
            makeRecipe({ id: 'r1', title: 'Zucchini Bake' }),
            makeRecipe({ id: 'r2', title: 'Apple Crumble' }),
            makeRecipe({ id: 'r3', title: 'Minestrone' }),
          ],
          totalCount: 3,
        }),
      );

      expect(component.availableRecipes.map((r) => r.title)).toEqual([
        'Apple Crumble',
        'Minestrone',
        'Zucchini Bake',
      ]);
    });

    it('renders one option per candidate', () => {
      createComponent(
        makeData({
          recipes: [makeRecipe({ id: 'r1' }), makeRecipe({ id: 'r2', title: 'Risotto' })],
          totalCount: 2,
        }),
      );

      expect(fixture.nativeElement.querySelectorAll('mat-list-option').length).toBe(2);
    });
  });

  // ── Empty states distinguish "none" from "could not load" ───────────────────

  describe('empty states', () => {
    it('says no other recipes are available when the library genuinely has none', () => {
      createComponent(makeData({ recipes: [], totalCount: 0 }));

      expect(fixture.nativeElement.querySelector('.no-recipes').textContent).toContain(
        'No other recipes available.',
      );
    });

    it('says the fetch failed when the list is empty for that reason', () => {
      // The two empty states are not interchangeable: one is recoverable.
      createComponent(makeData({ loadFailed: true, recipes: [], totalCount: 0 }));

      expect(fixture.nativeElement.querySelector('.no-recipes').textContent).toContain(
        'Could not load recipes.',
      );
    });

    it('shows no empty-state message when candidates exist', () => {
      createComponent(makeData({ recipes: [makeRecipe()], totalCount: 1 }));

      expect(fixture.nativeElement.querySelector('.no-recipes')).toBeNull();
    });
  });

  // ── Partial-list note ───────────────────────────────────────────────────────

  describe('partial list note', () => {
    it('warns when the fetched page is smaller than the library', () => {
      createComponent(makeData({ recipes: [makeRecipe()], totalCount: 500 }));

      expect(component.isPartialList).toBe(true);
      expect(fixture.nativeElement.querySelector('.partial-note')).not.toBeNull();
    });

    it('stays silent when the whole library was fetched', () => {
      createComponent(makeData({ recipes: [makeRecipe()], totalCount: 1 }));

      expect(component.isPartialList).toBe(false);
      expect(fixture.nativeElement.querySelector('.partial-note')).toBeNull();
    });

    it('compares against the fetched count, not the filtered candidate count', () => {
      // Two recipes were fetched out of two in the library, but one is filtered
      // out as unresolved. That is not a partial page.
      createComponent(
        makeData({
          recipes: [makeRecipe({ id: 'r1' }), makeRecipe({ id: 'r2', isFullyResolved: false })],
          totalCount: 2,
        }),
      );

      expect(component.availableRecipes.length).toBe(1);
      expect(component.isPartialList).toBe(false);
    });
  });

  // ── Selection ───────────────────────────────────────────────────────────────

  describe('onSelect', () => {
    it('closes with the chosen recipe ID', () => {
      createComponent(makeData({ recipes: [makeRecipe()], totalCount: 1 }));

      component.onSelect(selectionChange('recipe-1'));

      expect(dialogRefMock.close).toHaveBeenCalledWith('recipe-1');
    });

    it('stays open when the change carries no option', () => {
      createComponent(makeData({ recipes: [makeRecipe()], totalCount: 1 }));

      component.onSelect(selectionChange(undefined));

      expect(dialogRefMock.close).not.toHaveBeenCalled();
    });
  });

  // ── Header ──────────────────────────────────────────────────────────────────

  it('names the recipe currently occupying the slot', () => {
    createComponent(makeData({ currentRecipeTitle: 'Chicken Piccata' }));

    expect(fixture.nativeElement.querySelector('.current-label').textContent).toContain(
      'Chicken Piccata',
    );
  });
});
