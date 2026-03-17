import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  GenerateMealPlanRequest,
  MealPlanResponse,
  MealPlanSlotResponse,
  SwapSlotRequest
} from '../models/meal-plan.models';

@Injectable({ providedIn: 'root' })
export class MealPlanService {
  private readonly baseUrl = `${environment.apiUrl}/v1/meal-plans`;
  private readonly http = inject(HttpClient);

  generatePlan(
    request: GenerateMealPlanRequest
  ): Observable<MealPlanResponse> {
    return this.http.post<MealPlanResponse>(
      `${this.baseUrl}/generate`,
      request
    );
  }

  getActivePlan(): Observable<MealPlanResponse> {
    return this.http.get<MealPlanResponse>(`${this.baseUrl}/active`);
  }

  swapSlot(
    slotId: string,
    request: SwapSlotRequest
  ): Observable<MealPlanSlotResponse> {
    return this.http.put<MealPlanSlotResponse>(
      `${this.baseUrl}/slots/${slotId}`,
      request
    );
  }
}
