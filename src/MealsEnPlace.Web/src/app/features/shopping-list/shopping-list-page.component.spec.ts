import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { MealPlanResponse } from '../../core/models/meal-plan.models';
import { ShoppingListItemResponse } from '../../core/models/shopping-list.models';
import { MealPlanService } from '../../core/services/meal-plan.service';
import { ShoppingListService } from '../../core/services/shopping-list.service';
import { TodoistAvailabilityService } from '../../core/services/todoist-availability.service';
import { ShoppingListPageComponent } from './shopping-list-page.component';

describe('ShoppingListPageComponent', () => {
  let component: ShoppingListPageComponent;
  let configured: ReturnType<typeof signal<boolean>>;
  let dialogMock: { open: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<ShoppingListPageComponent>;
  let mealPlanServiceMock: { getActivePlan: ReturnType<typeof vi.fn> };
  let shoppingListServiceMock: {
    generateList: ReturnType<typeof vi.fn>;
    getList: ReturnType<typeof vi.fn>;
    pushMealPlanListToTodoist: ReturnType<typeof vi.fn>;
  };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };

  const PLAN: MealPlanResponse = {
    createdAt: '2026-09-01T00:00:00Z',
    id: 'plan-1',
    name: 'Week of Sept 1',
    slots: [],
    weekStartDate: '2026-09-01',
  };

  function makeItem(
    overrides: Partial<ShoppingListItemResponse> = {},
  ): ShoppingListItemResponse {
    return {
      canonicalIngredientName: 'Diced Tomatoes',
      category: 'Canned',
      id: 'sl-1',
      notes: null,
      quantity: 14.5,
      unitOfMeasureAbbreviation: 'oz',
      ...overrides,
    };
  }

  function dialogReturning(result: unknown): { afterClosed: () => Observable<unknown> } {
    return { afterClosed: () => of(result) };
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [ShoppingListPageComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: MealPlanService, useValue: mealPlanServiceMock },
        { provide: ShoppingListService, useValue: shoppingListServiceMock },
        { provide: TodoistAvailabilityService, useValue: { configured, refresh: vi.fn() } },
      ],
    });

    TestBed.overrideProvider(MatDialog, { useValue: dialogMock });
    TestBed.overrideProvider(MatSnackBar, { useValue: snackBarMock });

    fixture = TestBed.createComponent(ShoppingListPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  interface Internals {
    activePlan: () => MealPlanResponse | null;
    items: () => ShoppingListItemResponse[];
    loading: () => boolean;
    pushing: () => boolean;
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
    mealPlanServiceMock = { getActivePlan: vi.fn().mockReturnValue(of(PLAN)) };
    shoppingListServiceMock = {
      generateList: vi.fn(),
      getList: vi.fn().mockReturnValue(of([makeItem()])),
      pushMealPlanListToTodoist: vi.fn(),
    };
  });

  // ── Load ────────────────────────────────────────────────────────────────────

  describe('load', () => {
    it('fetches the list for the active plan', () => {
      createComponent();

      expect(mealPlanServiceMock.getActivePlan).toHaveBeenCalled();
      expect(shoppingListServiceMock.getList).toHaveBeenCalledWith('plan-1');
      expect(internals().items().length).toBe(1);
      expect(internals().loading()).toBe(false);
    });

    it('renders a row per item', () => {
      shoppingListServiceMock.getList.mockReturnValue(
        of([makeItem(), makeItem({ id: 'sl-2' })]),
      );

      createComponent();

      expect(fixture.nativeElement.querySelectorAll('mat-row').length).toBe(2);
    });

    it('names the plan the list belongs to', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('.plan-context').textContent).toContain(
        'Week of Sept 1',
      );
    });

    it('points the user at meal planning when there is no active plan', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));

      createComponent();

      expect(internals().activePlan()).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('No active meal plan.');
      expect(shoppingListServiceMock.getList).not.toHaveBeenCalled();
    });

    it('celebrates an empty list — inventory already covers the plan', () => {
      shoppingListServiceMock.getList.mockReturnValue(of([]));

      createComponent();

      expect(fixture.nativeElement.textContent).toContain(
        'Your inventory covers everything in this meal plan!',
      );
    });

    it('clears the spinner when the list fetch fails, leaving the plan in place', () => {
      shoppingListServiceMock.getList.mockReturnValue(throwError(() => new Error('boom')));

      createComponent();

      expect(internals().loading()).toBe(false);
      expect(internals().activePlan()).toEqual(PLAN);
    });
  });

  // ── Regenerate ──────────────────────────────────────────────────────────────

  describe('regenerate', () => {
    it('does nothing when there is no plan', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));
      createComponent();

      component.regenerate();

      expect(shoppingListServiceMock.generateList).not.toHaveBeenCalled();
    });

    it('replaces the list with the regenerated items', () => {
      createComponent();
      shoppingListServiceMock.generateList.mockReturnValue(
        of([makeItem({ id: 'sl-new' }), makeItem({ id: 'sl-new-2' })]),
      );

      component.regenerate();

      expect(shoppingListServiceMock.generateList).toHaveBeenCalledWith('plan-1');
      expect(internals().items().map((item) => item.id)).toEqual(['sl-new', 'sl-new-2']);
      expect(internals().loading()).toBe(false);
    });

    it('keeps the previous list and clears the spinner when regeneration fails', () => {
      createComponent();
      shoppingListServiceMock.generateList.mockReturnValue(
        throwError(() => new Error('boom')),
      );

      component.regenerate();

      expect(internals().items().length).toBe(1);
      expect(internals().loading()).toBe(false);
    });

    it('is wired to the Regenerate button', () => {
      createComponent();
      shoppingListServiceMock.generateList.mockReturnValue(of([makeItem()]));
      const regenerate = vi.spyOn(component, 'regenerate');

      buttonWithText('Regenerate')?.click();

      expect(regenerate).toHaveBeenCalled();
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

    it('opens the picker for the shopping-list surface', () => {
      createComponent();

      component.pushToTodoist();

      expect(dialogMock.open).toHaveBeenCalledWith(expect.anything(), {
        data: { resourceType: 'shoppingList' },
      });
    });

    it('pushes nothing when the picker is dismissed', () => {
      createComponent();

      component.pushToTodoist();

      expect(shoppingListServiceMock.pushMealPlanListToTodoist).not.toHaveBeenCalled();
    });

    it('pushes to the chosen project', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: '69mF7QcCj9JmXxp8' }));
      shoppingListServiceMock.pushMealPlanListToTodoist.mockReturnValue(
        of({ closed: 0, created: 5, unchanged: 0, updated: 0 }),
      );

      component.pushToTodoist();

      expect(shoppingListServiceMock.pushMealPlanListToTodoist).toHaveBeenCalledWith(
        'plan-1',
        '69mF7QcCj9JmXxp8',
      );
    });

    it('treats a null project as the Inbox, not as a dismissal', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      shoppingListServiceMock.pushMealPlanListToTodoist.mockReturnValue(
        of({ closed: 0, created: 1, unchanged: 0, updated: 0 }),
      );

      component.pushToTodoist();

      expect(shoppingListServiceMock.pushMealPlanListToTodoist).toHaveBeenCalledWith(
        'plan-1',
        null,
      );
    });

    it('summarizes only the non-zero counters', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      shoppingListServiceMock.pushMealPlanListToTodoist.mockReturnValue(
        of({ closed: 2, created: 0, unchanged: 4, updated: 1 }),
      );

      component.pushToTodoist();

      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Todoist: 1 updated, 2 closed, 4 unchanged.',
        'Dismiss',
        { duration: 5000 },
      );
    });

    it('says nothing was pushed when every counter is zero', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      shoppingListServiceMock.pushMealPlanListToTodoist.mockReturnValue(
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
      shoppingListServiceMock.pushMealPlanListToTodoist.mockReturnValue(
        throwError(() => ({ error: { detail: 'Todoist rejected the request.' } })),
      );

      component.pushToTodoist();

      expect(internals().pushing()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Todoist rejected the request.',
        'Dismiss',
        { duration: 6000 },
      );
    });

    it('falls back to a generic message when the failure carries no detail', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ projectId: null }));
      shoppingListServiceMock.pushMealPlanListToTodoist.mockReturnValue(
        throwError(() => ({})),
      );

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

    it('hides the header actions entirely when there is no plan', () => {
      mealPlanServiceMock.getActivePlan.mockReturnValue(throwError(() => ({ status: 404 })));
      createComponent();

      expect(fixture.nativeElement.querySelector('.header-actions')).toBeNull();
    });
  });
});
