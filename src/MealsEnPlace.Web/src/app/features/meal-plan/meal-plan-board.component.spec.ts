import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { Observable, of, throwError } from 'rxjs';
import {
  ConsumeMealResponse,
  MealPlanResponse,
  MealPlanSlotResponse,
  ReorderPreviewResponse,
} from '../../core/models/meal-plan.models';
import { PagedResult, RecipeListItemDto } from '../../core/models/recipe.models';
import { MealPlanService } from '../../core/services/meal-plan.service';
import { RecipeService } from '../../core/services/recipe.service';
import { TodoistAvailabilityService } from '../../core/services/todoist-availability.service';
import { MealPlanBoardComponent } from './meal-plan-board.component';

describe('MealPlanBoardComponent', () => {
  let component: MealPlanBoardComponent;
  let configured: ReturnType<typeof signal<boolean>>;
  let dialogMock: { open: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<MealPlanBoardComponent>;
  let mealPlanServiceMock: {
    applyReorderByExpiry: ReturnType<typeof vi.fn>;
    consumeSlot: ReturnType<typeof vi.fn>;
    generatePlan: ReturnType<typeof vi.fn>;
    getActivePlan: ReturnType<typeof vi.fn>;
    previewReorderByExpiry: ReturnType<typeof vi.fn>;
    pushToTodoist: ReturnType<typeof vi.fn>;
    swapSlot: ReturnType<typeof vi.fn>;
    unconsumeSlot: ReturnType<typeof vi.fn>;
  };
  let recipeServiceMock: { getRecipes: ReturnType<typeof vi.fn> };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };

  function makeSlot(overrides: Partial<MealPlanSlotResponse> = {}): MealPlanSlotResponse {
    return {
      consumedAt: null,
      cuisineType: 'Italian',
      dayOfWeek: 'Monday',
      id: 'slot-1',
      mealSlot: 'Dinner',
      recipeId: 'recipe-1',
      recipeTitle: 'Spinach Lasagna',
      ...overrides,
    };
  }

  function makePlan(slots: MealPlanSlotResponse[] = [makeSlot()]): MealPlanResponse {
    return {
      createdAt: '2026-09-01T00:00:00Z',
      id: 'plan-1',
      name: 'Week of Sept 1',
      slots,
      weekStartDate: '2026-09-01',
    };
  }

  function makeRecipePage(
    items: RecipeListItemDto[] = [],
    totalCount = items.length,
  ): PagedResult<RecipeListItemDto> {
    return { items, page: 1, pageSize: 100, totalCount, totalPages: 1 };
  }

  /** A dialog handle whose afterClosed emits the given result. */
  function dialogReturning(result: unknown): { afterClosed: () => Observable<unknown> } {
    return { afterClosed: () => of(result) };
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [MealPlanBoardComponent, NoopAnimationsModule],
      providers: [
        { provide: MealPlanService, useValue: mealPlanServiceMock },
        { provide: RecipeService, useValue: recipeServiceMock },
        { provide: TodoistAvailabilityService, useValue: { configured, refresh: vi.fn() } },
      ],
    });

    // MatDialogModule is in the component's imports and supplies its own
    // MatDialog, which outranks a plain TestBed provider. Override it.
    TestBed.overrideProvider(MatDialog, { useValue: dialogMock });
    TestBed.overrideProvider(MatSnackBar, { useValue: snackBarMock });

    fixture = TestBed.createComponent(MealPlanBoardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  interface Internals {
    consumingSlotId: () => string | null;
    error: () => boolean;
    loading: () => boolean;
    plan: () => MealPlanResponse | null;
    pushing: () => boolean;
    reordering: () => boolean;
  }

  function internals(): Internals {
    return component as unknown as Internals;
  }

  function buttonWithText(text: string): HTMLButtonElement | undefined {
    return (
      Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
    ).find((button) => button.textContent?.includes(text));
  }

  beforeEach(() => {
    configured = signal(true);
    dialogMock = { open: vi.fn().mockReturnValue(dialogReturning(undefined)) };
    snackBarMock = { open: vi.fn() };
    mealPlanServiceMock = {
      applyReorderByExpiry: vi.fn(),
      consumeSlot: vi.fn(),
      generatePlan: vi.fn(),
      getActivePlan: vi.fn().mockReturnValue(of(makePlan())),
      previewReorderByExpiry: vi.fn(),
      pushToTodoist: vi.fn(),
      swapSlot: vi.fn(),
      unconsumeSlot: vi.fn(),
    };
    recipeServiceMock = { getRecipes: vi.fn().mockReturnValue(of(makeRecipePage())) };
  });

  // ── Initial load ────────────────────────────────────────────────────────────

  describe('initial load', () => {
    it('fetches the active plan on init', () => {
      createComponent();

      expect(mealPlanServiceMock.getActivePlan).toHaveBeenCalled();
      expect(internals().plan()?.id).toBe('plan-1');
    });

    it('prefetches a page of recipes at the API maximum for the swap picker', () => {
      // MEP-043 caps the page at 100; the picker has no search until MEP-046.
      createComponent();

      expect(recipeServiceMock.getRecipes).toHaveBeenCalledWith(1, 100);
    });

    it('treats a 404 as "no plan yet" rather than an error', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));

      createComponent();

      expect(internals().plan()).toBeNull();
      expect(internals().error()).toBe(false);
      expect(fixture.nativeElement.textContent).toContain('No meal plan yet');
    });

    it('treats any other failure as an error with a retry affordance', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 500 })));

      createComponent();

      expect(internals().error()).toBe(true);
      expect(fixture.nativeElement.querySelector('.error-message')).not.toBeNull();
    });

    it('recovers when Retry is clicked after a failure', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 500 })));
      createComponent();

      mealPlanServiceMock.getActivePlan.mockReturnValue(of(makePlan()));
      (
        fixture.nativeElement.querySelector('.error-message button') as HTMLButtonElement
      ).click();
      fixture.detectChanges();

      expect(internals().error()).toBe(false);
      expect(internals().plan()?.id).toBe('plan-1');
    });
  });

  // ── Board layout ────────────────────────────────────────────────────────────

  describe('getSlotsForDay', () => {
    it('returns an empty list when no plan is loaded', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));
      createComponent();

      expect(component.getSlotsForDay('Monday')).toEqual([]);
    });

    it('returns only the slots on that day', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(
        of(
          makePlan([
            makeSlot({ dayOfWeek: 'Monday', id: 'a' }),
            makeSlot({ dayOfWeek: 'Tuesday', id: 'b' }),
          ]),
        ),
      );
      createComponent();

      expect(component.getSlotsForDay('Monday').map((s) => s.id)).toEqual(['a']);
    });

    it('orders slots by meal occasion, not by the order the API returned them', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(
        of(
          makePlan([
            makeSlot({ id: 'snack', mealSlot: 'Snack' }),
            makeSlot({ id: 'dinner', mealSlot: 'Dinner' }),
            makeSlot({ id: 'breakfast', mealSlot: 'Breakfast' }),
            makeSlot({ id: 'lunch', mealSlot: 'Lunch' }),
          ]),
        ),
      );
      createComponent();

      expect(component.getSlotsForDay('Monday').map((s) => s.id)).toEqual([
        'breakfast',
        'lunch',
        'dinner',
        'snack',
      ]);
    });

    it('renders a column for every day of the week', () => {
      createComponent();

      expect(fixture.nativeElement.querySelectorAll('.day-column').length).toBe(7);
    });

    it('marks a day with no slots as empty', () => {
      createComponent();

      // Only Monday has a slot, so the other six days show the empty note.
      expect(fixture.nativeElement.querySelectorAll('.empty-day').length).toBe(6);
    });
  });

  // ── Consume / unconsume ─────────────────────────────────────────────────────

  describe('consumeSlot', () => {
    function consumeResult(
      overrides: Partial<ConsumeMealResponse> = {},
    ): ConsumeMealResponse {
      return {
        autoDepleteApplied: false,
        consumedAt: '2026-09-01T18:00:00Z',
        shortIngredients: [],
        ...overrides,
      };
    }

    it('stamps the slot as consumed on success', () => {
      mealPlanServiceMock.consumeSlot.mockReturnValue(of(consumeResult()));
      createComponent();

      component.consumeSlot(makeSlot());

      expect(internals().plan()?.slots[0].consumedAt).toBe('2026-09-01T18:00:00Z');
      expect(internals().consumingSlotId()).toBeNull();
    });

    it('reports a plain confirmation when nothing was depleted', () => {
      mealPlanServiceMock.consumeSlot.mockReturnValue(of(consumeResult()));
      createComponent();

      component.consumeSlot(makeSlot());

      expect(snackBarMock.open).toHaveBeenCalledWith('Marked eaten.', 'Dismiss', {
        duration: 3000,
      });
    });

    it('says inventory was updated when auto-deplete ran', () => {
      mealPlanServiceMock.consumeSlot.mockReturnValue(
        of(consumeResult({ autoDepleteApplied: true })),
      );
      createComponent();

      component.consumeSlot(makeSlot());

      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Marked eaten. Inventory updated.',
        'Dismiss',
        { duration: 3000 },
      );
    });

    it('names the short ingredients, which takes priority over the auto-deplete note', () => {
      mealPlanServiceMock.consumeSlot.mockReturnValue(
        of(
          consumeResult({
            autoDepleteApplied: true,
            shortIngredients: [
              { ingredientName: 'Spinach', shortBy: 50, unitOfMeasureAbbreviation: 'g' },
              { ingredientName: 'Ricotta', shortBy: 2, unitOfMeasureAbbreviation: 'oz' },
            ],
          }),
        ),
      );
      createComponent();

      component.consumeSlot(makeSlot());

      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Marked eaten. Inventory was short on: Spinach (short by 50 g), Ricotta (short by 2 oz)',
        'Dismiss',
        { duration: 8000 },
      );
    });

    it('leaves the slot unconsumed and reports the failure', () => {
      mealPlanServiceMock.consumeSlot.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      component.consumeSlot(makeSlot());

      expect(internals().plan()?.slots[0].consumedAt).toBeNull();
      expect(internals().consumingSlotId()).toBeNull();
      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Could not mark the meal as eaten.',
        'Dismiss',
        { duration: 5000 },
      );
    });
  });

  describe('unconsumeSlot', () => {
    it('clears the consumed stamp on success', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(
        of(makePlan([makeSlot({ consumedAt: '2026-09-01T18:00:00Z' })])),
      );
      mealPlanServiceMock.unconsumeSlot.mockReturnValue(of(undefined));
      createComponent();

      component.unconsumeSlot(makeSlot());

      expect(internals().plan()?.slots[0].consumedAt).toBeNull();
    });

    it('keeps the stamp and reports the failure', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(
        of(makePlan([makeSlot({ consumedAt: '2026-09-01T18:00:00Z' })])),
      );
      mealPlanServiceMock.unconsumeSlot.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      component.unconsumeSlot(makeSlot());

      expect(internals().plan()?.slots[0].consumedAt).toBe('2026-09-01T18:00:00Z');
      expect(snackBarMock.open).toHaveBeenCalledWith('Could not unmark the meal.', 'Dismiss', {
        duration: 5000,
      });
    });
  });

  // ── Generate ────────────────────────────────────────────────────────────────

  describe('openGenerateDialog', () => {
    it('does nothing when the dialog is dismissed', () => {
      dialogMock.open.mockReturnValue(dialogReturning(undefined));
      createComponent();

      component.openGenerateDialog();

      expect(mealPlanServiceMock.generatePlan).not.toHaveBeenCalled();
    });

    it('generates and replaces the board with the new plan', () => {
      dialogMock.open.mockReturnValue(dialogReturning({ name: 'Next week' }));
      mealPlanServiceMock.generatePlan.mockReturnValue(
        of({ ...makePlan(), id: 'plan-2', name: 'Next week' }),
      );
      createComponent();

      component.openGenerateDialog();

      expect(mealPlanServiceMock.generatePlan).toHaveBeenCalledWith({ name: 'Next week' });
      expect(internals().plan()?.id).toBe('plan-2');
      expect(internals().loading()).toBe(false);
    });

    it('surfaces an error when generation fails', () => {
      dialogMock.open.mockReturnValue(dialogReturning({}));
      mealPlanServiceMock.generatePlan.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      component.openGenerateDialog();

      expect(internals().error()).toBe(true);
      expect(internals().loading()).toBe(false);
    });
  });

  // ── Swap ────────────────────────────────────────────────────────────────────

  describe('openSwapDialog', () => {
    it('passes the prefetched recipes and the true library total to the dialog', () => {
      recipeServiceMock.getRecipes.mockReturnValue(
        makeRecipePageObservable([{ id: 'r1' }], 500),
      );
      createComponent();

      component.openSwapDialog(makeSlot());

      expect(dialogMock.open).toHaveBeenLastCalledWith(expect.anything(), {
        data: {
          currentRecipeId: 'recipe-1',
          currentRecipeTitle: 'Spinach Lasagna',
          loadFailed: false,
          recipes: [expect.objectContaining({ id: 'r1' })],
          totalCount: 500,
        },
      });
    });

    it('tells the dialog when the recipe prefetch failed, so it can say so', () => {
      recipeServiceMock.getRecipes.mockReturnValue(throwError(() => new Error('network')));
      createComponent();

      component.openSwapDialog(makeSlot());

      expect(dialogMock.open).toHaveBeenLastCalledWith(
        expect.anything(),
        expect.objectContaining({
          data: expect.objectContaining({ loadFailed: true, recipes: [], totalCount: 0 }),
        }),
      );
    });

    it('does nothing when the swap is cancelled', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning(undefined));

      component.openSwapDialog(makeSlot());

      expect(mealPlanServiceMock.swapSlot).not.toHaveBeenCalled();
    });

    it('replaces the slot in place with the swapped recipe', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning('recipe-9'));
      mealPlanServiceMock.swapSlot.mockReturnValue(
        of(makeSlot({ recipeId: 'recipe-9', recipeTitle: 'Risotto' })),
      );

      component.openSwapDialog(makeSlot());

      expect(mealPlanServiceMock.swapSlot).toHaveBeenCalledWith('slot-1', {
        recipeId: 'recipe-9',
      });
      expect(internals().plan()?.slots.length).toBe(1);
      expect(internals().plan()?.slots[0].recipeTitle).toBe('Risotto');
    });
  });

  // ── Reorder by expiry ───────────────────────────────────────────────────────

  describe('openReorderDialog', () => {
    function preview(overrides: Partial<ReorderPreviewResponse> = {}): ReorderPreviewResponse {
      return {
        changes: [],
        hasChanges: true,
        reason: null,
        urgencyWindowDays: 5,
        ...overrides,
      };
    }

    it('does nothing when there is no plan to reorder', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));
      createComponent();

      component.openReorderDialog();

      expect(mealPlanServiceMock.previewReorderByExpiry).not.toHaveBeenCalled();
    });

    it('reports a failed preview without opening the dialog', () => {
      mealPlanServiceMock.previewReorderByExpiry.mockReturnValue(
        throwError(() => new Error('boom')),
      );
      createComponent();
      dialogMock.open.mockClear();

      component.openReorderDialog();

      expect(dialogMock.open).not.toHaveBeenCalled();
      expect(internals().reordering()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Could not compute reorder preview.',
        'Dismiss',
        { duration: 5000 },
      );
    });

    it('applies nothing when the preview is dismissed', () => {
      mealPlanServiceMock.previewReorderByExpiry.mockReturnValue(of(preview()));
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning(false));

      component.openReorderDialog();

      expect(mealPlanServiceMock.applyReorderByExpiry).not.toHaveBeenCalled();
    });

    it('applies the reorder with the window the preview reported, not a fresh default', () => {
      mealPlanServiceMock.previewReorderByExpiry.mockReturnValue(
        of(preview({ urgencyWindowDays: 3 })),
      );
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning(true));
      mealPlanServiceMock.applyReorderByExpiry.mockReturnValue(
        of({ ...makePlan(), id: 'plan-reordered' }),
      );

      component.openReorderDialog();

      expect(mealPlanServiceMock.applyReorderByExpiry).toHaveBeenCalledWith('plan-1', 3);
      expect(internals().plan()?.id).toBe('plan-reordered');
      expect(snackBarMock.open).toHaveBeenCalledWith('Meal plan reordered.', 'Dismiss', {
        duration: 3000,
      });
    });

    it('reports a failed apply and leaves the board on the original plan', () => {
      mealPlanServiceMock.previewReorderByExpiry.mockReturnValue(of(preview()));
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning(true));
      mealPlanServiceMock.applyReorderByExpiry.mockReturnValue(
        throwError(() => new Error('boom')),
      );

      component.openReorderDialog();

      expect(internals().plan()?.id).toBe('plan-1');
      expect(internals().reordering()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith('Could not apply reorder.', 'Dismiss', {
        duration: 5000,
      });
    });
  });

  // ── Todoist push ────────────────────────────────────────────────────────────

  describe('pushToTodoist', () => {
    it('does nothing when there is no plan to push', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));
      createComponent();
      dialogMock.open.mockClear();

      component.pushToTodoist();

      expect(dialogMock.open).not.toHaveBeenCalled();
    });

    it('pushes nothing when the project picker is dismissed', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning(undefined));

      component.pushToTodoist();

      expect(mealPlanServiceMock.pushToTodoist).not.toHaveBeenCalled();
    });

    it('pushes to the chosen project', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: '69mF7QcCj9JmXxp8' }));
      mealPlanServiceMock.pushToTodoist.mockReturnValue(
        of({ closed: 0, created: 7, unchanged: 0, updated: 0 }),
      );

      component.pushToTodoist();

      expect(mealPlanServiceMock.pushToTodoist).toHaveBeenCalledWith(
        'plan-1',
        '69mF7QcCj9JmXxp8',
      );
    });

    it('pushes to the Inbox when the picker returns a null project', () => {
      // null is a real selection; only undefined means dismissed.
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      mealPlanServiceMock.pushToTodoist.mockReturnValue(
        of({ closed: 0, created: 1, unchanged: 0, updated: 0 }),
      );

      component.pushToTodoist();

      expect(mealPlanServiceMock.pushToTodoist).toHaveBeenCalledWith('plan-1', null);
    });

    it('summarizes only the non-zero counters', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      mealPlanServiceMock.pushToTodoist.mockReturnValue(
        of({ closed: 1, created: 3, unchanged: 0, updated: 2 }),
      );

      component.pushToTodoist();

      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Todoist: 3 created, 2 updated, 1 closed.',
        'Dismiss',
        { duration: 5000 },
      );
    });

    it('says nothing was pushed when every counter is zero', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      mealPlanServiceMock.pushToTodoist.mockReturnValue(
        of({ closed: 0, created: 0, unchanged: 0, updated: 0 }),
      );

      component.pushToTodoist();

      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Todoist: nothing to push.',
        'Dismiss',
        { duration: 5000 },
      );
    });

    it('surfaces the problem detail from a failed push', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      mealPlanServiceMock.pushToTodoist.mockReturnValue(
        throwError(() => ({ error: { detail: 'Todoist token is invalid.' } })),
      );

      component.pushToTodoist();

      expect(internals().pushing()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Todoist token is invalid.',
        'Dismiss',
        { duration: 6000 },
      );
    });

    it('falls back to a generic message when the failure carries no detail', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      mealPlanServiceMock.pushToTodoist.mockReturnValue(throwError(() => ({})));

      component.pushToTodoist();

      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Push to Todoist failed.',
        'Dismiss',
        { duration: 6000 },
      );
    });

    it('disables the push button while Todoist is not configured', () => {
      configured.set(false);
      createComponent();

      expect(buttonWithText('Push to Todoist')?.disabled).toBe(true);
    });

    it('enables the push button once Todoist is configured', () => {
      createComponent();

      expect(buttonWithText('Push to Todoist')?.disabled).toBe(false);
    });
  });

  // ── Header affordances depend on a plan existing ────────────────────────────

  describe('header', () => {
    it('hides the reorder and push buttons when there is no plan', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));
      createComponent();

      expect(buttonWithText('Reorder by expiry')).toBeUndefined();
      expect(buttonWithText('Push to Todoist')).toBeUndefined();
    });

    it('always offers Generate Plan', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));
      createComponent();

      expect(buttonWithText('Generate Plan')).toBeDefined();
    });
  });

  /** Recipe page observable with the given partial items and library total. */
  function makeRecipePageObservable(
    items: Partial<RecipeListItemDto>[],
    totalCount: number,
  ): Observable<PagedResult<RecipeListItemDto>> {
    return of(
      makeRecipePage(
        items.map((item) => ({
          cuisineType: 'Italian',
          dietaryTags: [],
          id: 'r1',
          isFullyResolved: true,
          title: 'Pasta',
          totalIngredients: 5,
          unresolvedCount: 0,
          ...item,
        })),
        totalCount,
      ),
    );
  }
});
