import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TodoistAvailabilityService } from './todoist-availability.service';

describe('TodoistAvailabilityService', () => {
  const STATUS_URL = '/api/v1/settings/todoist/status';

  let httpMock: HttpTestingController;
  let service: TodoistAvailabilityService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(TodoistAvailabilityService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('assumes not configured until a refresh says otherwise', () => {
    expect(service.configured()).toBe(false);
  });

  it('setConfigured drives the signal directly, without a request', () => {
    service.setConfigured(true);

    expect(service.configured()).toBe(true);
    httpMock.expectNone(STATUS_URL);
  });

  describe('refresh', () => {
    it('GETs the Todoist status URL and adopts a configured result', () => {
      service.refresh();

      const testRequest = httpMock.expectOne(STATUS_URL);
      expect(testRequest.request.method).toBe('GET');

      testRequest.flush({ configured: true });
      expect(service.configured()).toBe(true);
    });

    it('adopts an unconfigured result', () => {
      service.setConfigured(true);

      service.refresh();
      httpMock.expectOne(STATUS_URL).flush({ configured: false });

      expect(service.configured()).toBe(false);
    });

    it('falls back to not-configured when the status request fails', () => {
      // Unlike the Claude equivalent, this service has an explicit error handler:
      // an unreachable API is treated as "not configured" rather than left stale.
      service.setConfigured(true);

      service.refresh();
      httpMock.expectOne(STATUS_URL).flush('boom', { status: 500, statusText: 'Server Error' });

      expect(service.configured()).toBe(false);
    });
  });
});
