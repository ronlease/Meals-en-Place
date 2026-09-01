import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AiAvailabilityService } from './ai-availability.service';
import { SettingsService } from './settings.service';

describe('AiAvailabilityService', () => {
  let service: AiAvailabilityService;
  let settingsServiceMock: { getStatus: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    settingsServiceMock = { getStatus: vi.fn() };

    TestBed.configureTestingModule({
      providers: [{ provide: SettingsService, useValue: settingsServiceMock }],
    });

    service = TestBed.inject(AiAvailabilityService);
  });

  // ── Initial state ───────────────────────────────────────────────────────────

  describe('initial state', () => {
    it('assumes not configured until told otherwise', () => {
      expect(service.configured()).toBe(false);
    });

    it('starts with the banner not dismissed', () => {
      expect(service.dismissed()).toBe(false);
    });
  });

  // ── dismissBanner ───────────────────────────────────────────────────────────

  it('dismissBanner sets the dismissed signal', () => {
    service.dismissBanner();

    expect(service.dismissed()).toBe(true);
  });

  // ── setConfigured ───────────────────────────────────────────────────────────

  describe('setConfigured', () => {
    it('sets the configured signal', () => {
      service.setConfigured(true);

      expect(service.configured()).toBe(true);
    });

    it('un-dismisses the banner when the key becomes configured', () => {
      // Becoming configured is a state change worth re-surfacing, so a prior
      // dismissal must not stick.
      service.dismissBanner();

      service.setConfigured(true);

      expect(service.dismissed()).toBe(false);
    });

    it('leaves a dismissal in place when the key becomes unconfigured', () => {
      service.dismissBanner();

      service.setConfigured(false);

      expect(service.dismissed()).toBe(true);
    });
  });

  // ── refresh ─────────────────────────────────────────────────────────────────

  describe('refresh', () => {
    it('adopts a configured status from the settings service', () => {
      settingsServiceMock.getStatus.mockReturnValue(of({ configured: true }));

      service.refresh();

      expect(service.configured()).toBe(true);
    });

    it('adopts an unconfigured status from the settings service', () => {
      service.setConfigured(true);
      settingsServiceMock.getStatus.mockReturnValue(of({ configured: false }));

      service.refresh();

      expect(service.configured()).toBe(false);
    });

    it('un-dismisses the banner when refresh reports the key is configured', () => {
      service.dismissBanner();
      settingsServiceMock.getStatus.mockReturnValue(of({ configured: true }));

      service.refresh();

      expect(service.dismissed()).toBe(false);
    });

    it('leaves the signals untouched when the status request fails', () => {
      // There is no error handler on the subscription; a failure must not throw
      // or flip the configured signal.
      service.setConfigured(true);
      settingsServiceMock.getStatus.mockReturnValue(throwError(() => new Error('network')));

      expect(() => service.refresh()).not.toThrow();
      expect(service.configured()).toBe(true);
    });
  });
});
