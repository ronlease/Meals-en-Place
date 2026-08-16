import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ClaudeTokenStatusResponse,
  ClaudeTokenTestResponse,
  SaveClaudeTokenRequest,
  SaveTodoistTokenRequest,
  TestClaudeTokenRequest,
  TestTodoistTokenRequest,
  TodoistTokenTestResponse,
} from '../models/settings.models';
import { TodoistStatusResponse } from '../models/todoist.models';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly claudeUrl = `${environment.apiUrl}/v1/settings/claude`;
  private readonly http = inject(HttpClient);
  private readonly todoistUrl = `${environment.apiUrl}/v1/settings/todoist`;

  clearToken(): Observable<ClaudeTokenStatusResponse> {
    return this.http.delete<ClaudeTokenStatusResponse>(`${this.claudeUrl}/token`);
  }

  clearTodoistToken(): Observable<TodoistStatusResponse> {
    return this.http.delete<TodoistStatusResponse>(`${this.todoistUrl}/token`);
  }

  getStatus(): Observable<ClaudeTokenStatusResponse> {
    return this.http.get<ClaudeTokenStatusResponse>(`${this.claudeUrl}/status`);
  }

  getTodoistStatus(): Observable<TodoistStatusResponse> {
    return this.http.get<TodoistStatusResponse>(`${this.todoistUrl}/status`);
  }

  saveToken(token: string): Observable<ClaudeTokenStatusResponse> {
    const body: SaveClaudeTokenRequest = { token };
    return this.http.post<ClaudeTokenStatusResponse>(`${this.claudeUrl}/token`, body);
  }

  saveTodoistToken(token: string): Observable<TodoistStatusResponse> {
    const body: SaveTodoistTokenRequest = { token };
    return this.http.post<TodoistStatusResponse>(`${this.todoistUrl}/token`, body);
  }

  testToken(token?: string): Observable<ClaudeTokenTestResponse> {
    const body: TestClaudeTokenRequest = { token: token ?? null };
    return this.http.post<ClaudeTokenTestResponse>(`${this.claudeUrl}/test`, body);
  }

  testTodoistToken(token?: string): Observable<TodoistTokenTestResponse> {
    const body: TestTodoistTokenRequest = { token: token ?? null };
    return this.http.post<TodoistTokenTestResponse>(`${this.todoistUrl}/test`, body);
  }
}
