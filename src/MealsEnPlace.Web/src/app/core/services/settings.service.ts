import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ClaudeModel,
  ClaudeTokenStatusResponse,
  ClaudeTokenTestResponse,
  SaveClaudeModelRequest,
  SaveClaudeTokenRequest,
  SaveTodoistTokenRequest,
  TestClaudeTokenRequest,
  TestTodoistTokenRequest,
  TodoistTokenTestResponse,
} from '../models/settings.models';
import { TodoistProjectHistoryResponse, TodoistStatusResponse } from '../models/todoist.models';

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

  getProjectHistory(): Observable<TodoistProjectHistoryResponse> {
    // The API serializes with JsonIgnoreCondition.WhenWritingNull, so null-valued
    // fields are absent from the payload rather than present as null. Normalize at
    // this boundary so consumers can rely on the declared `string | null` shape and
    // strict null checks behave; without this, an absent key arrives as undefined
    // and slips past every `!== null` guard downstream.
    return this.http.get<TodoistProjectHistoryResponse>(`${this.todoistUrl}/projects/history`).pipe(
      map((response) => ({
        lastUsedMealPlanProjectId: response.lastUsedMealPlanProjectId ?? null,
        lastUsedShoppingListProjectId: response.lastUsedShoppingListProjectId ?? null,
        nameResolutionError: response.nameResolutionError ?? null,
        namesResolved: response.namesResolved ?? false,
        projects: (response.projects ?? []).map((project) => ({
          displayName: project.displayName ?? null,
          isInbox: project.isInbox ?? false,
          projectId: project.projectId ?? null,
        })),
      })),
    );
  }

  getStatus(): Observable<ClaudeTokenStatusResponse> {
    return this.http.get<ClaudeTokenStatusResponse>(`${this.claudeUrl}/status`);
  }

  getTodoistStatus(): Observable<TodoistStatusResponse> {
    return this.http.get<TodoistStatusResponse>(`${this.todoistUrl}/status`);
  }

  saveModel(model: ClaudeModel): Observable<ClaudeTokenStatusResponse> {
    const body: SaveClaudeModelRequest = { model };
    return this.http.post<ClaudeTokenStatusResponse>(`${this.claudeUrl}/model`, body);
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
