import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  AddInventoryItemRequest,
  ContainerReferenceDetectedResponse,
  InventoryItemResponse,
  UpdateInventoryItemRequest,
} from '../models/inventory.models';
import { InventoryService } from './inventory.service';

describe('InventoryService', () => {
  const BASE_URL = '/api/v1/inventory';

  let httpMock: HttpTestingController;
  let service: InventoryService;

  function makeItem(overrides: Partial<InventoryItemResponse> = {}): InventoryItemResponse {
    return {
      canonicalIngredientId: 'ing-1',
      canonicalIngredientName: 'Diced Tomatoes',
      expiryDate: null,
      id: 'item-1',
      location: 'Pantry',
      notes: null,
      quantity: 14.5,
      unitOfMeasureAbbreviation: 'oz',
      unitOfMeasureId: 'uom-oz',
      ...overrides,
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(InventoryService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // ── addItem ─────────────────────────────────────────────────────────────────

  describe('addItem', () => {
    it('POSTs the request to the inventory collection URL', () => {
      const request = { quantity: 2 } as AddInventoryItemRequest;
      let received: unknown;

      service.addItem(request).subscribe((response) => (received = response));

      const testRequest = httpMock.expectOne(BASE_URL);
      expect(testRequest.request.method).toBe('POST');
      expect(testRequest.request.body).toEqual(request);

      const item = makeItem();
      testRequest.flush(item);
      expect(received).toEqual(item);
    });

    it('passes a container-reference-detected response through unchanged', () => {
      // The endpoint returns one of two shapes; the service must not coerce either.
      const detected: ContainerReferenceDetectedResponse = {
        detectedKeyword: 'can',
        message: 'Declare the net weight or volume of this container.',
        originalInput: '1 can diced tomatoes',
      };
      let received: unknown;

      service
        .addItem({ quantity: 1 } as AddInventoryItemRequest)
        .subscribe((response) => (received = response));

      httpMock.expectOne(BASE_URL).flush(detected);
      expect(received).toEqual(detected);
    });
  });

  // ── deleteItem ──────────────────────────────────────────────────────────────

  it('deleteItem issues a DELETE against the item URL', () => {
    let completed = false;

    service.deleteItem('item-9').subscribe(() => (completed = true));

    const testRequest = httpMock.expectOne(`${BASE_URL}/item-9`);
    expect(testRequest.request.method).toBe('DELETE');

    testRequest.flush(null);
    expect(completed).toBe(true);
  });

  // ── getItems ────────────────────────────────────────────────────────────────

  it('getItems sends the location as a query parameter', () => {
    let received: InventoryItemResponse[] | undefined;

    service.getItems('Freezer').subscribe((items) => (received = items));

    const testRequest = httpMock.expectOne(
      (candidate) => candidate.url === BASE_URL && candidate.params.get('location') === 'Freezer',
    );
    expect(testRequest.request.method).toBe('GET');

    const items = [makeItem({ location: 'Freezer' })];
    testRequest.flush(items);
    expect(received).toEqual(items);
  });

  // ── updateItem ──────────────────────────────────────────────────────────────

  it('updateItem PUTs the request to the item URL', () => {
    const request = { quantity: 3 } as UpdateInventoryItemRequest;
    let received: InventoryItemResponse | undefined;

    service.updateItem('item-4', request).subscribe((item) => (received = item));

    const testRequest = httpMock.expectOne(`${BASE_URL}/item-4`);
    expect(testRequest.request.method).toBe('PUT');
    expect(testRequest.request.body).toEqual(request);

    const item = makeItem({ id: 'item-4', quantity: 3 });
    testRequest.flush(item);
    expect(received).toEqual(item);
  });
});
