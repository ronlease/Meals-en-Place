import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { WasteAlertResponse } from '../../core/models/waste-alert.models';
import { WasteAlertService } from '../../core/services/waste-alert.service';
import { WasteAlertsPageComponent } from './waste-alerts-page.component';

describe('WasteAlertsPageComponent', () => {
  let component: WasteAlertsPageComponent;
  let fixture: ComponentFixture<WasteAlertsPageComponent>;
  let wasteAlertServiceMock: {
    dismissAlert: ReturnType<typeof vi.fn>;
    getAlerts: ReturnType<typeof vi.fn>;
  };

  function makeAlert(overrides: Partial<WasteAlertResponse> = {}): WasteAlertResponse {
    return {
      alertId: 'alert-1',
      canonicalIngredientName: 'Spinach',
      createdAt: '2026-09-01T00:00:00Z',
      daysUntilExpiry: 2,
      expiryDate: '2026-09-03',
      inventoryItemId: 'item-1',
      location: 'Fridge',
      matchedRecipes: [],
      quantity: 200,
      unitOfMeasureAbbreviation: 'g',
      ...overrides,
    };
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [WasteAlertsPageComponent, NoopAnimationsModule],
      providers: [{ provide: WasteAlertService, useValue: wasteAlertServiceMock }],
    });

    fixture = TestBed.createComponent(WasteAlertsPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  interface Internals {
    alerts: () => WasteAlertResponse[];
    error: () => boolean;
    loading: () => boolean;
  }

  function internals(): Internals {
    return component as unknown as Internals;
  }

  function badgeText(): string {
    return fixture.nativeElement.querySelector('.expiry-badge').textContent.trim();
  }

  beforeEach(() => {
    wasteAlertServiceMock = { dismissAlert: vi.fn(), getAlerts: vi.fn() };
    wasteAlertServiceMock.getAlerts.mockReturnValue(of([makeAlert()]));
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ── Load ────────────────────────────────────────────────────────────────────

  describe('load', () => {
    it('fetches alerts on init', () => {
      createComponent();

      expect(wasteAlertServiceMock.getAlerts).toHaveBeenCalled();
      expect(internals().alerts().length).toBe(1);
      expect(internals().loading()).toBe(false);
    });

    it('renders a card per alert', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(
        of([makeAlert(), makeAlert({ alertId: 'alert-2' })]),
      );

      createComponent();

      expect(fixture.nativeElement.querySelectorAll('.alert-card').length).toBe(2);
    });

    it('reports an all-clear when there are no alerts', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(of([]));

      createComponent();

      expect(fixture.nativeElement.textContent).toContain(
        'No waste alerts right now. All items are fresh!',
      );
    });

    it('shows the error state when the fetch fails', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(throwError(() => new Error('boom')));

      createComponent();

      expect(internals().error()).toBe(true);
      expect(internals().loading()).toBe(false);
      expect(fixture.nativeElement.querySelector('.error-message')).not.toBeNull();
    });

    it('recovers when Retry is clicked', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      wasteAlertServiceMock.getAlerts.mockReturnValue(of([makeAlert()]));
      (fixture.nativeElement.querySelector('.error-message button') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(internals().error()).toBe(false);
      expect(internals().alerts().length).toBe(1);
    });
  });

  // ── Urgency banding ─────────────────────────────────────────────────────────
  //
  // Two independent scales: the avatar urgency class and the expiry badge.
  // Their boundaries differ, so both are pinned.

  describe('getUrgencyClass', () => {
    it('marks a past-due item as expired', () => {
      createComponent();

      expect(component.getUrgencyClass(-1)).toBe('urgency-expired');
    });

    it('marks an item expiring today as critical, not expired', () => {
      createComponent();

      expect(component.getUrgencyClass(0)).toBe('urgency-critical');
    });

    it('marks an item expiring tomorrow as critical', () => {
      createComponent();

      expect(component.getUrgencyClass(1)).toBe('urgency-critical');
    });

    it('marks two days out as a warning', () => {
      createComponent();

      expect(component.getUrgencyClass(2)).toBe('urgency-warning');
    });

    it('marks three days out as merely soon', () => {
      createComponent();

      expect(component.getUrgencyClass(3)).toBe('urgency-soon');
    });
  });

  describe('getExpiryBadgeClass', () => {
    it('bands a past-due item red', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(-1)).toContain('expiry-red');
    });

    it('bands an item expiring today red — unlike the urgency scale', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(0)).toContain('expiry-red');
    });

    it('bands an item expiring tomorrow amber', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(1)).toContain('expiry-amber');
    });

    it('bands two days out blue', () => {
      createComponent();

      expect(component.getExpiryBadgeClass(2)).toContain('expiry-blue');
    });
  });

  // ── Badge wording ───────────────────────────────────────────────────────────

  describe('badge wording', () => {
    it('says "Expires today" for a same-day expiry', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(of([makeAlert({ daysUntilExpiry: 0 })]));

      createComponent();

      expect(badgeText()).toBe('Expires today');
    });

    it('pluralizes a multi-day countdown', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(of([makeAlert({ daysUntilExpiry: 3 })]));

      createComponent();

      expect(badgeText()).toBe('Expires in 3 days');
    });

    it('uses the singular for a one-day countdown', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(of([makeAlert({ daysUntilExpiry: 1 })]));

      createComponent();

      expect(badgeText()).toBe('Expires in 1 day');
    });

    it('reports how long ago a past-due item expired, as a positive number', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(of([makeAlert({ daysUntilExpiry: -4 })]));

      createComponent();

      expect(badgeText()).toBe('Expired 4 days ago');
    });

    it('uses the singular for an item that expired yesterday', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(of([makeAlert({ daysUntilExpiry: -1 })]));

      createComponent();

      expect(badgeText()).toBe('Expired 1 day ago');
    });
  });

  // ── Suggested recipes ───────────────────────────────────────────────────────

  describe('recipe suggestions', () => {
    it('lists a chip per matched recipe', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(
        of([
          makeAlert({
            matchedRecipes: [
              { cuisineType: 'Italian', recipeId: 'r1', title: 'Spinach Lasagna' },
              { cuisineType: 'Indian', recipeId: 'r2', title: 'Saag Paneer' },
            ],
          }),
        ]),
      );

      createComponent();

      const titles = Array.from(fixture.nativeElement.querySelectorAll('mat-chip')).map((chip) =>
        (chip as HTMLElement).textContent?.trim(),
      );
      expect(titles).toEqual(['restaurant Spinach Lasagna', 'restaurant Saag Paneer']);
    });

    it('renders no chips when nothing in the library matches', () => {
      createComponent();

      expect(fixture.nativeElement.querySelectorAll('mat-chip').length).toBe(0);
    });

    it('shows the quantity and location on the card', () => {
      createComponent();

      expect(fixture.nativeElement.querySelector('mat-card-subtitle').textContent).toContain(
        '200 g · Fridge',
      );
    });
  });

  // ── Dismiss ─────────────────────────────────────────────────────────────────

  describe('dismiss', () => {
    it('removes the alert from the page on success', () => {
      wasteAlertServiceMock.getAlerts.mockReturnValue(
        of([makeAlert(), makeAlert({ alertId: 'alert-2' })]),
      );
      wasteAlertServiceMock.dismissAlert.mockReturnValue(of(undefined));
      createComponent();

      component.dismiss('alert-1');

      expect(wasteAlertServiceMock.dismissAlert).toHaveBeenCalledWith('alert-1');
      expect(
        internals()
          .alerts()
          .map((a) => a.alertId),
      ).toEqual(['alert-2']);
    });

    it('keeps the alert when the dismissal fails', () => {
      wasteAlertServiceMock.dismissAlert.mockReturnValue(throwError(() => new Error('boom')));
      createComponent();

      component.dismiss('alert-1');

      expect(internals().alerts().length).toBe(1);
      expect(console.warn).toHaveBeenCalled();
    });

    it('is wired to the card Dismiss button', () => {
      wasteAlertServiceMock.dismissAlert.mockReturnValue(of(undefined));
      createComponent();
      const dismiss = vi.spyOn(component, 'dismiss');

      (fixture.nativeElement.querySelector('mat-card-actions button') as HTMLButtonElement).click();

      expect(dismiss).toHaveBeenCalledWith('alert-1');
    });
  });
});
