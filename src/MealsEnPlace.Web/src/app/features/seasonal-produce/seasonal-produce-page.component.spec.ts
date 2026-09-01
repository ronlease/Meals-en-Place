import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { SeasonalProduceResponse } from '../../core/models/seasonal-produce.models';
import { SeasonalProduceService } from '../../core/services/seasonal-produce.service';
import { SeasonalProducePageComponent } from './seasonal-produce-page.component';

describe('SeasonalProducePageComponent', () => {
  let component: SeasonalProducePageComponent;
  let fixture: ComponentFixture<SeasonalProducePageComponent>;
  let seasonalProduceServiceMock: {
    getAllWindows: ReturnType<typeof vi.fn>;
    getInSeason: ReturnType<typeof vi.fn>;
  };

  function makeWindow(
    overrides: Partial<SeasonalProduceResponse> = {},
  ): SeasonalProduceResponse {
    return {
      ingredientId: 'ing-1',
      name: 'Asparagus',
      peakSeasonEnd: '06-15',
      peakSeasonStart: '04-01',
      usdaZone: '7a',
      ...overrides,
    };
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [SeasonalProducePageComponent, NoopAnimationsModule],
      providers: [
        { provide: SeasonalProduceService, useValue: seasonalProduceServiceMock },
      ],
    });

    fixture = TestBed.createComponent(SeasonalProducePageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  type Internals = {
    allWindows: () => SeasonalProduceResponse[];
    displayData: () => SeasonalProduceResponse[];
    error: () => boolean;
    inSeason: () => SeasonalProduceResponse[];
    loading: () => boolean;
    viewMode: { (): 'all' | 'in-season'; set: (value: 'all' | 'in-season') => void };
  };

  function internals(): Internals {
    return component as unknown as Internals;
  }

  beforeEach(() => {
    seasonalProduceServiceMock = {
      getAllWindows: vi
        .fn()
        .mockReturnValue(
          of([makeWindow(), makeWindow({ ingredientId: 'ing-2', name: 'Butternut Squash' })]),
        ),
      getInSeason: vi.fn().mockReturnValue(of([makeWindow()])),
    };
  });

  // ── Load ────────────────────────────────────────────────────────────────────

  describe('load', () => {
    it('fetches both the in-season list and the full calendar', () => {
      createComponent();

      expect(seasonalProduceServiceMock.getInSeason).toHaveBeenCalled();
      expect(seasonalProduceServiceMock.getAllWindows).toHaveBeenCalled();
      expect(internals().loading()).toBe(false);
    });

    it('stores each list separately', () => {
      createComponent();

      expect(internals().inSeason().length).toBe(1);
      expect(internals().allWindows().length).toBe(2);
    });

    it('shows the error state when the in-season fetch fails', () => {
      seasonalProduceServiceMock.getInSeason.mockReturnValue(
        throwError(() => new Error('boom')),
      );

      createComponent();

      expect(internals().error()).toBe(true);
      expect(internals().loading()).toBe(false);
      expect(fixture.nativeElement.querySelector('.error-message')).not.toBeNull();
    });

    it('does not request the full calendar when the in-season fetch fails', () => {
      seasonalProduceServiceMock.getInSeason.mockReturnValue(
        throwError(() => new Error('boom')),
      );

      createComponent();

      expect(seasonalProduceServiceMock.getAllWindows).not.toHaveBeenCalled();
    });

    it('keeps the usable in-season view when only the full calendar fails', () => {
      // The default view needs only the in-season list, so a failure fetching
      // the calendar must not blank the page.
      seasonalProduceServiceMock.getAllWindows.mockReturnValue(
        throwError(() => new Error('boom')),
      );

      createComponent();

      expect(internals().error()).toBe(false);
      expect(internals().loading()).toBe(false);
      expect(internals().inSeason().length).toBe(1);
      expect(internals().allWindows()).toEqual([]);
    });
  });

  // ── View mode ───────────────────────────────────────────────────────────────

  describe('view mode', () => {
    it('starts on the in-season view', () => {
      createComponent();

      expect(internals().viewMode()).toBe('in-season');
      expect(internals().displayData().length).toBe(1);
    });

    it('switches to the full calendar', () => {
      createComponent();

      internals().viewMode.set('all');

      expect(internals().displayData().length).toBe(2);
    });

    it('switches back to the in-season list', () => {
      createComponent();
      internals().viewMode.set('all');

      internals().viewMode.set('in-season');

      expect(internals().displayData().map((item) => item.name)).toEqual(['Asparagus']);
    });

    it('renders a row per displayed window', () => {
      createComponent();

      internals().viewMode.set('all');
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelectorAll('mat-row').length).toBe(2);
    });

    it('shows the season start and end for each entry', () => {
      createComponent();

      const row = fixture.nativeElement.querySelector('mat-row').textContent;
      expect(row).toContain('Asparagus');
      expect(row).toContain('04-01');
      expect(row).toContain('06-15');
    });
  });

  // ── Empty state ─────────────────────────────────────────────────────────────

  describe('empty state', () => {
    it('says nothing is in season when the list is empty', () => {
      seasonalProduceServiceMock.getInSeason.mockReturnValue(of([]));

      createComponent();

      expect(fixture.nativeElement.textContent).toContain(
        'No produce is currently in season for Zone 7a.',
      );
    });

    it('shows the empty state for the full calendar too when it is empty', () => {
      seasonalProduceServiceMock.getAllWindows.mockReturnValue(of([]));
      createComponent();

      internals().viewMode.set('all');
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('mat-table')).toBeNull();
    });
  });

  // ── Zone scoping ────────────────────────────────────────────────────────────

  it('states the USDA zone the data is scoped to', () => {
    createComponent();

    expect(fixture.nativeElement.querySelector('.zone-badge').textContent).toContain(
      'USDA Zone 7a',
    );
  });
});
