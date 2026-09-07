import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  BulkResolveGroupRequest,
  CreateRecipeRequest,
  PagedResult,
  RecipeDetailDto,
  RecipeListItemDto,
  RecipeMatchResponse,
  UnresolvedGroupResponse,
} from '../models/recipe.models';
import { RecipeService } from './recipe.service';

describe('RecipeService', () => {
  const BASE_URL = '/api/v1/recipes';

  let httpMock: HttpTestingController;
  let service: RecipeService;

  const EMPTY_MATCH: RecipeMatchResponse = {
    claudeFeasibilityApplied: false,
    fullMatches: [],
    nearMatches: [],
    partialMatches: [],
  };

  function makeDetail(): RecipeDetailDto {
    return {
      cuisineType: 'Italian',
      dietaryTags: [],
      id: 'recipe-1',
      ingredients: [],
      instructions: 'Boil water.',
      isFullyResolved: true,
      servingCount: 4,
      sourceUrl: null,
      title: 'Pasta',
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(RecipeService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // ── Pagination parameters ───────────────────────────────────────────────────

  describe('getRecipes', () => {
    it('defaults to page 1 with a page size of 25', () => {
      let received: PagedResult<RecipeListItemDto> | undefined;

      service.getRecipes().subscribe((page) => (received = page));

      const testRequest = httpMock.expectOne(
        (candidate) =>
          candidate.url === BASE_URL &&
          candidate.params.get('page') === '1' &&
          candidate.params.get('pageSize') === '25',
      );
      expect(testRequest.request.method).toBe('GET');

      const page: PagedResult<RecipeListItemDto> = {
        items: [],
        page: 1,
        pageSize: 25,
        totalCount: 0,
        totalPages: 0,
      };
      testRequest.flush(page);
      expect(received).toEqual(page);
    });

    it('sends the explicit page and pageSize when supplied', () => {
      service.getRecipes(4, 50).subscribe();

      httpMock
        .expectOne(
          (candidate) =>
            candidate.params.get('page') === '4' && candidate.params.get('pageSize') === '50',
        )
        .flush({ items: [], page: 4, pageSize: 50, totalCount: 0, totalPages: 0 });
    });
  });

  // ── Match filters are all optional and independently applied ────────────────

  describe('matchRecipes', () => {
    it('sends no filter parameters when every argument is omitted', () => {
      // seasonalOnly is undefined, so even that parameter must be absent.
      service.matchRecipes().subscribe();

      const testRequest = httpMock.expectOne(`${BASE_URL}/match`);
      expect(testRequest.request.params.keys()).toEqual([]);

      testRequest.flush(EMPTY_MATCH);
    });

    it('sends the cuisine filter when supplied', () => {
      service.matchRecipes('Thai').subscribe();

      const testRequest = httpMock.expectOne((candidate) => candidate.url === `${BASE_URL}/match`);
      expect(testRequest.request.params.get('cuisine')).toBe('Thai');

      testRequest.flush(EMPTY_MATCH);
    });

    it('joins multiple dietary tags with a comma', () => {
      service.matchRecipes(undefined, ['Vegan', 'GlutenFree']).subscribe();

      const testRequest = httpMock.expectOne((candidate) => candidate.url === `${BASE_URL}/match`);
      expect(testRequest.request.params.get('dietaryTags')).toBe('Vegan,GlutenFree');

      testRequest.flush(EMPTY_MATCH);
    });

    it('omits dietaryTags when the array is empty rather than sending a blank value', () => {
      service.matchRecipes(undefined, []).subscribe();

      const testRequest = httpMock.expectOne((candidate) => candidate.url === `${BASE_URL}/match`);
      expect(testRequest.request.params.has('dietaryTags')).toBe(false);

      testRequest.flush(EMPTY_MATCH);
    });

    it('sends seasonalOnly=false explicitly — false is a real filter, not an absent one', () => {
      service.matchRecipes(undefined, undefined, false).subscribe();

      const testRequest = httpMock.expectOne((candidate) => candidate.url === `${BASE_URL}/match`);
      expect(testRequest.request.params.get('seasonalOnly')).toBe('false');

      testRequest.flush(EMPTY_MATCH);
    });

    it('sends every filter together when all three are supplied', () => {
      service.matchRecipes('Italian', ['Vegetarian'], true).subscribe();

      const testRequest = httpMock.expectOne((candidate) => candidate.url === `${BASE_URL}/match`);
      expect(testRequest.request.params.get('cuisine')).toBe('Italian');
      expect(testRequest.request.params.get('dietaryTags')).toBe('Vegetarian');
      expect(testRequest.request.params.get('seasonalOnly')).toBe('true');

      testRequest.flush(EMPTY_MATCH);
    });
  });

  // ── Remaining endpoints ─────────────────────────────────────────────────────

  it('addToShoppingList POSTs a null body to the shopping-list URL, not the recipes URL', () => {
    service.addToShoppingList('recipe-5').subscribe();

    const testRequest = httpMock.expectOne('/api/v1/shopping-list/add-from-recipe/recipe-5');
    expect(testRequest.request.method).toBe('POST');
    expect(testRequest.request.body).toBeNull();

    testRequest.flush([]);
  });

  it('bulkResolveGroup POSTs the request to the unresolved-groups resolve URL', () => {
    const request: BulkResolveGroupRequest = {
      canonicalIngredientId: 'ing-1',
      notes: '1 can chopped tomatoes',
      quantity: 14.5,
      unitOfMeasureId: 'uom-oz',
    };

    service.bulkResolveGroup(request).subscribe();

    const testRequest = httpMock.expectOne(`${BASE_URL}/unresolved-groups/resolve`);
    expect(testRequest.request.method).toBe('POST');
    expect(testRequest.request.body).toEqual(request);

    testRequest.flush({ affectedCount: 12 });
  });

  it('createRecipe POSTs to the recipes collection URL', () => {
    const request: CreateRecipeRequest = {
      cuisineType: 'Italian',
      ingredients: [],
      instructions: 'Boil water.',
      servingCount: 4,
      title: 'Pasta',
    };

    service.createRecipe(request).subscribe();

    const testRequest = httpMock.expectOne(BASE_URL);
    expect(testRequest.request.method).toBe('POST');
    expect(testRequest.request.body).toEqual(request);

    testRequest.flush(makeDetail());
  });

  it('getRecipeDetail GETs the recipe URL', () => {
    let received: RecipeDetailDto | undefined;

    service.getRecipeDetail('recipe-1').subscribe((detail) => (received = detail));

    const testRequest = httpMock.expectOne(`${BASE_URL}/recipe-1`);
    expect(testRequest.request.method).toBe('GET');

    testRequest.flush(makeDetail());
    expect(received).toEqual(makeDetail());
  });

  it('getUnresolvedGroups GETs the unresolved-groups URL', () => {
    let received: UnresolvedGroupResponse[] | undefined;

    service.getUnresolvedGroups().subscribe((groups) => (received = groups));

    const testRequest = httpMock.expectOne(`${BASE_URL}/unresolved-groups`);
    expect(testRequest.request.method).toBe('GET');

    const groups: UnresolvedGroupResponse[] = [
      {
        canonicalIngredientId: 'ing-1',
        canonicalIngredientName: 'Diced Tomatoes',
        notes: '1 can',
        occurrenceCount: 42,
      },
    ];
    testRequest.flush(groups);
    expect(received).toEqual(groups);
  });
});
