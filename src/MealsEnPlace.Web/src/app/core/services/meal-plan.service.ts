import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConsumeMealResponse,
  GenerateMealPlanRequest,
  MealPlanResponse,
  MealPlanSlotResponse,
  ReorderPreviewResponse,
  SwapSlotRequest
} from '../models/meal-plan.models';
import { MealPlanPushResult } from '../models/todoist.models';

@Injectable({ providedIn: 'root' })
export class MealPlanService {
  private readonly baseUrl = `${environment.apiUrl}/v1/meal-plans`;
  private readonly http = inject(HttpClient);
  private readonly slotsUrl = `${environment.apiUrl}/v1/meal-plan-slots`;

  consumeSlot(slotId: string): Observable<ConsumeMealResponse> {
    return this.http.post<ConsumeMealResponse>(
      `${this.slotsUrl}/${slotId}/consume`,
      {}
    );
  }

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

  applyReorderByExpiry(
    mealPlanId: string,
    urgencyWindowDays?: number
  ): Observable<MealPlanResponse> {
    const query = urgencyWindowDays ? `?urgencyWindowDays=${urgencyWindowDays}` : '';
    return this.http.post<MealPlanResponse>(
      `${this.baseUrl}/${mealPlanId}/reorder-by-expiry/apply${query}`,
      {}
    );
  }

  previewReorderByExpiry(
    mealPlanId: string,
    urgencyWindowDays?: number
  ): Observable<ReorderPreviewResponse> {
    const query = urgencyWindowDays ? `?urgencyWindowDays=${urgencyWindowDays}` : '';
    return this.http.post<ReorderPreviewResponse>(
      `${this.baseUrl}/${mealPlanId}/reorder-by-expiry/preview${query}`,
      {}
    );
  }

  pushToTodoist(mealPlanId: string): Observable<MealPlanPushResult> {
    return this.http.post<MealPlanPushResult>(
      `${this.baseUrl}/${mealPlanId}/push/todoist`,
      {}
    );
  }

  unconsumeSlot(slotId: string): Observable<void> {
    return this.http.delete<void>(`${this.slotsUrl}/${slotId}/consume`);
  }
}
