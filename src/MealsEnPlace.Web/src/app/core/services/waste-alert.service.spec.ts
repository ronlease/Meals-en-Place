import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { WasteAlertResponse } from '../models/waste-alert.models';
import { WasteAlertService } from './waste-alert.service';

describe('WasteAlertService', () => {
  const BASE_URL = '/api/v1/waste-alerts';

  let httpMock: HttpTestingController;
  let service: WasteAlertService;

  const ALERTS: WasteAlertResponse[] = [
    {
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
    },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(WasteAlertService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('dismissAlert POSTs an empty body to the alert dismiss URL', () => {
    let completed = false;

    service.dismissAlert('alert-1').subscribe(() => (completed = true));

    const testRequest = httpMock.expectOne(`${BASE_URL}/alert-1/dismiss`);
    expect(testRequest.request.method).toBe('POST');
    expect(testRequest.request.body).toEqual({});

    testRequest.flush(null);
    expect(completed).toBe(true);
  });

  it('getAlerts GETs the waste-alerts collection', () => {
    let received: WasteAlertResponse[] | undefined;

    service.getAlerts().subscribe((alerts) => (received = alerts));

    const testRequest = httpMock.expectOne(BASE_URL);
    expect(testRequest.request.method).toBe('GET');

    testRequest.flush(ALERTS);
    expect(received).toEqual(ALERTS);
  });
});
