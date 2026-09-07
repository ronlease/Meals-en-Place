import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { InventoryItemResponse } from '../../core/models/inventory.models';
import { InventoryService } from '../../core/services/inventory.service';
import { InventoryTableComponent } from './inventory-table.component';

describe('InventoryTableComponent', () => {
  let component: InventoryTableComponent;
  let dialogMock: { open: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<InventoryTableComponent>;
  let inventoryServiceMock: {
    deleteItem: ReturnType<typeof vi.fn>;
    getItems: ReturnType<typeof vi.fn>;
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

  /** An ISO date string the given number of days from today. */
  function daysFromToday(days: number): string {
    const date = new Date();
    date.setHours(0, 0, 0, 0);
    date.setDate(date.getDate() + days);
    return date.toISOString();
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [InventoryTableComponent, NoopAnimationsModule],
      providers: [
        { provide: InventoryService, useValue: inventoryServiceMock },
        { provide: MatDialog, useValue: dialogMock },
        { provide: MatSnackBar, useValue: snackBarMock },
      ],
    });

    fixture = TestBed.createComponent(InventoryTableComponent);
    fixture.componentRef.setInput('location', 'Pantry');
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  function items(): InventoryItemResponse[] {
    return (component as unknown as { items: () => InventoryItemResponse[] }).items();
  }

  function deletingId(): string | null {
    return (component as unknown as { deletingId: () => string | null }).deletingId();
  }

  beforeEach(() => {
    inventoryServiceMock = { deleteItem: vi.fn(), getItems: vi.fn() };
    dialogMock = { open: vi.fn() };
    snackBarMock = { open: vi.fn() };
    inventoryServiceMock.getItems.mockReturnValue(of([makeItem()]));
  });

  // ── Initial load ────────────────────────────────────────────────────────────

  describe('initial load', () => {
    it('fetches items for the bound location on init', () => {
      createComponent();

      expect(inventoryServiceMock.getItems).toHaveBeenCalledWith('Pantry');
    });

    it('populates the items signal from the response', () => {
      createComponent();

      expect(items().length).toBe(1);
    });

    it('renders a row per item', () => {
      inventoryServiceMock.getItems.mockReturnValue(of([makeItem(), makeItem({ id: 'item-2' })]));

      createComponent();

      expect(fixture.nativeElement.querySelectorAll('mat-row').length).toBe(2);
    });

    it('shows an empty-state message naming the location when there are no items', () => {
      inventoryServiceMock.getItems.mockReturnValue(of([]));

      createComponent();

      const message = fixture.nativeElement.querySelector('.state-message');
      expect(message.textContent).toContain('No items in Pantry');
    });
  });

  // ── Error path ──────────────────────────────────────────────────────────────

  describe('load failure', () => {
    it('shows the error message when the fetch fails', () => {
      inventoryServiceMock.getItems.mockReturnValue(throwError(() => new Error('network')));

      createComponent();

      expect(fixture.nativeElement.querySelector('.error-message')).not.toBeNull();
    });

    it('clears the spinner after a failed request', () => {
      inventoryServiceMock.getItems.mockReturnValue(throwError(() => new Error('network')));

      createComponent();

      expect(fixture.nativeElement.querySelector('mat-progress-spinner')).toBeNull();
    });

    it('re-fetches and recovers when Retry is clicked', () => {
      inventoryServiceMock.getItems.mockReturnValue(throwError(() => new Error('network')));
      createComponent();

      inventoryServiceMock.getItems.mockReturnValue(of([makeItem()]));
      const retry = fixture.nativeElement.querySelector(
        '.error-message button',
      ) as HTMLButtonElement;
      retry.click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.error-message')).toBeNull();
      expect(items().length).toBe(1);
    });
  });

  // ── Expiry banding ──────────────────────────────────────────────────────────
  //
  // The bands are the visual cue that drives the whole waste-reduction feature,
  // so the day boundaries are pinned exactly.

  describe('getExpiryClass', () => {
    it('bands an item expiring today as red', () => {
      createComponent();

      expect(component.getExpiryClass(daysFromToday(0))).toContain('expiry-red');
    });

    it('bands an already-expired item as red', () => {
      createComponent();

      expect(component.getExpiryClass(daysFromToday(-5))).toContain('expiry-red');
    });

    it('bands two days out as red — the last day inside the red band', () => {
      createComponent();

      expect(component.getExpiryClass(daysFromToday(2))).toContain('expiry-red');
    });

    it('bands three days out as amber — the first day outside the red band', () => {
      createComponent();

      expect(component.getExpiryClass(daysFromToday(3))).toContain('expiry-amber');
    });

    it('bands seven days out as amber — the last day inside the amber band', () => {
      createComponent();

      expect(component.getExpiryClass(daysFromToday(7))).toContain('expiry-amber');
    });

    it('bands eight days out as green', () => {
      createComponent();

      expect(component.getExpiryClass(daysFromToday(8))).toContain('expiry-ok');
    });

    it('renders an em dash instead of a badge for an item with no expiry date', () => {
      inventoryServiceMock.getItems.mockReturnValue(of([makeItem({ expiryDate: null })]));

      createComponent();

      expect(fixture.nativeElement.querySelector('.no-expiry')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('.expiry-badge')).toBeNull();
    });

    it('renders a badge for an item that has an expiry date', () => {
      inventoryServiceMock.getItems.mockReturnValue(
        of([makeItem({ expiryDate: daysFromToday(1) })]),
      );

      createComponent();

      expect(fixture.nativeElement.querySelector('.expiry-badge')).not.toBeNull();
    });
  });

  // ── Delete ──────────────────────────────────────────────────────────────────

  describe('deleteItem', () => {
    it('removes the item from the table on success', () => {
      inventoryServiceMock.getItems.mockReturnValue(of([makeItem(), makeItem({ id: 'item-2' })]));
      inventoryServiceMock.deleteItem.mockReturnValue(of(undefined));
      createComponent();

      component.deleteItem(makeItem({ id: 'item-2' }));

      expect(items().map((item) => item.id)).toEqual(['item-1']);
    });

    it('emits itemDeleted with the removed id so the parent can refresh its counts', () => {
      inventoryServiceMock.deleteItem.mockReturnValue(of(undefined));
      createComponent();
      const emitted: string[] = [];
      component.itemDeleted.subscribe((id) => emitted.push(id));

      component.deleteItem(makeItem());

      expect(emitted).toEqual(['item-1']);
    });

    it('confirms the deletion with a snack bar', () => {
      inventoryServiceMock.deleteItem.mockReturnValue(of(undefined));
      createComponent();

      component.deleteItem(makeItem());

      expect(snackBarMock.open).toHaveBeenCalledWith('Item deleted.', undefined, {
        duration: 2500,
      });
    });

    it('keeps the item and reports the failure when the delete fails', () => {
      inventoryServiceMock.deleteItem.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      component.deleteItem(makeItem());

      expect(items().length).toBe(1);
      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to delete item.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('clears the in-flight marker after a failure so the row is interactive again', () => {
      inventoryServiceMock.deleteItem.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      component.deleteItem(makeItem());

      expect(deletingId()).toBeNull();
    });

    it('does not emit itemDeleted when the delete fails', () => {
      inventoryServiceMock.deleteItem.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();
      const emitted: string[] = [];
      component.itemDeleted.subscribe((id) => emitted.push(id));

      component.deleteItem(makeItem());

      expect(emitted).toEqual([]);
    });
  });

  // ── Dialogs ─────────────────────────────────────────────────────────────────

  describe('openAddDialog', () => {
    it('opens the dialog in add mode for the bound location', () => {
      dialogMock.open.mockReturnValue({ afterClosed: () => of(null) });
      createComponent();

      component.openAddDialog();

      expect(dialogMock.open).toHaveBeenCalledWith(expect.anything(), {
        data: { location: 'Pantry', mode: 'add' },
        width: '500px',
      });
    });

    it('appends the new item when the dialog returns one', () => {
      dialogMock.open.mockReturnValue({
        afterClosed: () => of(makeItem({ id: 'item-new' })),
      });
      createComponent();

      component.openAddDialog();

      expect(items().map((item) => item.id)).toEqual(['item-1', 'item-new']);
    });

    it('leaves the table untouched when the dialog is dismissed', () => {
      dialogMock.open.mockReturnValue({ afterClosed: () => of(null) });
      createComponent();

      component.openAddDialog();

      expect(items().length).toBe(1);
    });
  });

  describe('openEditDialog', () => {
    it('opens the dialog in edit mode carrying the item', () => {
      dialogMock.open.mockReturnValue({ afterClosed: () => of(null) });
      createComponent();
      const item = makeItem();

      component.openEditDialog(item);

      expect(dialogMock.open).toHaveBeenCalledWith(expect.anything(), {
        data: { item, location: 'Pantry', mode: 'edit' },
        width: '500px',
      });
    });

    it('replaces the edited row in place rather than appending a duplicate', () => {
      dialogMock.open.mockReturnValue({
        afterClosed: () => of(makeItem({ quantity: 99 })),
      });
      createComponent();

      component.openEditDialog(makeItem());

      expect(items().length).toBe(1);
      expect(items()[0].quantity).toBe(99);
    });

    it('leaves the row untouched when the edit is cancelled', () => {
      dialogMock.open.mockReturnValue({ afterClosed: () => of(null) });
      createComponent();

      component.openEditDialog(makeItem());

      expect(items()[0].quantity).toBe(14.5);
    });
  });
});
