import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import {
  InventoryItemResponse,
  InventoryLocation,
} from '../../core/models/inventory.models';
import { InventoryService } from '../../core/services/inventory.service';
import { ExpirationPageComponent } from './expiration-page.component';

describe('ExpirationPageComponent', () => {
  let component: ExpirationPageComponent;
  let fixture: ComponentFixture<ExpirationPageComponent>;
  let inventoryServiceMock: { getItems: ReturnType<typeof vi.fn> };

  function makeItem(overrides: Partial<InventoryItemResponse> = {}): InventoryItemResponse {
    return {
      canonicalIngredientId: 'ing-1',
      canonicalIngredientName: 'Spinach',
      expiryDate: null,
      id: 'item-1',
      location: 'Fridge',
      notes: null,
      quantity: 200,
      unitOfMeasureAbbreviation: 'g',
      unitOfMeasureId: 'uom-g',
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

  /** Serves the given items per location; unnamed locations return nothing. */
  function serve(byLocation: Partial<Record<InventoryLocation, InventoryItemResponse[]>>): void {
    inventoryServiceMock.getItems.mockImplementation((location: InventoryLocation) =>
      of(byLocation[location] ?? []),
    );
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [ExpirationPageComponent, NoopAnimationsModule],
      providers: [{ provide: InventoryService, useValue: inventoryServiceMock }],
    });

    fixture = TestBed.createComponent(ExpirationPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  type ExpiringItem = InventoryItemResponse & { daysRemaining: number };

  type Internals = {
    activeFilter: {
      (): 'all' | '3days' | '7days';
      set: (value: 'all' | '3days' | '7days') => void;
    };
    allItems: () => ExpiringItem[];
    error: () => boolean;
    filteredItems: () => ExpiringItem[];
    loading: () => boolean;
  };

  function internals(): Internals {
    return component as unknown as Internals;
  }

  beforeEach(() => {
    inventoryServiceMock = { getItems: vi.fn() };
    serve({ Fridge: [makeItem({ expiryDate: daysFromToday(2) })] });
  });

  // ── Load ────────────────────────────────────────────────────────────────────

  describe('load', () => {
    it('queries every storage location', () => {
      createComponent();

      expect(inventoryServiceMock.getItems).toHaveBeenCalledWith('Pantry');
      expect(inventoryServiceMock.getItems).toHaveBeenCalledWith('Fridge');
      expect(inventoryServiceMock.getItems).toHaveBeenCalledWith('Freezer');
      expect(internals().loading()).toBe(false);
    });

    it('merges items from all three locations into one list', () => {
      serve({
        Freezer: [makeItem({ expiryDate: daysFromToday(3), id: 'c' })],
        Fridge: [makeItem({ expiryDate: daysFromToday(2), id: 'b' })],
        Pantry: [makeItem({ expiryDate: daysFromToday(1), id: 'a' })],
      });

      createComponent();

      expect(internals().allItems().length).toBe(3);
    });

    it('excludes items with no expiry date', () => {
      // The page is about expiry; a shelf-stable item without a date has nothing
      // to say here.
      serve({
        Pantry: [
          makeItem({ expiryDate: null, id: 'no-date' }),
          makeItem({ expiryDate: daysFromToday(4), id: 'dated' }),
        ],
      });

      createComponent();

      expect(internals().allItems().map((item) => item.id)).toEqual(['dated']);
    });

    it('excludes items whose expiry date is an empty string', () => {
      serve({ Pantry: [makeItem({ expiryDate: '', id: 'blank' })] });

      createComponent();

      expect(internals().allItems()).toEqual([]);
    });

    it('sorts by days remaining, soonest first, across locations', () => {
      serve({
        Freezer: [makeItem({ expiryDate: daysFromToday(1), id: 'soon' })],
        Fridge: [makeItem({ expiryDate: daysFromToday(20), id: 'later' })],
        Pantry: [makeItem({ expiryDate: daysFromToday(-2), id: 'expired' })],
      });

      createComponent();

      expect(internals().allItems().map((item) => item.id)).toEqual([
        'expired',
        'soon',
        'later',
      ]);
    });

    it('computes days remaining relative to today', () => {
      serve({ Pantry: [makeItem({ expiryDate: daysFromToday(5) })] });

      createComponent();

      expect(internals().allItems()[0].daysRemaining).toBe(5);
    });

    it('computes a negative days-remaining for an already-expired item', () => {
      serve({ Pantry: [makeItem({ expiryDate: daysFromToday(-3) })] });

      createComponent();

      expect(internals().allItems()[0].daysRemaining).toBe(-3);
    });

    it('shows the error state when any location fails', () => {
      inventoryServiceMock.getItems.mockImplementation((location: InventoryLocation) =>
        location === 'Freezer' ? throwError(() => new Error('boom')) : of([]),
      );

      createComponent();

      expect(internals().error()).toBe(true);
      expect(internals().loading()).toBe(false);
      expect(fixture.nativeElement.querySelector('.error-message')).not.toBeNull();
    });

    it('recovers when Retry is clicked', () => {
      inventoryServiceMock.getItems.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      serve({ Pantry: [makeItem({ expiryDate: daysFromToday(2) })] });
      (
        fixture.nativeElement.querySelector('.error-message button') as HTMLButtonElement
      ).click();
      fixture.detectChanges();

      expect(internals().error()).toBe(false);
      expect(internals().allItems().length).toBe(1);
    });
  });

  // ── Filtering ───────────────────────────────────────────────────────────────

  describe('filtering', () => {
    beforeEach(() => {
      serve({
        Pantry: [
          makeItem({ expiryDate: daysFromToday(2), id: 'in-3' }),
          makeItem({ expiryDate: daysFromToday(3), id: 'boundary-3' }),
          makeItem({ expiryDate: daysFromToday(7), id: 'boundary-7' }),
          makeItem({ expiryDate: daysFromToday(30), id: 'far-off' }),
        ],
      });
    });

    it('shows everything by default', () => {
      createComponent();

      expect(internals().filteredItems().length).toBe(4);
    });

    it('includes the boundary day in the 3-day filter', () => {
      createComponent();

      internals().activeFilter.set('3days');

      expect(internals().filteredItems().map((item) => item.id)).toEqual([
        'in-3',
        'boundary-3',
      ]);
    });

    it('includes the boundary day in the 7-day filter', () => {
      createComponent();

      internals().activeFilter.set('7days');

      expect(internals().filteredItems().map((item) => item.id)).toEqual([
        'in-3',
        'boundary-3',
        'boundary-7',
      ]);
    });

    it('keeps already-expired items visible under every filter', () => {
      // An expired item is the most urgent thing on the page; a "next 3 days"
      // filter must not hide it.
      serve({ Pantry: [makeItem({ expiryDate: daysFromToday(-5), id: 'expired' })] });
      createComponent();

      internals().activeFilter.set('3days');

      expect(internals().filteredItems().map((item) => item.id)).toEqual(['expired']);
    });

    it('renders a row per filtered item', () => {
      createComponent();

      internals().activeFilter.set('3days');
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelectorAll('mat-row').length).toBe(2);
    });
  });

  // ── Empty states name the active filter ─────────────────────────────────────

  describe('empty states', () => {
    beforeEach(() => {
      serve({});
    });

    it('says nothing has an expiry date under the all filter', () => {
      createComponent();

      expect(fixture.nativeElement.textContent).toContain(
        'No items with expiry dates found.',
      );
    });

    it('names the 7-day window when that filter is empty', () => {
      createComponent();

      internals().activeFilter.set('7days');
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain(
        'No items expiring within 7 days.',
      );
    });

    it('names the 3-day window when that filter is empty', () => {
      createComponent();

      internals().activeFilter.set('3days');
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain(
        'No items expiring within 3 days.',
      );
    });
  });

  // ── Expiry banding ──────────────────────────────────────────────────────────

  describe('expiry banding', () => {
    it('bands an already-expired item red', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(-1)).toContain('expiry-red');
    });

    it('bands two days out red — the last day inside the red band', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(2)).toContain('expiry-red');
    });

    it('bands three days out amber', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(3)).toContain('expiry-amber');
    });

    it('bands seven days out amber — the last day inside the amber band', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(7)).toContain('expiry-amber');
    });

    it('bands eight days out green', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(8)).toContain('expiry-ok');
    });

    it('bands the days-remaining column on the same scale as the date column', () => {
      createComponent();

      expect(component.getDaysRemainingClass(2)).toBe(component.getExpiryBadgeClass(2));
      expect(component.getDaysRemainingClass(5)).toBe(component.getExpiryBadgeClass(5));
      expect(component.getDaysRemainingClass(9)).toBe(component.getExpiryBadgeClass(9));
    });
  });

  // ── Days-remaining wording ──────────────────────────────────────────────────

  describe('days-remaining wording', () => {
    function daysCellText(): string {
      const cells = Array.from(
        fixture.nativeElement.querySelectorAll('mat-row mat-cell'),
      ) as HTMLElement[];
      return cells[cells.length - 1].textContent?.trim() ?? '';
    }

    it('labels a past-due item as Expired', () => {
      serve({ Pantry: [makeItem({ expiryDate: daysFromToday(-1) })] });

      createComponent();

      expect(daysCellText()).toBe('Expired');
    });

    it('says "Expires today" on the expiry date itself', () => {
      serve({ Pantry: [makeItem({ expiryDate: daysFromToday(0) })] });

      createComponent();

      expect(daysCellText()).toBe('Expires today');
    });

    it('uses the singular for one day remaining', () => {
      serve({ Pantry: [makeItem({ expiryDate: daysFromToday(1) })] });

      createComponent();

      expect(daysCellText()).toBe('1 day');
    });

    it('pluralizes multiple days remaining', () => {
      serve({ Pantry: [makeItem({ expiryDate: daysFromToday(4) })] });

      createComponent();

      expect(daysCellText()).toBe('4 days');
    });
  });
});
