import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  CanonicalIngredientDto,
  CreateIngredientRequest,
  UnitOfMeasureDto,
} from '../models/inventory.models';
import { ReferenceDataService } from './reference-data.service';

describe('ReferenceDataService', () => {
  const BASE_URL = '/api/v1/referencedata';

  let httpMock: HttpTestingController;
  let service: ReferenceDataService;

  const INGREDIENT: CanonicalIngredientDto = {
    category: 'Canned',
    defaultUnitOfMeasureId: 'uom-oz',
    id: 'ing-1',
    name: 'Diced Tomatoes',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(ReferenceDataService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('createIngredient POSTs the request to the ingredients URL', () => {
    const request: CreateIngredientRequest = {
      category: 'Canned',
      defaultUnitOfMeasureId: 'uom-oz',
      name: 'Diced Tomatoes',
    };
    let received: CanonicalIngredientDto | undefined;

    service.createIngredient(request).subscribe((ingredient) => (received = ingredient));

    const testRequest = httpMock.expectOne(`${BASE_URL}/ingredients`);
    expect(testRequest.request.method).toBe('POST');
    expect(testRequest.request.body).toEqual(request);

    testRequest.flush(INGREDIENT);
    expect(received).toEqual(INGREDIENT);
  });

  it('getUnits GETs the units URL', () => {
    let received: UnitOfMeasureDto[] | undefined;

    service.getUnits().subscribe((units) => (received = units));

    const testRequest = httpMock.expectOne(`${BASE_URL}/units`);
    expect(testRequest.request.method).toBe('GET');

    const units: UnitOfMeasureDto[] = [
      { abbreviation: 'oz', id: 'uom-oz', name: 'Ounce', unitOfMeasureType: 'Weight' },
    ];
    testRequest.flush(units);
    expect(received).toEqual(units);
  });

  it('searchIngredients GETs the ingredients URL with search and limit params', () => {
    let received: CanonicalIngredientDto[] | undefined;

    service.searchIngredients('diced', 10).subscribe((ingredients) => (received = ingredients));

    const testRequest = httpMock.expectOne(`${BASE_URL}/ingredients?search=diced&limit=10`);
    expect(testRequest.request.method).toBe('GET');

    testRequest.flush([INGREDIENT]);
    expect(received).toEqual([INGREDIENT]);
  });

  it('searchIngredients uses the default limit of 20 when none is supplied', () => {
    service.searchIngredients('butter').subscribe();

    const testRequest = httpMock.expectOne(`${BASE_URL}/ingredients?search=butter&limit=20`);
    testRequest.flush([]);
  });

  it('does not expose a getIngredients method that would fetch the full ingredient list', () => {
    // Scenario: No remaining caller requests the full ingredient list.
    // The old getIngredients() that triggered a 120,505-row dump must not exist on the service.
    expect((service as unknown as Record<string, unknown>)['getIngredients']).toBeUndefined();
  });
});
