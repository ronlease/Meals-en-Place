import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TodoistProjectHistoryResponse } from '../models/todoist.models';
import { SettingsService } from './settings.service';

describe('SettingsService', () => {
  const CLAUDE_URL = '/api/v1/settings/claude';
  const TODOIST_URL = '/api/v1/settings/todoist';

  let httpMock: HttpTestingController;
  let service: SettingsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(SettingsService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // ── getProjectHistory null-normalization ────────────────────────────────────
  //
  // The API serializes with JsonIgnoreCondition.WhenWritingNull, so null-valued
  // fields arrive absent rather than null. The service normalizes at this
  // boundary; these tests pin that behaviour because every `!== null` guard
  // downstream depends on it.

  describe('getProjectHistory normalization', () => {
    it('coerces absent scalar fields to null rather than leaving them undefined', () => {
      let received: TodoistProjectHistoryResponse | undefined;

      service.getProjectHistory().subscribe((response) => (received = response));

      // A payload with every nullable key omitted, as the API actually sends it.
      httpMock
        .expectOne(`${TODOIST_URL}/projects/history`)
        .flush({ projects: [] });

      expect(received).toEqual({
        lastUsedMealPlanProjectId: null,
        lastUsedShoppingListProjectId: null,
        nameResolutionError: null,
        namesResolved: false,
        projects: [],
      });
    });

    it('defaults an absent projects array to an empty array', () => {
      let received: TodoistProjectHistoryResponse | undefined;

      service.getProjectHistory().subscribe((response) => (received = response));

      httpMock.expectOne(`${TODOIST_URL}/projects/history`).flush({});

      expect(received?.projects).toEqual([]);
    });

    it('normalizes each project entry, defaulting isInbox to false', () => {
      let received: TodoistProjectHistoryResponse | undefined;

      service.getProjectHistory().subscribe((response) => (received = response));

      httpMock
        .expectOne(`${TODOIST_URL}/projects/history`)
        .flush({ projects: [{ projectId: '69mF7QcCj9JmXxp8' }] });

      expect(received?.projects).toEqual([
        { displayName: null, isInbox: false, projectId: '69mF7QcCj9JmXxp8' },
      ]);
    });

    it('preserves values that are actually present', () => {
      let received: TodoistProjectHistoryResponse | undefined;

      service.getProjectHistory().subscribe((response) => (received = response));

      const payload: TodoistProjectHistoryResponse = {
        lastUsedMealPlanProjectId: 'meals-id',
        lastUsedShoppingListProjectId: 'groceries-id',
        nameResolutionError: 'Todoist API unreachable',
        namesResolved: true,
        projects: [{ displayName: 'Inbox (default)', isInbox: true, projectId: null }],
      };
      httpMock.expectOne(`${TODOIST_URL}/projects/history`).flush(payload);

      expect(received).toEqual(payload);
    });
  });

  // ── Claude token endpoints ──────────────────────────────────────────────────

  describe('Claude token', () => {
    it('clearToken DELETEs the token URL', () => {
      service.clearToken().subscribe();

      const testRequest = httpMock.expectOne(`${CLAUDE_URL}/token`);
      expect(testRequest.request.method).toBe('DELETE');

      testRequest.flush({ configured: false });
    });

    it('getStatus GETs the status URL', () => {
      let received: { configured: boolean } | undefined;

      service.getStatus().subscribe((status) => (received = status));

      const testRequest = httpMock.expectOne(`${CLAUDE_URL}/status`);
      expect(testRequest.request.method).toBe('GET');

      testRequest.flush({ configured: true });
      expect(received).toEqual({ configured: true });
    });

    it('saveToken POSTs the token in the body', () => {
      service.saveToken('sk-ant-secret').subscribe();

      const testRequest = httpMock.expectOne(`${CLAUDE_URL}/token`);
      expect(testRequest.request.method).toBe('POST');
      expect(testRequest.request.body).toEqual({ token: 'sk-ant-secret' });

      testRequest.flush({ configured: true });
    });

    it('testToken sends the supplied token', () => {
      service.testToken('sk-ant-candidate').subscribe();

      const testRequest = httpMock.expectOne(`${CLAUDE_URL}/test`);
      expect(testRequest.request.body).toEqual({ token: 'sk-ant-candidate' });

      testRequest.flush({ success: true });
    });

    it('testToken sends an explicit null when no token is supplied — tests the saved one', () => {
      service.testToken().subscribe();

      const testRequest = httpMock.expectOne(`${CLAUDE_URL}/test`);
      expect(testRequest.request.body).toEqual({ token: null });

      testRequest.flush({ errorMessage: null, success: true });
    });
  });

  // ── Todoist token endpoints ─────────────────────────────────────────────────

  describe('Todoist token', () => {
    it('clearTodoistToken DELETEs the Todoist token URL', () => {
      service.clearTodoistToken().subscribe();

      const testRequest = httpMock.expectOne(`${TODOIST_URL}/token`);
      expect(testRequest.request.method).toBe('DELETE');

      testRequest.flush({ configured: false });
    });

    it('getTodoistStatus GETs the Todoist status URL', () => {
      service.getTodoistStatus().subscribe();

      const testRequest = httpMock.expectOne(`${TODOIST_URL}/status`);
      expect(testRequest.request.method).toBe('GET');

      testRequest.flush({ configured: true });
    });

    it('saveTodoistToken POSTs the token in the body', () => {
      service.saveTodoistToken('todoist-secret').subscribe();

      const testRequest = httpMock.expectOne(`${TODOIST_URL}/token`);
      expect(testRequest.request.method).toBe('POST');
      expect(testRequest.request.body).toEqual({ token: 'todoist-secret' });

      testRequest.flush({ configured: true });
    });

    it('testTodoistToken sends an explicit null when no token is supplied', () => {
      service.testTodoistToken().subscribe();

      const testRequest = httpMock.expectOne(`${TODOIST_URL}/test`);
      expect(testRequest.request.body).toEqual({ token: null });

      testRequest.flush({ success: false, errorMessage: 'Invalid token' });
    });

    it('testTodoistToken sends the supplied token', () => {
      service.testTodoistToken('candidate').subscribe();

      httpMock
        .expectOne((candidate) => candidate.url === `${TODOIST_URL}/test`)
        .flush({ success: true });
    });
  });
});
