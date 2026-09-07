import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { UnitOfMeasureDto } from '../../core/models/inventory.models';
import { RecipeService } from '../../core/services/recipe.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import {
  BulkResolveDialogComponent,
  BulkResolveDialogData,
} from './bulk-resolve-dialog.component';

describe('BulkResolveDialogComponent', () => {
  const DATA: BulkResolveDialogData = {
    canonicalIngredientId: 'ing-1',
    canonicalIngredientName: 'Diced Tomatoes',
    notes: '1 can chopped tomatoes',
    occurrenceCount: 42,
  };

  const UNITS: UnitOfMeasureDto[] = [
    { abbreviation: 'oz', id: 'uom-oz', name: 'Ounce', unitOfMeasureType: 'Weight' },
    { abbreviation: 'g', id: 'uom-g', name: 'Gram', unitOfMeasureType: 'Weight' },
  ];

  let component: BulkResolveDialogComponent;
  let dialogRefMock: { close: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<BulkResolveDialogComponent>;
  let recipeServiceMock: { bulkResolveGroup: ReturnType<typeof vi.fn> };
  let referenceDataServiceMock: { getUnits: ReturnType<typeof vi.fn> };

  function createComponent(data: BulkResolveDialogData = DATA): void {
    TestBed.configureTestingModule({
      imports: [BulkResolveDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefMock },
        { provide: RecipeService, useValue: recipeServiceMock },
        { provide: ReferenceDataService, useValue: referenceDataServiceMock },
      ],
    });

    fixture = TestBed.createComponent(BulkResolveDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  interface Internals {
    errorMessage: () => string | null;
    form: BulkResolveDialogComponent['form'];
    submitting: () => boolean;
    units: () => UnitOfMeasureDto[];
    unitsLoading: () => boolean;
  }

  function internals(): Internals {
    return component as unknown as Internals;
  }

  beforeEach(() => {
    dialogRefMock = { close: vi.fn() };
    recipeServiceMock = { bulkResolveGroup: vi.fn() };
    referenceDataServiceMock = { getUnits: vi.fn().mockReturnValue(of(UNITS)) };
  });

  // ── Units ───────────────────────────────────────────────────────────────────

  describe('units', () => {
    it('loads the units on init', () => {
      createComponent();

      expect(internals().units()).toEqual(UNITS);
      expect(internals().unitsLoading()).toBe(false);
    });

    it('reports a failed unit load in the dialog rather than a snack bar', () => {
      referenceDataServiceMock.getUnits.mockReturnValue(throwError(() => new Error('boom')));

      createComponent();

      expect(internals().unitsLoading()).toBe(false);
      expect(internals().errorMessage()).toBe(
        'Failed to load units of measure. Close and retry.',
      );
      expect(fixture.nativeElement.querySelector('.error-banner')).not.toBeNull();
    });
  });

  // ── Impact statement ────────────────────────────────────────────────────────
  //
  // The declaration fans out across every recipe using the same phrase, so the
  // dialog states the blast radius before the user commits.

  describe('impact statement', () => {
    it('names the ingredient being resolved', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('h2').textContent).toContain(
        'Diced Tomatoes',
      );
    });

    it('quotes the source phrase verbatim', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('.phrase-label').textContent).toContain(
        '1 can chopped tomatoes',
      );
    });

    it('states how many ingredients will change, pluralized', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('.impact-banner').textContent).toContain(
        '42',
      );
      expect(fixture.nativeElement.querySelector('.impact-banner').textContent).toContain(
        'ingredients across',
      );
    });

    it('uses the singular for a single occurrence', () => {
      createComponent({ ...DATA, occurrenceCount: 1 });

      expect(fixture.nativeElement.querySelector('.impact-banner').textContent).toContain(
        'ingredient across',
      );
    });
  });

  // ── Validation ──────────────────────────────────────────────────────────────

  describe('validation', () => {
    it('refuses to submit an empty form and marks it touched', () => {
      createComponent();

      component.submit();

      expect(recipeServiceMock.bulkResolveGroup).not.toHaveBeenCalled();
      expect(internals().form.controls.quantity.touched).toBe(true);
    });

    it('refuses a quantity below the minimum', () => {
      createComponent();
      internals().form.setValue({ quantity: 0, unitOfMeasureId: 'uom-oz' });

      component.submit();

      expect(recipeServiceMock.bulkResolveGroup).not.toHaveBeenCalled();
    });

    it('refuses a quantity with no unit', () => {
      createComponent();
      internals().form.setValue({ quantity: 14.5, unitOfMeasureId: '' });

      component.submit();

      expect(recipeServiceMock.bulkResolveGroup).not.toHaveBeenCalled();
    });
  });

  // ── Submission ──────────────────────────────────────────────────────────────

  describe('submit', () => {
    it('posts the declaration carrying the group identity from the dialog data', () => {
      createComponent();
      internals().form.setValue({ quantity: 14.5, unitOfMeasureId: 'uom-oz' });
      recipeServiceMock.bulkResolveGroup.mockReturnValue(of({ affectedCount: 42 }));

      component.submit();

      expect(recipeServiceMock.bulkResolveGroup).toHaveBeenCalledWith({
        canonicalIngredientId: 'ing-1',
        notes: '1 can chopped tomatoes',
        quantity: 14.5,
        unitOfMeasureId: 'uom-oz',
      });
    });

    it('closes with the affected count so the caller can report it', () => {
      createComponent();
      internals().form.setValue({ quantity: 14.5, unitOfMeasureId: 'uom-oz' });
      recipeServiceMock.bulkResolveGroup.mockReturnValue(of({ affectedCount: 42 }));

      component.submit();

      expect(dialogRefMock.close).toHaveBeenCalledWith({ affectedCount: 42 });
      expect(internals().submitting()).toBe(false);
    });

    it('clears a previous error before retrying', () => {
      createComponent();
      internals().form.setValue({ quantity: 14.5, unitOfMeasureId: 'uom-oz' });
      recipeServiceMock.bulkResolveGroup.mockReturnValue(throwError(() => ({})));
      component.submit();

      recipeServiceMock.bulkResolveGroup.mockReturnValue(of({ affectedCount: 1 }));
      component.submit();

      expect(internals().errorMessage()).toBeNull();
    });

    it('shows the problem detail in the dialog and stays open on failure', () => {
      createComponent();
      internals().form.setValue({ quantity: 14.5, unitOfMeasureId: 'uom-oz' });
      recipeServiceMock.bulkResolveGroup.mockReturnValue(
        throwError(() => ({ error: { detail: 'Unit is not compatible with the ingredient.' } })),
      );

      component.submit();
      fixture.detectChanges();

      expect(dialogRefMock.close).not.toHaveBeenCalled();
      expect(internals().submitting()).toBe(false);
      expect(fixture.nativeElement.querySelector('.error-banner').textContent).toContain(
        'Unit is not compatible with the ingredient.',
      );
    });

    it('falls back to a generic message when the failure carries no detail', () => {
      createComponent();
      internals().form.setValue({ quantity: 14.5, unitOfMeasureId: 'uom-oz' });
      recipeServiceMock.bulkResolveGroup.mockReturnValue(throwError(() => ({})));

      component.submit();

      expect(internals().errorMessage()).toBe(
        'Failed to apply the resolution. Please try again.',
      );
    });
  });

  // ── Cancel ──────────────────────────────────────────────────────────────────

  describe('cancel', () => {
    it('closes with undefined so the caller applies nothing', () => {
      createComponent();

      component.cancel();

      expect(dialogRefMock.close).toHaveBeenCalledWith(undefined);
      expect(recipeServiceMock.bulkResolveGroup).not.toHaveBeenCalled();
    });

    it('does not close before the user acts', () => {
      createComponent();

      expect(dialogRefMock.close).not.toHaveBeenCalled();
    });
  });
});
