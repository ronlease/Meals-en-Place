import { TestBed } from '@angular/core/testing';
import { NetworkStatusService } from './network-status.service';

describe('NetworkStatusService', () => {
  /** Overrides navigator.onLine, which is read-only on the real navigator. */
  function stubOnLine(value: boolean): void {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value });
  }

  function createService(): NetworkStatusService {
    TestBed.configureTestingModule({});
    return TestBed.inject(NetworkStatusService);
  }

  afterEach(() => {
    stubOnLine(true);
  });

  // ── Seeding from navigator.onLine ────────────────────────────────────────────

  describe('initial state', () => {
    it('seeds the signal from navigator.onLine when online', () => {
      stubOnLine(true);

      expect(createService().isOnline()).toBe(true);
    });

    it('seeds the signal from navigator.onLine when already offline at construction', () => {
      stubOnLine(false);

      expect(createService().isOnline()).toBe(false);
    });
  });

  // ── Window event handling ───────────────────────────────────────────────────

  describe('window events', () => {
    it('flips the signal to false on the offline event', () => {
      const service = createService();

      window.dispatchEvent(new Event('offline'));

      expect(service.isOnline()).toBe(false);
    });

    it('flips the signal back to true on the online event', () => {
      stubOnLine(false);
      const service = createService();

      window.dispatchEvent(new Event('online'));

      expect(service.isOnline()).toBe(true);
    });
  });

  // ── Teardown ────────────────────────────────────────────────────────────────

  describe('ngOnDestroy', () => {
    it('stops responding to network events after destruction', () => {
      // The handlers are removed by name, so a leaked listener would still
      // mutate the signal here.
      const service = createService();

      service.ngOnDestroy();
      window.dispatchEvent(new Event('offline'));

      expect(service.isOnline()).toBe(true);
    });
  });
});
