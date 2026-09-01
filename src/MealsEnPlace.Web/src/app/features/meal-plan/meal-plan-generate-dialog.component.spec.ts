import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MealPlanGenerateDialogComponent } from './meal-plan-generate-dialog.component';

describe('MealPlanGenerateDialogComponent', () => {
  let component: MealPlanGenerateDialogComponent;
  let dialogRefMock: { close: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<MealPlanGenerateDialogComponent>;

  beforeEach(() => {
    dialogRefMock = { close: vi.fn() };

    TestBed.configureTestingModule({
      imports: [MealPlanGenerateDialogComponent, NoopAnimationsModule],
      providers: [{ provide: MatDialogRef, useValue: dialogRefMock }],
    });

    fixture = TestBed.createComponent(MealPlanGenerateDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Defaults ────────────────────────────────────────────────────────────────

  describe('defaults', () => {
    it('starts with an empty plan name', () => {
      expect(component.planName).toBe('');
    });

    it('starts with the seasonal preference off', () => {
      expect(component.seasonalOnly).toBe(false);
    });
  });

  // ── Request construction: omit rather than send empty values ─────────────────

  describe('generate', () => {
    it('sends an empty request when nothing was chosen', () => {
      // Absent keys let the API apply its own defaults; empty ones would not.
      component.generate();

      expect(dialogRefMock.close).toHaveBeenCalledWith({});
    });

    it('includes the plan name when one is typed', () => {
      component.planName = 'Week of March 16';

      component.generate();

      expect(dialogRefMock.close).toHaveBeenCalledWith({ name: 'Week of March 16' });
    });

    it('trims surrounding whitespace from the plan name', () => {
      component.planName = '  Week of March 16  ';

      component.generate();

      expect(dialogRefMock.close).toHaveBeenCalledWith({ name: 'Week of March 16' });
    });

    it('omits a whitespace-only plan name entirely', () => {
      component.planName = '   ';

      component.generate();

      expect(dialogRefMock.close).toHaveBeenCalledWith({});
    });

    it('includes seasonalOnly when the preference is on', () => {
      component.seasonalOnly = true;

      component.generate();

      expect(dialogRefMock.close).toHaveBeenCalledWith({ seasonalOnly: true });
    });

    it('omits seasonalOnly when the preference is off rather than sending false', () => {
      component.seasonalOnly = false;

      component.generate();

      expect(dialogRefMock.close.mock.calls[0][0]).not.toHaveProperty('seasonalOnly');
    });

    it('carries both options together', () => {
      component.planName = 'Spring week';
      component.seasonalOnly = true;

      component.generate();

      expect(dialogRefMock.close).toHaveBeenCalledWith({
        name: 'Spring week',
        seasonalOnly: true,
      });
    });
  });

  // ── Wiring ──────────────────────────────────────────────────────────────────

  it('generates when the Generate button is clicked', () => {
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    const generateButton = buttons.find((b) => b.textContent?.trim() === 'Generate');

    generateButton?.click();

    expect(dialogRefMock.close).toHaveBeenCalledWith({});
  });

  it('does not close before the user acts', () => {
    expect(dialogRefMock.close).not.toHaveBeenCalled();
  });
});
