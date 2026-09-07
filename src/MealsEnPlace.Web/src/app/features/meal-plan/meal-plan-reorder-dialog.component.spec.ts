import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import {
  ReorderedSlotDto,
  ReorderPreviewResponse,
} from '../../core/models/meal-plan.models';
import {
  MealPlanReorderDialogComponent,
  ReorderDialogData,
} from './meal-plan-reorder-dialog.component';

describe('MealPlanReorderDialogComponent', () => {
  let dialogRefMock: { close: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<MealPlanReorderDialogComponent>;

  function makeChange(overrides: Partial<ReorderedSlotDto> = {}): ReorderedSlotDto {
    return {
      id: 'slot-1',
      mealSlot: 'Dinner',
      originalDay: 'Friday',
      proposedDay: 'Monday',
      recipeId: 'recipe-1',
      recipeTitle: 'Spinach Lasagna',
      urgencyScore: 0.876,
      ...overrides,
    };
  }

  function createComponent(preview: ReorderPreviewResponse): void {
    dialogRefMock = { close: vi.fn() };
    const data: ReorderDialogData = { preview };

    TestBed.configureTestingModule({
      imports: [MealPlanReorderDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefMock },
      ],
    });

    fixture = TestBed.createComponent(MealPlanReorderDialogComponent);
    fixture.detectChanges();
  }

  function buttonLabels(): string[] {
    return (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[])
      .map((button) => button.textContent?.trim() ?? '');
  }

  function clickButton(label: string): void {
    const button = (
      Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
    ).find((candidate) => candidate.textContent?.trim() === label);
    if (!button) {
      throw new Error(`No button labelled "${label}"`);
    }
    button.click();
  }

  // ── No-changes state ────────────────────────────────────────────────────────

  describe('when the plan needs no reordering', () => {
    const NO_CHANGES: ReorderPreviewResponse = {
      changes: [],
      hasChanges: false,
      reason: 'Nothing in inventory is close to expiry.',
      urgencyWindowDays: 5,
    };

    it('explains why, using the reason from the API', () => {
      createComponent(NO_CHANGES);

      expect(fixture.nativeElement.querySelector('.no-changes').textContent).toContain(
        'Nothing in inventory is close to expiry.',
      );
    });

    it('renders no changes table', () => {
      createComponent(NO_CHANGES);

      expect(fixture.nativeElement.querySelector('.changes-table')).toBeNull();
    });

    it('offers only a Close button — there is nothing to apply', () => {
      createComponent(NO_CHANGES);

      expect(buttonLabels()).toEqual(['Close']);
    });

    it('closes with false so the caller applies nothing', () => {
      createComponent(NO_CHANGES);

      clickButton('Close');

      expect(dialogRefMock.close).toHaveBeenCalledWith(false);
    });
  });

  // ── Proposed-changes state ──────────────────────────────────────────────────

  describe('when the plan would be reordered', () => {
    const WITH_CHANGES: ReorderPreviewResponse = {
      changes: [makeChange(), makeChange({ id: 'slot-2', mealSlot: 'Lunch' })],
      hasChanges: true,
      reason: null,
      urgencyWindowDays: 3,
    };

    it('renders a row per proposed change', () => {
      createComponent(WITH_CHANGES);

      expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(2);
    });

    it('shows the day movement for each change', () => {
      createComponent(WITH_CHANGES);

      const firstRow = fixture.nativeElement.querySelector('tbody tr').textContent;
      expect(firstRow).toContain('Friday');
      expect(firstRow).toContain('Monday');
    });

    it('shows the meal occasion, which the reorder never changes', () => {
      createComponent(WITH_CHANGES);

      expect(fixture.nativeElement.querySelector('tbody tr').textContent).toContain('Dinner');
    });

    it('rounds the urgency score to two decimal places', () => {
      createComponent(WITH_CHANGES);

      expect(fixture.nativeElement.querySelector('tbody tr').textContent).toContain('0.88');
    });

    it('states the urgency window in the legend', () => {
      createComponent(WITH_CHANGES);

      expect(fixture.nativeElement.querySelector('.legend').textContent).toContain(
        'next 3 day(s)',
      );
    });

    it('offers both Cancel and Apply reorder', () => {
      createComponent(WITH_CHANGES);

      expect(buttonLabels()).toEqual(['Cancel', 'Apply reorder']);
    });

    it('closes with true when the reorder is applied', () => {
      createComponent(WITH_CHANGES);

      clickButton('Apply reorder');

      expect(dialogRefMock.close).toHaveBeenCalledWith(true);
    });

    it('closes with false when the reorder is cancelled', () => {
      createComponent(WITH_CHANGES);

      clickButton('Cancel');

      expect(dialogRefMock.close).toHaveBeenCalledWith(false);
    });

    it('does not close before the user decides', () => {
      createComponent(WITH_CHANGES);

      expect(dialogRefMock.close).not.toHaveBeenCalled();
    });
  });
});
