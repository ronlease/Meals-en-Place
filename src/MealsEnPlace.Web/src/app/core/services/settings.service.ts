import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ClaudeTokenStatusResponse,
  ClaudeTokenTestResponse,
  SaveClaudeTokenRequest,
  TestClaudeTokenRequest,
} from '../models/settings.models';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly baseUrl = `${environment.apiUrl}/v1/settings/claude`;
  private readonly http = inject(HttpClient);

  clearToken(): Observable<ClaudeTokenStatusResponse> {
    return this.http.delete<ClaudeTokenStatusResponse>(`${this.baseUrl}/token`);
  }

  getStatus(): Observable<ClaudeTokenStatusResponse> {
    return this.http.get<ClaudeTokenStatusResponse>(`${this.baseUrl}/status`);
  }

  saveToken(token: string): Observable<ClaudeTokenStatusResponse> {
    const body: SaveClaudeTokenRequest = { token };
    return this.http.post<ClaudeTokenStatusResponse>(`${this.baseUrl}/token`, body);
  }

  testToken(token?: string): Observable<ClaudeTokenTestResponse> {
    const body: TestClaudeTokenRequest = { token: token ?? null };
    return this.http.post<ClaudeTokenTestResponse>(`${this.baseUrl}/test`, body);
  }
}
