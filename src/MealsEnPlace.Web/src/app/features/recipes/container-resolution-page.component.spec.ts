import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { Observable, of, throwError } from 'rxjs';
import { UnresolvedGroupResponse } from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';
import { ContainerResolutionPageComponent } from './container-resolution-page.component';

describe('ContainerResolutionPageComponent', () => {
  let component: ContainerResolutionPageComponent;
  let dialogMock: { open: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<ContainerResolutionPageComponent>;
  let recipeServiceMock: { getUnresolvedGroups: ReturnType<typeof vi.fn> };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };

  function makeGroup(overrides: Partial<UnresolvedGroupResponse> = {}): UnresolvedGroupResponse {
    return {
      canonicalIngredientId: 'ing-1',
      canonicalIngredientName: 'Diced Tomatoes',
      notes: '1 can chopped tomatoes',
      occurrenceCount: 42,
      ...overrides,
    };
  }

  function dialogReturning(result: unknown): { afterClosed: () => Observable<unknown> } {
    return { afterClosed: () => of(result) };
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [ContainerResolutionPageComponent, NoopAnimationsModule],
      providers: [{ provide: RecipeService, useValue: recipeServiceMock }],
    });

    TestBed.overrideProvider(MatDialog, { useValue: dialogMock });
    TestBed.overrideProvider(MatSnackBar, { useValue: snackBarMock });

    fixture = TestBed.createComponent(ContainerResolutionPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  interface Internals {
    error: () => boolean;
    groups: () => UnresolvedGroupResponse[];
    loading: () => boolean;
    load: () => void;
    totalOccurrences: () => number;
  }

  function internals(): Internals {
    return component as unknown as Internals;
  }

  beforeEach(() => {
    dialogMock = { open: vi.fn().mockReturnValue(dialogReturning(undefined)) };
    snackBarMock = { open: vi.fn() };
    recipeServiceMock = { getUnresolvedGroups: vi.fn().mockReturnValue(of([makeGroup()])) };
  });

  // ── Load ────────────────────────────────────────────────────────────────────

  describe('load', () => {
    it('fetches the unresolved groups on init', () => {
      createComponent();

      expect(recipeServiceMock.getUnresolvedGroups).toHaveBeenCalled();
      expect(internals().groups().length).toBe(1);
      expect(internals().loading()).toBe(false);
    });

    it('renders a row per group', () => {
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(
        of([makeGroup(), makeGroup({ canonicalIngredientId: 'ing-2' })]),
      );

      createComponent();

      expect(fixture.nativeElement.querySelectorAll('mat-row').length).toBe(2);
    });

    it('celebrates an empty queue rather than showing a bare table', () => {
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(of([]));

      createComponent();

      expect(fixture.nativeElement.textContent).toContain(
        "No unresolved container references. You're all caught up.",
      );
    });

    it('shows the error state when the fetch fails', () => {
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(throwError(() => new Error('boom')));

      createComponent();

      expect(internals().error()).toBe(true);
      expect(internals().loading()).toBe(false);
      expect(fixture.nativeElement.querySelector('.error-message')).not.toBeNull();
    });

    it('recovers when Retry is clicked', () => {
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      recipeServiceMock.getUnresolvedGroups.mockReturnValue(of([makeGroup()]));
      (fixture.nativeElement.querySelector('.error-message button') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(internals().error()).toBe(false);
      expect(internals().groups().length).toBe(1);
    });
  });

  // ── Summary counters ────────────────────────────────────────────────────────

  describe('summary', () => {
    it('sums the occurrences across every group', () => {
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(
        of([
          makeGroup({ occurrenceCount: 42 }),
          makeGroup({ canonicalIngredientId: 'ing-2', occurrenceCount: 8 }),
        ]),
      );

      createComponent();

      expect(internals().totalOccurrences()).toBe(50);
    });

    it('is zero when the queue is empty', () => {
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(of([]));

      createComponent();

      expect(internals().totalOccurrences()).toBe(0);
    });

    it('pluralizes the group and ingredient counters', () => {
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(
        of([
          makeGroup({ occurrenceCount: 42 }),
          makeGroup({ canonicalIngredientId: 'ing-2', occurrenceCount: 8 }),
        ]),
      );

      createComponent();

      const summary = fixture.nativeElement.querySelector('.summary-bar').textContent;
      expect(summary).toContain('2 groups');
      expect(summary).toContain('50 ingredients pending');
    });

    it('uses the singular for a queue of one', () => {
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(
        of([makeGroup({ occurrenceCount: 1 })]),
      );

      createComponent();

      const summary = fixture.nativeElement.querySelector('.summary-bar').textContent;
      expect(summary).toContain('1 group');
      expect(summary).not.toContain('1 groups');
      expect(summary).toContain('1 ingredient pending');
    });
  });

  // ── Resolve dialog ──────────────────────────────────────────────────────────

  describe('openResolveDialog', () => {
    it('passes the group identity and source phrase to the dialog', () => {
      createComponent();

      component.openResolveDialog(makeGroup());

      expect(dialogMock.open).toHaveBeenCalledWith(expect.anything(), {
        data: {
          canonicalIngredientId: 'ing-1',
          canonicalIngredientName: 'Diced Tomatoes',
          notes: '1 can chopped tomatoes',
          occurrenceCount: 42,
        },
        width: '480px',
      });
    });

    it('does not refetch when the dialog is dismissed', () => {
      createComponent();
      recipeServiceMock.getUnresolvedGroups.mockClear();

      component.openResolveDialog(makeGroup());

      expect(recipeServiceMock.getUnresolvedGroups).not.toHaveBeenCalled();
      expect(snackBarMock.open).not.toHaveBeenCalled();
    });

    it('reports the count and refetches the queue after a resolution', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ affectedCount: 42 }));
      recipeServiceMock.getUnresolvedGroups.mockClear();
      recipeServiceMock.getUnresolvedGroups.mockReturnValue(of([]));

      component.openResolveDialog(makeGroup());

      expect(snackBarMock.open).toHaveBeenCalledWith('Resolved 42 ingredients.', 'Dismiss', {
        duration: 4000,
      });
      expect(recipeServiceMock.getUnresolvedGroups).toHaveBeenCalled();
      expect(internals().groups()).toEqual([]);
    });

    it('uses the singular when exactly one ingredient was resolved', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning({ affectedCount: 1 }));

      component.openResolveDialog(makeGroup());

      expect(snackBarMock.open).toHaveBeenCalledWith('Resolved 1 ingredient.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('opens from the row action button', () => {
      createComponent();
      const openResolveDialog = vi.spyOn(component, 'openResolveDialog');

      (fixture.nativeElement.querySelector('.actions-cell button') as HTMLButtonElement).click();

      expect(openResolveDialog).toHaveBeenCalled();
    });
  });
});
