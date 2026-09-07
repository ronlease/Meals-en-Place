import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { UserPreferencesResponse } from '../models/preferences.models';
import { PreferencesService } from './preferences.service';

describe('PreferencesService', () => {
  const BASE_URL = '/api/v1/preferences';

  let httpMock: HttpTestingController;
  let service: PreferencesService;

  function makePrefs(
    overrides: Partial<UserPreferencesResponse> = {},
  ): UserPreferencesResponse {
    return { autoDepleteOnConsume: false, displaySystem: 'Imperial', ...overrides };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(PreferencesService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // ── Defaults before anything is loaded ──────────────────────────────────────

  describe('defaults', () => {
    it('starts on Imperial — the documented default display system', () => {
      expect(service.displaySystem()).toBe('Imperial');
    });

    it('starts with auto-deplete off', () => {
      expect(service.autoDepleteOnConsume()).toBe(false);
    });
  });

  // ── loadPreferences ─────────────────────────────────────────────────────────

  describe('loadPreferences', () => {
    it('GETs the preferences URL and applies both fields to the signals', () => {
      service.loadPreferences();

      const testRequest = httpMock.expectOne(BASE_URL);
      expect(testRequest.request.method).toBe('GET');

      testRequest.flush(makePrefs({ autoDepleteOnConsume: true, displaySystem: 'Metric' }));

      expect(service.displaySystem()).toBe('Metric');
      expect(service.autoDepleteOnConsume()).toBe(true);
    });

    it('leaves the signals at their defaults when the request fails', () => {
      // The subscriber has no error handler, so a failure must not corrupt state.
      service.loadPreferences();

      httpMock
        .expectOne(BASE_URL)
        .flush('boom', { status: 500, statusText: 'Server Error' });

      expect(service.displaySystem()).toBe('Imperial');
      expect(service.autoDepleteOnConsume()).toBe(false);
    });
  });

  // ── setAutoDepleteOnConsume ─────────────────────────────────────────────────

  describe('setAutoDepleteOnConsume', () => {
    it('PUTs the new value alongside the current display system', () => {
      service.setAutoDepleteOnConsume(true);

      const testRequest = httpMock.expectOne(BASE_URL);
      expect(testRequest.request.method).toBe('PUT');
      expect(testRequest.request.body).toEqual({
        autoDepleteOnConsume: true,
        displaySystem: 'Imperial',
      });

      testRequest.flush(makePrefs({ autoDepleteOnConsume: true }));
      expect(service.autoDepleteOnConsume()).toBe(true);
    });

    it('carries the current display system rather than resetting it', () => {
      // Load Metric first, then flip auto-deplete: the PUT must preserve Metric.
      service.loadPreferences();
      httpMock.expectOne(BASE_URL).flush(makePrefs({ displaySystem: 'Metric' }));

      service.setAutoDepleteOnConsume(true);

      const testRequest = httpMock.expectOne(BASE_URL);
      expect(testRequest.request.body).toEqual({
        autoDepleteOnConsume: true,
        displaySystem: 'Metric',
      });

      testRequest.flush(makePrefs({ autoDepleteOnConsume: true, displaySystem: 'Metric' }));
    });

    it('adopts the server response rather than the optimistic local value', () => {
      // If the server disagrees, the server wins.
      service.setAutoDepleteOnConsume(true);

      httpMock.expectOne(BASE_URL).flush(makePrefs({ autoDepleteOnConsume: false }));

      expect(service.autoDepleteOnConsume()).toBe(false);
    });
  });

  // ── toggleDisplaySystem ─────────────────────────────────────────────────────

  describe('toggleDisplaySystem', () => {
    it('requests Metric when the current system is Imperial', () => {
      service.toggleDisplaySystem();

      const testRequest = httpMock.expectOne(BASE_URL);
      expect(testRequest.request.method).toBe('PUT');
      expect(testRequest.request.body).toEqual({ displaySystem: 'Metric' });

      testRequest.flush(makePrefs({ displaySystem: 'Metric' }));
      expect(service.displaySystem()).toBe('Metric');
    });

    it('requests Imperial when the current system is Metric', () => {
      service.loadPreferences();
      httpMock.expectOne(BASE_URL).flush(makePrefs({ displaySystem: 'Metric' }));

      service.toggleDisplaySystem();

      const testRequest = httpMock.expectOne(BASE_URL);
      expect(testRequest.request.body).toEqual({ displaySystem: 'Imperial' });

      testRequest.flush(makePrefs({ displaySystem: 'Imperial' }));
      expect(service.displaySystem()).toBe('Imperial');
    });

    it('does not flip the signal when the request fails', () => {
      service.toggleDisplaySystem();

      httpMock
        .expectOne(BASE_URL)
        .flush('boom', { status: 500, statusText: 'Server Error' });

      expect(service.displaySystem()).toBe('Imperial');
    });
  });
});
