import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { WasteAlertResponse } from '../models/waste-alert.models';

@Injectable({ providedIn: 'root' })
export class WasteAlertService {
  private readonly baseUrl = `${environment.apiUrl}/v1/waste-alerts`;
  private readonly http = inject(HttpClient);

  dismissAlert(alertId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${alertId}/dismiss`, {});
  }

  getAlerts(): Observable<WasteAlertResponse[]> {
    return this.http.get<WasteAlertResponse[]>(this.baseUrl);
  }
}
