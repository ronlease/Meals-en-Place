import { inject, Injectable, signal } from '@angular/core';
import { ClaudeModel } from '../models/settings.models';
import { SettingsService } from './settings.service';

/**
 * App-wide source of truth for "is the Claude API key configured?" and "which
 * Claude model is selected?". All Claude-dependent UI affordances consult this
 * signal so the persistent banner and subtle in-page notes stay in sync with
 * the saved token state, and the Settings page model picker stays in sync with
 * the saved model preference (MEP-052).
 */
@Injectable({ providedIn: 'root' })
export class AiAvailabilityService {
  private readonly _configured = signal<boolean>(false);
  private readonly _dismissed = signal<boolean>(false);
  private readonly _model = signal<ClaudeModel>('Sonnet5');
  private readonly settingsService = inject(SettingsService);

  readonly configured = this._configured.asReadonly();
  readonly dismissed = this._dismissed.asReadonly();
  readonly model = this._model.asReadonly();

  dismissBanner(): void {
    this._dismissed.set(true);
  }

  refresh(): void {
    this.settingsService.getStatus().subscribe({
      // A status check that cannot reach the API leaves the last known value in
      // place rather than escaping as an uncaught error.
      error: () => undefined,
      next: (status) => {
        this._configured.set(status.configured);
        this._model.set(status.model);
        if (status.configured) {
          this._dismissed.set(false);
        }
      },
    });
  }

  setConfigured(configured: boolean): void {
    this._configured.set(configured);
    if (configured) {
      this._dismissed.set(false);
    }
  }

  setModel(model: ClaudeModel): void {
    this._model.set(model);
  }
}
