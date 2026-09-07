import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ShoppingListItemResponse } from '../models/shopping-list.models';
import { ShoppingListPushResult } from '../models/todoist.models';
import { ShoppingListService } from './shopping-list.service';

describe('ShoppingListService', () => {
  const PLANS_URL = '/api/v1/meal-plans';
  const STANDALONE_URL = '/api/v1/shopping-list';

  let httpMock: HttpTestingController;
  let service: ShoppingListService;

  const ITEMS: ShoppingListItemResponse[] = [
    {
      canonicalIngredientName: 'Diced Tomatoes',
      category: 'Canned',
      id: 'sl-1',
      notes: null,
      quantity: 14.5,
      unitOfMeasureAbbreviation: 'oz',
    },
  ];

  const PUSH_RESULT: ShoppingListPushResult = {
    closed: 1,
    created: 3,
    unchanged: 2,
    updated: 4,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(ShoppingListService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('generateList POSTs to the plan-scoped shopping-list URL', () => {
    let received: ShoppingListItemResponse[] | undefined;

    service.generateList('plan-1').subscribe((items) => (received = items));

    const testRequest = httpMock.expectOne(`${PLANS_URL}/plan-1/shopping-list`);
    expect(testRequest.request.method).toBe('POST');
    expect(testRequest.request.body).toEqual({});

    testRequest.flush(ITEMS);
    expect(received).toEqual(ITEMS);
  });

  it('getList GETs the same plan-scoped URL that generateList POSTs to', () => {
    let received: ShoppingListItemResponse[] | undefined;

    service.getList('plan-1').subscribe((items) => (received = items));

    const testRequest = httpMock.expectOne(`${PLANS_URL}/plan-1/shopping-list`);
    expect(testRequest.request.method).toBe('GET');

    testRequest.flush(ITEMS);
    expect(received).toEqual(ITEMS);
  });

  // ── The two push surfaces target different URLs ──────────────────────────────

  describe('Todoist push', () => {
    it('pushMealPlanListToTodoist sends the project ID to the plan-scoped push URL', () => {
      let received: ShoppingListPushResult | undefined;

      service
        .pushMealPlanListToTodoist('plan-1', '69mF7QcCj9JmXxp8')
        .subscribe((result) => (received = result));

      const testRequest = httpMock.expectOne(
        `${PLANS_URL}/plan-1/shopping-list/push/todoist`,
      );
      expect(testRequest.request.method).toBe('POST');
      expect(testRequest.request.body).toEqual({ projectId: '69mF7QcCj9JmXxp8' });

      testRequest.flush(PUSH_RESULT);
      expect(received).toEqual(PUSH_RESULT);
    });

    it('pushMealPlanListToTodoist sends a null project ID for the Inbox selection', () => {
      service.pushMealPlanListToTodoist('plan-1', null).subscribe();

      const testRequest = httpMock.expectOne(
        `${PLANS_URL}/plan-1/shopping-list/push/todoist`,
      );
      expect(testRequest.request.body).toEqual({ projectId: null });

      testRequest.flush(PUSH_RESULT);
    });

    it('pushStandaloneListToTodoist targets the standalone URL with an empty body', () => {
      // The standalone list carries no plan ID and no project selection.
      let received: ShoppingListPushResult | undefined;

      service.pushStandaloneListToTodoist().subscribe((result) => (received = result));

      const testRequest = httpMock.expectOne(`${STANDALONE_URL}/push/todoist`);
      expect(testRequest.request.method).toBe('POST');
      expect(testRequest.request.body).toEqual({});

      testRequest.flush(PUSH_RESULT);
      expect(received).toEqual(PUSH_RESULT);
    });
  });
});
