import { HttpClient } from '@angular/common/http';
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

  createIngredient(
    request: CreateIngredientRequest
  ): Observable<CanonicalIngredientDto> {
    return this.http.post<CanonicalIngredientDto>(
      `${this.baseUrl}/ingredients`,
      request
    );
  }

  getIngredients(): Observable<CanonicalIngredientDto[]> {
    return this.http.get<CanonicalIngredientDto[]>(
      `${this.baseUrl}/ingredients`
    );
  }

  getUnits(): Observable<UnitOfMeasureDto[]> {
    return this.http.get<UnitOfMeasureDto[]>(`${this.baseUrl}/units`);
  }
}
