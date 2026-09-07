import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ConsumeMealResponse,
  GenerateMealPlanRequest,
  MealPlanResponse,
  MealPlanSlotResponse,
  ReorderPreviewResponse,
} from '../models/meal-plan.models';
import { MealPlanPushResult } from '../models/todoist.models';
import { MealPlanService } from './meal-plan.service';

describe('MealPlanService', () => {
  const PLANS_URL = '/api/v1/meal-plans';
  const SLOTS_URL = '/api/v1/meal-plan-slots';

  let httpMock: HttpTestingController;
  let service: MealPlanService;

  function makePlan(): MealPlanResponse {
    return {
      createdAt: '2026-09-01T00:00:00Z',
      id: 'plan-1',
      name: 'Week of Sept 1',
      slots: [],
      weekStartDate: '2026-09-01',
    };
  }

  function makeSlot(): MealPlanSlotResponse {
    return {
      consumedAt: null,
      cuisineType: 'Italian',
      dayOfWeek: 'Monday',
      id: 'slot-1',
      mealSlot: 'Dinner',
      recipeId: 'recipe-1',
      recipeTitle: 'Pasta',
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(MealPlanService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // ── Reorder-by-expiry: the urgency window is optional ────────────────────────

  describe('reorder by expiry', () => {
    it('applyReorderByExpiry omits the query string when no urgency window is given', () => {
      let received: MealPlanResponse | undefined;

      service.applyReorderByExpiry('plan-1').subscribe((plan) => (received = plan));

      const testRequest = httpMock.expectOne(
        `${PLANS_URL}/plan-1/reorder-by-expiry/apply`,
      );
      expect(testRequest.request.method).toBe('POST');
      expect(testRequest.request.body).toEqual({});

      testRequest.flush(makePlan());
      expect(received).toEqual(makePlan());
    });

    it('applyReorderByExpiry appends urgencyWindowDays when supplied', () => {
      service.applyReorderByExpiry('plan-1', 3).subscribe();

      httpMock
        .expectOne(`${PLANS_URL}/plan-1/reorder-by-expiry/apply?urgencyWindowDays=3`)
        .flush(makePlan());
    });

    it('applyReorderByExpiry treats a zero window as absent — 0 is falsy in the guard', () => {
      // Documents current behaviour: the service builds the query with a truthiness
      // check, so an explicit 0 produces no query string.
      service.applyReorderByExpiry('plan-1', 0).subscribe();

      httpMock
        .expectOne(`${PLANS_URL}/plan-1/reorder-by-expiry/apply`)
        .flush(makePlan());
    });

    it('previewReorderByExpiry omits the query string when no urgency window is given', () => {
      let received: ReorderPreviewResponse | undefined;

      service.previewReorderByExpiry('plan-2').subscribe((preview) => (received = preview));

      const testRequest = httpMock.expectOne(
        `${PLANS_URL}/plan-2/reorder-by-expiry/preview`,
      );
      expect(testRequest.request.method).toBe('POST');

      const preview: ReorderPreviewResponse = {
        changes: [],
        hasChanges: false,
        reason: 'Nothing is close to expiry.',
        urgencyWindowDays: 5,
      };
      testRequest.flush(preview);
      expect(received).toEqual(preview);
    });

    it('previewReorderByExpiry appends urgencyWindowDays when supplied', () => {
      service.previewReorderByExpiry('plan-2', 7).subscribe();

      httpMock
        .expectOne(`${PLANS_URL}/plan-2/reorder-by-expiry/preview?urgencyWindowDays=7`)
        .flush({ changes: [], hasChanges: false, reason: null, urgencyWindowDays: 7 });
    });
  });

  // ── Consume / unconsume use the slots URL, not the plans URL ─────────────────

  describe('slot consumption', () => {
    it('consumeSlot POSTs to the meal-plan-slots URL', () => {
      let received: ConsumeMealResponse | undefined;

      service.consumeSlot('slot-7').subscribe((response) => (received = response));

      const testRequest = httpMock.expectOne(`${SLOTS_URL}/slot-7/consume`);
      expect(testRequest.request.method).toBe('POST');

      const response: ConsumeMealResponse = {
        autoDepleteApplied: true,
        consumedAt: '2026-09-01T18:00:00Z',
        shortIngredients: [],
      };
      testRequest.flush(response);
      expect(received).toEqual(response);
    });

    it('unconsumeSlot DELETEs the consume sub-resource', () => {
      let completed = false;

      service.unconsumeSlot('slot-7').subscribe(() => (completed = true));

      const testRequest = httpMock.expectOne(`${SLOTS_URL}/slot-7/consume`);
      expect(testRequest.request.method).toBe('DELETE');

      testRequest.flush(null);
      expect(completed).toBe(true);
    });
  });

  // ── Remaining endpoints ─────────────────────────────────────────────────────

  it('generatePlan POSTs the request to the generate endpoint', () => {
    const request: GenerateMealPlanRequest = { name: 'Next week', seasonalOnly: true };

    service.generatePlan(request).subscribe();

    const testRequest = httpMock.expectOne(`${PLANS_URL}/generate`);
    expect(testRequest.request.method).toBe('POST');
    expect(testRequest.request.body).toEqual(request);

    testRequest.flush(makePlan());
  });

  it('getActivePlan GETs the active plan', () => {
    let received: MealPlanResponse | undefined;

    service.getActivePlan().subscribe((plan) => (received = plan));

    const testRequest = httpMock.expectOne(`${PLANS_URL}/active`);
    expect(testRequest.request.method).toBe('GET');

    testRequest.flush(makePlan());
    expect(received).toEqual(makePlan());
  });

  it('pushToTodoist sends the project ID in the body', () => {
    let received: MealPlanPushResult | undefined;

    service.pushToTodoist('plan-1', '69mF7QcCj9JmXxp8').subscribe((r) => (received = r));

    const testRequest = httpMock.expectOne(`${PLANS_URL}/plan-1/push/todoist`);
    expect(testRequest.request.method).toBe('POST');
    expect(testRequest.request.body).toEqual({ projectId: '69mF7QcCj9JmXxp8' });

    const result: MealPlanPushResult = { closed: 0, created: 7, unchanged: 0, updated: 0 };
    testRequest.flush(result);
    expect(received).toEqual(result);
  });

  it('pushToTodoist sends a null project ID for the Inbox selection', () => {
    service.pushToTodoist('plan-1', null).subscribe();

    const testRequest = httpMock.expectOne(`${PLANS_URL}/plan-1/push/todoist`);
    expect(testRequest.request.body).toEqual({ projectId: null });

    testRequest.flush({ closed: 0, created: 1, unchanged: 0, updated: 0 });
  });

  it('swapSlot PUTs to the plan-scoped slots URL', () => {
    let received: MealPlanSlotResponse | undefined;

    service.swapSlot('slot-3', { recipeId: 'recipe-9' }).subscribe((s) => (received = s));

    const testRequest = httpMock.expectOne(`${PLANS_URL}/slots/slot-3`);
    expect(testRequest.request.method).toBe('PUT');
    expect(testRequest.request.body).toEqual({ recipeId: 'recipe-9' });

    testRequest.flush(makeSlot());
    expect(received).toEqual(makeSlot());
  });
});
