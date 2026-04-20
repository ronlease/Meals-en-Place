import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { TodoistStatusResponse } from '../models/todoist.models';

/**
 * Lightweight "is Todoist configured?" signal consumed by push buttons and
 * any future Todoist-backed UI. Mirrors the MEP-032 `AiAvailabilityService`
 * pattern so the same affordance (configured pill + disabled action) reads
 * consistently across the app.
 */
@Injectable({ providedIn: 'root' })
export class TodoistAvailabilityService {
  private readonly _configured = signal<boolean>(false);
  private readonly http = inject(HttpClient);
  private readonly statusUrl = `${environment.apiUrl}/v1/settings/todoist/status`;

  readonly configured = this._configured.asReadonly();

  refresh(): void {
    this.http.get<TodoistStatusResponse>(this.statusUrl).subscribe({
      error: () => this._configured.set(false),
      next: (status) => this._configured.set(status.configured),
    });
  }
}
