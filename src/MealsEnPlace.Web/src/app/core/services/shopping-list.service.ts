import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ShoppingListItemResponse } from '../models/shopping-list.models';

@Injectable({ providedIn: 'root' })
export class ShoppingListService {
  private readonly baseUrl = `${environment.apiUrl}/v1/meal-plans`;
  private readonly http = inject(HttpClient);

  generateList(mealPlanId: string): Observable<ShoppingListItemResponse[]> {
    return this.http.post<ShoppingListItemResponse[]>(
      `${this.baseUrl}/${mealPlanId}/shopping-list`,
      {}
    );
  }

  getList(mealPlanId: string): Observable<ShoppingListItemResponse[]> {
    return this.http.get<ShoppingListItemResponse[]>(
      `${this.baseUrl}/${mealPlanId}/shopping-list`
    );
  }
}
