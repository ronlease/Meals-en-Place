import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import {
  DisplaySystem,
  UpdateUserPreferencesRequest,
  UserPreferencesResponse
} from '../models/preferences.models';

@Injectable({ providedIn: 'root' })
export class PreferencesService {
  private readonly _displaySystem = signal<DisplaySystem>('Imperial');
  private readonly baseUrl = `${environment.apiUrl}/v1/preferences`;
  private readonly http = inject(HttpClient);

  readonly displaySystem = this._displaySystem.asReadonly();

  loadPreferences(): void {
    this.http.get<UserPreferencesResponse>(this.baseUrl).subscribe({
      next: (prefs) => this._displaySystem.set(prefs.displaySystem),
    });
  }

  toggleDisplaySystem(): void {
    const next: DisplaySystem = this._displaySystem() === 'Imperial' ? 'Metric' : 'Imperial';
    const request: UpdateUserPreferencesRequest = { displaySystem: next };
    this.http.put<UserPreferencesResponse>(this.baseUrl, request).subscribe({
      next: (prefs) => this._displaySystem.set(prefs.displaySystem),
    });
  }
}
