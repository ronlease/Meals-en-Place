import { provideNativeDateAdapter } from '@angular/material/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import {
  CanonicalIngredientDto,
  ContainerReferenceDetectedResponse,
  InventoryItemResponse,
  UnitOfMeasureDto,
} from '../../core/models/inventory.models';
import { InventoryService } from '../../core/services/inventory.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { InventoryDialogComponent, InventoryDialogData } from './inventory-dialog.component';

describe('InventoryDialogComponent', () => {
  const INGREDIENT: CanonicalIngredientDto = {
    category: 'Canned',
    defaultUnitOfMeasureId: 'uom-oz',
    id: 'ing-1',
    name: 'Diced Tomatoes',
  };

  const UNITS: UnitOfMeasureDto[] = [
    { abbreviation: 'oz', id: 'uom-oz', name: 'Ounce', unitOfMeasureType: 'Weight' },
    { abbreviation: 'g', id: 'uom-g', name: 'Gram', unitOfMeasureType: 'Weight' },
  ];

  let component: InventoryDialogComponent;
  let dialogRefMock: { close: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<InventoryDialogComponent>;
  let inventoryServiceMock: {
    addItem: ReturnType<typeof vi.fn>;
    updateItem: ReturnType<typeof vi.fn>;
  };
  let referenceDataServiceMock: {
    createIngredient: ReturnType<typeof vi.fn>;
    getUnits: ReturnType<typeof vi.fn>;
    searchIngredients: ReturnType<typeof vi.fn>;
  };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };

  function makeItem(overrides: Partial<InventoryItemResponse> = {}): InventoryItemResponse {
    return {
      canonicalIngredientId: 'ing-1',
      canonicalIngredientName: 'Diced Tomatoes',
      expiryDate: null,
      id: 'item-1',
      location: 'Pantry',
      notes: null,
      quantity: 14.5,
      unitOfMeasureAbbreviation: 'oz',
      unitOfMeasureId: 'uom-oz',
      ...overrides,
    };
  }

  function createComponent(data: InventoryDialogData): void {
    TestBed.configureTestingModule({
      imports: [InventoryDialogComponent, NoopAnimationsModule],
      providers: [
        provideNativeDateAdapter(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefMock },
        { provide: InventoryService, useValue: inventoryServiceMock },
        { provide: ReferenceDataService, useValue: referenceDataServiceMock },
        { provide: MatSnackBar, useValue: snackBarMock },
      ],
    });

    fixture = TestBed.createComponent(InventoryDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  // ── Accessors for the component's protected surface ──────────────────────────

  interface Internals {
    containerForm: InventoryDialogComponent['containerForm'];
    containerReferenceDetected: () => boolean;
    inventoryForm: InventoryDialogComponent['inventoryForm'];
    loading: () => boolean;
    referenceDataLoading: () => boolean;
    units: () => UnitOfMeasureDto[];
  }

  function internals(): Internals {
    return component as unknown as Internals;
  }

  /** Fills the form to a valid add-mode state with the given ingredient selected. */
  function fillValidForm(): void {
    internals().inventoryForm.controls.canonicalIngredient.setValue(INGREDIENT);
    internals().inventoryForm.controls.quantity.setValue(2);
    internals().inventoryForm.controls.unitOfMeasureId.setValue('uom-oz');
  }

  beforeEach(() => {
    dialogRefMock = { close: vi.fn() };
    inventoryServiceMock = { addItem: vi.fn(), updateItem: vi.fn() };
    referenceDataServiceMock = {
      createIngredient: vi.fn(),
      getUnits: vi.fn().mockReturnValue(of(UNITS)),
      searchIngredients: vi.fn().mockReturnValue(of([])),
    };
    snackBarMock = { open: vi.fn() };
  });

  // ── Reference data loading ──────────────────────────────────────────────────

  describe('reference data', () => {
    it('loads units on init and stops the loading spinner', () => {
      createComponent({ location: 'Pantry', mode: 'add' });

      expect(internals().units()).toEqual(UNITS);
      expect(internals().referenceDataLoading()).toBe(false);
    });

    it('does not call getIngredients — ingredients are fetched on demand by the autocomplete', () => {
      createComponent({ location: 'Pantry', mode: 'add' });

      expect(referenceDataServiceMock.searchIngredients).not.toHaveBeenCalled();
    });

    it('reports a failed unit fetch and stops the spinner', () => {
      referenceDataServiceMock.getUnits.mockReturnValue(throwError(() => new Error('network')));

      createComponent({ location: 'Pantry', mode: 'add' });

      expect(internals().referenceDataLoading()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to load units.', 'Dismiss', {
        duration: 4000,
      });
    });
  });

  // ── Mode-specific seeding ───────────────────────────────────────────────────

  describe('form seeding', () => {
    it('seeds the location from the dialog data in add mode', () => {
      createComponent({ location: 'Freezer', mode: 'add' });

      expect(internals().inventoryForm.controls.location.value).toBe('Freezer');
    });

    it('seeds every field from the item in edit mode', () => {
      const item = makeItem({
        expiryDate: '2026-12-25',
        location: 'Fridge',
        notes: '1 can diced tomatoes',
        quantity: 14.5,
      });

      createComponent({ item, location: 'Pantry', mode: 'edit' });

      const value = internals().inventoryForm.getRawValue();
      expect(value.canonicalIngredient?.id).toBe('ing-1');
      expect(value.canonicalIngredient?.name).toBe('Diced Tomatoes');
      expect(value.location).toBe('Fridge');
      expect(value.notes).toBe('1 can diced tomatoes');
      expect(value.quantity).toBe(14.5);
      expect(value.unitOfMeasureId).toBe('uom-oz');
    });

    it('leaves the expiry date null when the item has none', () => {
      createComponent({ item: makeItem({ expiryDate: null }), location: 'Pantry', mode: 'edit' });

      expect(internals().inventoryForm.controls.expiryDate.value).toBeNull();
    });

    it('substitutes an empty string for absent notes so the control stays non-nullable', () => {
      createComponent({ item: makeItem({ notes: null }), location: 'Pantry', mode: 'edit' });

      expect(internals().inventoryForm.controls.notes.value).toBe('');
    });

    it('seeds the ingredient control with the edited item in edit mode', () => {
      createComponent({ item: makeItem(), location: 'Pantry', mode: 'edit' });

      expect(internals().inventoryForm.controls.canonicalIngredient.value?.id).toBe('ing-1');
    });

    it('titles the dialog "Add Item" in add mode', () => {
      createComponent({ location: 'Pantry', mode: 'add' });

      expect(fixture.nativeElement.textContent).toContain('Add Item');
    });

    it('titles the dialog "Edit Item" in edit mode', () => {
      createComponent({ item: makeItem(), location: 'Pantry', mode: 'edit' });

      expect(fixture.nativeElement.textContent).toContain('Edit Item');
    });
  });

  // ── Ingredient error messages ───────────────────────────────────────────────

  describe('ingredient error messages', () => {
    it('shows only the select-an-ingredient error when text was typed but no option picked', () => {
      createComponent({ location: 'Pantry', mode: 'add' });
      const ctrl = internals().inventoryForm.controls.canonicalIngredient;
      ctrl.markAsDirty();
      ctrl.markAsTouched();
      // value remains null — user typed but did not pick an option
      fixture.detectChanges();

      const errors: NodeListOf<HTMLElement> =
        fixture.nativeElement.querySelectorAll('.field-error');
      expect(errors.length).toBe(1);
      expect(errors[0].textContent?.trim()).toContain('Select an ingredient from the list');
    });

    it('shows only the required error when the field was blurred without typing anything', () => {
      createComponent({ location: 'Pantry', mode: 'add' });
      const ctrl = internals().inventoryForm.controls.canonicalIngredient;
      ctrl.markAsTouched();
      // control is not dirty — the user never typed anything
      fixture.detectChanges();

      const errors: NodeListOf<HTMLElement> =
        fixture.nativeElement.querySelectorAll('.field-error');
      expect(errors.length).toBe(1);
      expect(errors[0].textContent?.trim()).toContain('Ingredient is required');
    });
  });

  // ── Submit: add ─────────────────────────────────────────────────────────────

  describe('add submission', () => {
    it('refuses to submit an invalid form', () => {
      createComponent({ location: 'Pantry', mode: 'add' });

      component.onSubmit();

      expect(inventoryServiceMock.addItem).not.toHaveBeenCalled();
    });

    it('refuses to submit a valid form whose ingredient was never resolved to an ID', () => {
      createComponent({ location: 'Pantry', mode: 'add' });
      internals().inventoryForm.controls.quantity.setValue(2);
      internals().inventoryForm.controls.unitOfMeasureId.setValue('uom-oz');
      // canonicalIngredient remains null

      component.onSubmit();

      expect(inventoryServiceMock.addItem).not.toHaveBeenCalled();
    });

    it('posts the resolved ingredient ID with null container declarations', () => {
      createComponent({ location: 'Pantry', mode: 'add' });
      fillValidForm();
      inventoryServiceMock.addItem.mockReturnValue(of(makeItem()));

      component.onSubmit();

      expect(inventoryServiceMock.addItem).toHaveBeenCalledWith({
        canonicalIngredientId: 'ing-1',
        declaredQuantity: null,
        declaredUnitOfMeasureId: null,
        expiryDate: null,
        location: 'Pantry',
        notes: '',
        quantity: 2,
        unitOfMeasureId: 'uom-oz',
      });
    });

    it('sends the expiry date as a date-only string, not a full timestamp', () => {
      createComponent({ location: 'Pantry', mode: 'add' });
      fillValidForm();
      internals().inventoryForm.controls.expiryDate.setValue(new Date(Date.UTC(2026, 11, 25)));
      inventoryServiceMock.addItem.mockReturnValue(of(makeItem()));

      component.onSubmit();

      expect(inventoryServiceMock.addItem.mock.calls[0][0].expiryDate).toBe('2026-12-25');
    });

    it('closes with the created item on success', () => {
      createComponent({ location: 'Pantry', mode: 'add' });
      fillValidForm();
      const created = makeItem({ id: 'item-new' });
      inventoryServiceMock.addItem.mockReturnValue(of(created));

      component.onSubmit();

      expect(dialogRefMock.close).toHaveBeenCalledWith(created);
      expect(snackBarMock.open).toHaveBeenCalledWith('Item added.', undefined, {
        duration: 2500,
      });
    });

    it('reports the server message and stays open when the add fails', () => {
      createComponent({ location: 'Pantry', mode: 'add' });
      fillValidForm();
      inventoryServiceMock.addItem.mockReturnValue(
        throwError(() => ({ error: { message: 'Quantity must be positive.' } })),
      );

      component.onSubmit();

      expect(dialogRefMock.close).not.toHaveBeenCalled();
      expect(snackBarMock.open).toHaveBeenCalledWith('Quantity must be positive.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('falls back to a generic add-failure message', () => {
      createComponent({ location: 'Pantry', mode: 'add' });
      fillValidForm();
      inventoryServiceMock.addItem.mockReturnValue(throwError(() => ({})));

      component.onSubmit();

      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to add item.', 'Dismiss', {
        duration: 4000,
      });
    });
  });

  // ── Container reference resolution ──────────────────────────────────────────

  describe('container reference detection', () => {
    const DETECTED: ContainerReferenceDetectedResponse = {
      detectedKeyword: 'can',
      message: 'What is the net weight or volume of this container?',
      originalInput: '1 can of diced tomatoes',
    };

    function submitAndDetect(): void {
      createComponent({ location: 'Pantry', mode: 'add' });
      fillValidForm();
      inventoryServiceMock.addItem.mockReturnValue(of(DETECTED));
      component.onSubmit();
      fixture.detectChanges();
    }

    it('switches into declaration mode instead of closing the dialog', () => {
      submitAndDetect();

      expect(internals().containerReferenceDetected()).toBe(true);
      expect(dialogRefMock.close).not.toHaveBeenCalled();
    });

    it('renders the prompt from the server message', () => {
      submitAndDetect();

      const prompt = fixture.nativeElement.querySelector('.container-reference-prompt');
      expect(prompt.textContent).toContain('What is the net weight or volume of this container?');
    });

    it('relabels the confirm button to "Declare & Save"', () => {
      submitAndDetect();

      expect(fixture.nativeElement.textContent).toContain('Declare & Save');
    });

    it('refuses to resubmit until the declaration is filled in', () => {
      submitAndDetect();
      inventoryServiceMock.addItem.mockClear();

      component.onSubmit();

      expect(inventoryServiceMock.addItem).not.toHaveBeenCalled();
    });

    it('resubmits with the declared quantity and unit once they are supplied', () => {
      submitAndDetect();
      internals().containerForm.setValue({
        declaredQuantity: 14.5,
        declaredUnitOfMeasureId: 'uom-oz',
      });
      inventoryServiceMock.addItem.mockClear();
      inventoryServiceMock.addItem.mockReturnValue(of(makeItem()));

      component.onSubmit();

      expect(inventoryServiceMock.addItem).toHaveBeenCalledWith(
        expect.objectContaining({
          declaredQuantity: 14.5,
          declaredUnitOfMeasureId: 'uom-oz',
        }),
      );
    });

    it('closes with the item once the declaration is accepted', () => {
      submitAndDetect();
      internals().containerForm.setValue({
        declaredQuantity: 14.5,
        declaredUnitOfMeasureId: 'uom-oz',
      });
      const created = makeItem({ id: 'item-declared' });
      inventoryServiceMock.addItem.mockReturnValue(of(created));

      component.onSubmit();

      expect(dialogRefMock.close).toHaveBeenCalledWith(created);
    });

    it('reports rather than closes when the API detects a container reference a second time', () => {
      submitAndDetect();
      internals().containerForm.setValue({
        declaredQuantity: 14.5,
        declaredUnitOfMeasureId: 'uom-oz',
      });
      inventoryServiceMock.addItem.mockReturnValue(of(DETECTED));

      component.onSubmit();

      expect(dialogRefMock.close).not.toHaveBeenCalled();
      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Container reference still detected. Please try again.',
        'Dismiss',
        { duration: 4000 },
      );
    });

    it('surfaces the server message when the declared save fails', () => {
      submitAndDetect();
      internals().containerForm.setValue({
        declaredQuantity: 14.5,
        declaredUnitOfMeasureId: 'uom-oz',
      });
      inventoryServiceMock.addItem.mockReturnValue(
        throwError(() => ({ error: { message: 'Unit is not compatible.' } })),
      );

      component.onSubmit();

      expect(snackBarMock.open).toHaveBeenCalledWith('Unit is not compatible.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('falls back to a generic message when the declared save fails without one', () => {
      submitAndDetect();
      internals().containerForm.setValue({
        declaredQuantity: 14.5,
        declaredUnitOfMeasureId: 'uom-oz',
      });
      inventoryServiceMock.addItem.mockReturnValue(throwError(() => ({})));

      component.onSubmit();

      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to save item.', 'Dismiss', {
        duration: 4000,
      });
    });
  });

  // ── Submit: edit ────────────────────────────────────────────────────────────

  describe('edit submission', () => {
    it('PUTs the edited values without an ingredient ID — the ingredient cannot change', () => {
      createComponent({ item: makeItem(), location: 'Pantry', mode: 'edit' });
      internals().inventoryForm.controls.quantity.setValue(20);
      inventoryServiceMock.updateItem.mockReturnValue(of(makeItem({ quantity: 20 })));

      component.onSubmit();

      expect(inventoryServiceMock.updateItem).toHaveBeenCalledWith('item-1', {
        expiryDate: null,
        location: 'Pantry',
        notes: '',
        quantity: 20,
        unitOfMeasureId: 'uom-oz',
      });
    });

    it('closes with the updated item on success', () => {
      createComponent({ item: makeItem(), location: 'Pantry', mode: 'edit' });
      const updated = makeItem({ quantity: 20 });
      inventoryServiceMock.updateItem.mockReturnValue(of(updated));

      component.onSubmit();

      expect(dialogRefMock.close).toHaveBeenCalledWith(updated);
      expect(snackBarMock.open).toHaveBeenCalledWith('Item updated.', undefined, {
        duration: 2500,
      });
    });

    it('surfaces the server message and stays open when the update fails', () => {
      createComponent({ item: makeItem(), location: 'Pantry', mode: 'edit' });
      inventoryServiceMock.updateItem.mockReturnValue(
        throwError(() => ({ error: { message: 'Item no longer exists.' } })),
      );

      component.onSubmit();

      expect(dialogRefMock.close).not.toHaveBeenCalled();
      expect(snackBarMock.open).toHaveBeenCalledWith('Item no longer exists.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('falls back to a generic update-failure message', () => {
      createComponent({ item: makeItem(), location: 'Pantry', mode: 'edit' });
      inventoryServiceMock.updateItem.mockReturnValue(throwError(() => ({})));

      component.onSubmit();

      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to update item.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('refuses to submit when a required field has been cleared', () => {
      createComponent({ item: makeItem(), location: 'Pantry', mode: 'edit' });
      internals().inventoryForm.controls.quantity.setValue(null);

      component.onSubmit();

      expect(inventoryServiceMock.updateItem).not.toHaveBeenCalled();
    });
  });
});
