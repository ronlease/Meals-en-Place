import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  RecipeImportResultDto,
  RecipeListItemDto,
  RecipeMatchResponse,
  RecipeSearchResultDto,
} from '../models/recipe.models';

@Injectable({ providedIn: 'root' })
export class RecipeService {
  private readonly baseUrl = `${environment.apiUrl}/v1/recipes`;
  private readonly http = inject(HttpClient);

  getRecipes(): Observable<RecipeListItemDto[]> {
    return this.http.get<RecipeListItemDto[]>(this.baseUrl);
  }

  importRecipe(mealDbId: string): Observable<RecipeImportResultDto> {
    return this.http.post<RecipeImportResultDto>(
      `${this.baseUrl}/import/${mealDbId}`,
      null
    );
  }

  matchRecipes(
    cuisine?: string,
    dietaryTags?: string[],
    seasonalOnly?: boolean
  ): Observable<RecipeMatchResponse> {
    let params = new HttpParams();
    if (cuisine) {
      params = params.set('cuisine', cuisine);
    }
    if (dietaryTags && dietaryTags.length > 0) {
      params = params.set('dietaryTags', dietaryTags.join(','));
    }
    if (seasonalOnly !== undefined) {
      params = params.set('seasonalOnly', String(seasonalOnly));
    }
    return this.http.get<RecipeMatchResponse>(`${this.baseUrl}/match`, {
      params,
    });
  }

  searchByQuery(query: string): Observable<RecipeSearchResultDto[]> {
    return this.http.get<RecipeSearchResultDto[]>(`${this.baseUrl}/search`, {
      params: { query },
    });
  }
}
