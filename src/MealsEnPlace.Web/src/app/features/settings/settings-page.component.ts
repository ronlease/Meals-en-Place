import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AiAvailabilityService } from '../../core/services/ai-availability.service';
import { PreferencesService } from '../../core/services/preferences.service';
import { SettingsService } from '../../core/services/settings.service';
import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatDividerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
  ],
  selector: 'app-settings-page',
  styles: [
    `
      .settings-container {
        display: flex;
        flex-direction: column;
        gap: 16px;
        max-width: 720px;
        padding: 16px;
      }

      .section-card {
        padding: 8px;
      }

      .ai-row {
        align-items: center;
        display: flex;
        gap: 12px;
        flex-wrap: wrap;
      }

      .token-field {
        flex: 1 1 280px;
      }

      .status-pill {
        align-items: center;
        background: rgba(var(--mat-sys-tertiary-container-rgb, 200 200 200), 0.4);
        border-radius: 999px;
        display: inline-flex;
        gap: 6px;
        padding: 4px 10px;
      }

      .status-pill.configured {
        background: rgba(0, 200, 0, 0.12);
        color: #0a6;
      }

      .status-pill.not-configured {
        background: rgba(200, 80, 80, 0.12);
        color: #c54;
      }

      .stub-note {
        color: var(--mat-sys-on-surface-variant, #666);
        font-style: italic;
      }

      .actions {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
        margin-top: 8px;
      }

      .test-result {
        margin-top: 8px;
      }

      .test-result.success {
        color: #0a6;
      }

      .test-result.failure {
        color: #c54;
      }
    `,
  ],
  template: `
    <div class="settings-container">
      <h1>Settings</h1>

      <mat-card class="section-card">
        <mat-card-header>
          <mat-card-title>Display</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <mat-slide-toggle
            [checked]="preferencesService.displaySystem() === 'Metric'"
            (change)="preferencesService.toggleDisplaySystem()"
          >
            Use metric units (Imperial when off)
          </mat-slide-toggle>
        </mat-card-content>
      </mat-card>

      <mat-card class="section-card">
        <mat-card-header>
          <mat-card-title>AI (Claude API)</mat-card-title>
          <mat-card-subtitle>
            Paste your Anthropic API key to enable AI-backed features. The key is
            encrypted at rest and never returned by any endpoint.
          </mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <div class="ai-row">
            @if (aiAvailability.configured()) {
              <span class="status-pill configured">
                <mat-icon fontIcon="check_circle" inline></mat-icon>
                API key configured
              </span>
            } @else {
              <span class="status-pill not-configured">
                <mat-icon fontIcon="warning" inline></mat-icon>
                No API key — AI features disabled
              </span>
            }
          </div>

          <div class="ai-row" style="margin-top: 16px;">
            <mat-form-field appearance="outline" class="token-field">
              <mat-label>Anthropic API key</mat-label>
              <input
                matInput
                type="password"
                autocomplete="off"
                placeholder="sk-ant-..."
                [(ngModel)]="tokenInput"
              />
              <mat-hint>Stored encrypted; response never echoes the value back.</mat-hint>
            </mat-form-field>
          </div>

          <div class="actions">
            <button
              mat-flat-button
              color="primary"
              [disabled]="saving() || !tokenInput().trim()"
              (click)="save()"
            >
              Save
            </button>
            <button
              mat-stroked-button
              [disabled]="testing() || (!tokenInput().trim() && !aiAvailability.configured())"
              (click)="test()"
            >
              Test connection
            </button>
            @if (aiAvailability.configured()) {
              <button mat-stroked-button color="warn" (click)="remove()">Remove key</button>
            }
            @if (saving() || testing()) {
              <mat-progress-spinner diameter="24" mode="indeterminate" />
            }
          </div>

          @if (testResult(); as result) {
            <div class="test-result" [class.success]="result.success" [class.failure]="!result.success">
              @if (result.success) {
                <span><mat-icon fontIcon="check" inline></mat-icon> Anthropic accepted the key.</span>
              } @else {
                <span><mat-icon fontIcon="error" inline></mat-icon> {{ result.message }}</span>
              }
            </div>
          }
        </mat-card-content>
      </mat-card>

      <mat-card class="section-card">
        <mat-card-header>
          <mat-card-title>External Integrations</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <p class="stub-note">
            Todoist and other third-party providers will configure here when their
            stories ship (MEP-028, MEP-029).
          </p>
        </mat-card-content>
      </mat-card>

      <mat-card class="section-card">
        <mat-card-header>
          <mat-card-title>Inventory Behavior</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <mat-slide-toggle
            [checked]="preferencesService.autoDepleteOnConsume()"
            (change)="preferencesService.setAutoDepleteOnConsume($event.checked)"
          >
            Auto-deplete inventory when a meal is marked eaten
          </mat-slide-toggle>
          <p class="stub-note" style="margin-top: 8px;">
            When on, marking a meal plan slot as eaten deducts the recipe's
            ingredients from the oldest-expiry inventory rows first. Unmarking
            the slot restores those quantities.
          </p>
        </mat-card-content>
      </mat-card>
    </div>
  `,
})
export class SettingsPageComponent {
  protected readonly aiAvailability = inject(AiAvailabilityService);
  protected readonly preferencesService = inject(PreferencesService);
  protected readonly saving = signal(false);
  protected readonly testResult = signal<{ message: string; success: boolean } | null>(null);
  protected readonly testing = signal(false);
  protected readonly tokenInput = signal('');

  private readonly dialog = inject(MatDialog);
  private readonly settingsService = inject(SettingsService);
  private readonly snackBar = inject(MatSnackBar);

  constructor() {
    this.aiAvailability.refresh();
  }

  remove(): void {
    const data: ConfirmDialogData = {
      confirmLabel: 'Remove key',
      message: 'The stored Anthropic API key will be deleted. AI-backed features will disable until a new key is saved.',
      title: 'Remove Claude API key?',
    };
    this.dialog
      .open(ConfirmDialogComponent, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }
        this.settingsService.clearToken().subscribe({
          next: () => {
            this.aiAvailability.setConfigured(false);
            this.tokenInput.set('');
            this.testResult.set(null);
            this.snackBar.open('API key removed.', 'Dismiss', { duration: 4000 });
          },
        });
      });
  }

  save(): void {
    const raw = this.tokenInput().trim();
    if (!raw) {
      return;
    }
    this.saving.set(true);
    this.settingsService.saveToken(raw).subscribe({
      complete: () => this.saving.set(false),
      error: () => {
        this.saving.set(false);
        this.snackBar.open('Could not save the token. See console for details.', 'Dismiss', { duration: 5000 });
      },
      next: (status) => {
        this.aiAvailability.setConfigured(status.configured);
        this.tokenInput.set('');
        this.testResult.set(null);
        this.snackBar.open('API key saved.', 'Dismiss', { duration: 4000 });
      },
    });
  }

  test(): void {
    const candidate = this.tokenInput().trim();
    this.testing.set(true);
    this.testResult.set(null);
    this.settingsService.testToken(candidate.length > 0 ? candidate : undefined).subscribe({
      complete: () => this.testing.set(false),
      error: () => {
        this.testing.set(false);
        this.testResult.set({ message: 'Network error contacting the server.', success: false });
      },
      next: (result) =>
        this.testResult.set({
          message: result.errorMessage ?? 'Unknown error.',
          success: result.success,
        }),
    });
  }
}
