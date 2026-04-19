import { inject, Injectable, signal } from '@angular/core';
import { SettingsService } from './settings.service';

/**
 * App-wide source of truth for "is the Claude API key configured?". All
 * Claude-dependent UI affordances consult this signal so the persistent banner
 * and subtle in-page notes stay in sync with the saved token state.
 */
@Injectable({ providedIn: 'root' })
export class AiAvailabilityService {
  private readonly _configured = signal<boolean>(false);
  private readonly _dismissed = signal<boolean>(false);
  private readonly settingsService = inject(SettingsService);

  readonly configured = this._configured.asReadonly();
  readonly dismissed = this._dismissed.asReadonly();

  dismissBanner(): void {
    this._dismissed.set(true);
  }

  refresh(): void {
    this.settingsService.getStatus().subscribe({
      next: (status) => {
        this._configured.set(status.configured);
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
}
