import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CanonicalIngredientDto,
  CreateIngredientRequest,
  UnitOfMeasureDto,
} from '../models/inventory.models';

@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private readonly baseUrl = `${environment.apiUrl}/v1/referencedata`;
  private readonly http = inject(HttpClient);

  createIngredient(request: CreateIngredientRequest): Observable<CanonicalIngredientDto> {
    return this.http.post<CanonicalIngredientDto>(`${this.baseUrl}/ingredients`, request);
  }

  getUnits(): Observable<UnitOfMeasureDto[]> {
    return this.http.get<UnitOfMeasureDto[]>(`${this.baseUrl}/units`);
  }

  searchIngredients(term: string, limit = 20): Observable<CanonicalIngredientDto[]> {
    const params = new HttpParams().set('search', term).set('limit', limit);
    return this.http.get<CanonicalIngredientDto[]>(`${this.baseUrl}/ingredients`, { params });
  }
}
