import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SeasonalProduceResponse } from '../models/seasonal-produce.models';
import { SeasonalProduceService } from './seasonal-produce.service';

describe('SeasonalProduceService', () => {
  const BASE_URL = '/api/v1/seasonal-produce';

  let httpMock: HttpTestingController;
  let service: SeasonalProduceService;

  const WINDOWS: SeasonalProduceResponse[] = [
    {
      ingredientId: 'ing-1',
      name: 'Asparagus',
      peakSeasonEnd: '06-15',
      peakSeasonStart: '04-01',
      usdaZone: '7a',
    },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(SeasonalProduceService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('getAllWindows GETs the /all sub-resource', () => {
    let received: SeasonalProduceResponse[] | undefined;

    service.getAllWindows().subscribe((windows) => (received = windows));

    const testRequest = httpMock.expectOne(`${BASE_URL}/all`);
    expect(testRequest.request.method).toBe('GET');

    testRequest.flush(WINDOWS);
    expect(received).toEqual(WINDOWS);
  });

  it('getInSeason GETs the collection root, not the /all sub-resource', () => {
    let received: SeasonalProduceResponse[] | undefined;

    service.getInSeason().subscribe((windows) => (received = windows));

    const testRequest = httpMock.expectOne(BASE_URL);
    expect(testRequest.request.method).toBe('GET');

    testRequest.flush(WINDOWS);
    expect(received).toEqual(WINDOWS);
  });
});
