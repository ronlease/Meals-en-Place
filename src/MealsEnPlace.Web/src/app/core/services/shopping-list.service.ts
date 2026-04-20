import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ShoppingListItemResponse } from '../models/shopping-list.models';
import { ShoppingListPushResult } from '../models/todoist.models';

@Injectable({ providedIn: 'root' })
export class ShoppingListService {
  private readonly baseUrl = `${environment.apiUrl}/v1/meal-plans`;
  private readonly http = inject(HttpClient);
  private readonly standaloneUrl = `${environment.apiUrl}/v1/shopping-list`;

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

  pushMealPlanListToTodoist(mealPlanId: string): Observable<ShoppingListPushResult> {
    return this.http.post<ShoppingListPushResult>(
      `${this.baseUrl}/${mealPlanId}/shopping-list/push/todoist`,
      {}
    );
  }

  pushStandaloneListToTodoist(): Observable<ShoppingListPushResult> {
    return this.http.post<ShoppingListPushResult>(
      `${this.standaloneUrl}/push/todoist`,
      {}
    );
  }
}
