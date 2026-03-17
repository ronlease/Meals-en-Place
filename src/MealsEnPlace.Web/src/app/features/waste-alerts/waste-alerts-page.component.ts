import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WasteAlertResponse } from '../../core/models/waste-alert.models';
import { WasteAlertService } from '../../core/services/waste-alert.service';

@Component({
  selector: 'app-waste-alerts-page',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Waste Alerts</h1>
    </div>

    @if (loading()) {
      <div class="spinner-container">
        <mat-progress-spinner mode="indeterminate" diameter="48" />
      </div>
    } @else if (error()) {
      <div class="state-message error-message">
        <mat-icon>error_outline</mat-icon>
        <span>Failed to load waste alerts. Please try again.</span>
        <button mat-button color="primary" (click)="loadAlerts()">Retry</button>
      </div>
    } @else if (alerts().length === 0) {
      <div class="state-message">
        <mat-icon>check_circle_outline</mat-icon>
        <span>No waste alerts right now. All items are fresh!</span>
      </div>
    } @else {
      <div class="alerts-grid">
        @for (alert of alerts(); track alert.alertId) {
          <mat-card class="alert-card">
            <mat-card-header>
              <mat-icon mat-card-avatar class="alert-icon" [class]="getUrgencyClass(alert.daysUntilExpiry)">
                warning
              </mat-icon>
              <mat-card-title>{{ alert.canonicalIngredientName }}</mat-card-title>
              <mat-card-subtitle>
                {{ alert.quantity }} {{ alert.uomAbbreviation }} · {{ alert.location }}
              </mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              <div class="expiry-info">
                <span [class]="getExpiryBadgeClass(alert.daysUntilExpiry)">
                  @if (alert.daysUntilExpiry < 0) {
                    Expired {{ -alert.daysUntilExpiry }} day{{ -alert.daysUntilExpiry === 1 ? '' : 's' }} ago
                  } @else if (alert.daysUntilExpiry === 0) {
                    Expires today
                  } @else {
                    Expires in {{ alert.daysUntilExpiry }} day{{ alert.daysUntilExpiry === 1 ? '' : 's' }}
                  }
                </span>
                <span class="expiry-date">{{ alert.expiryDate | date: 'mediumDate' }}</span>
              </div>
              <div class="recipe-suggestions">
                <span class="suggestions-label">Use it in:</span>
                <mat-chip-set>
                  @for (recipe of alert.matchedRecipes; track recipe.recipeId) {
                    <mat-chip>
                      <mat-icon matChipAvatar>restaurant</mat-icon>
                      {{ recipe.title }}
                    </mat-chip>
                  }
                </mat-chip-set>
              </div>
            </mat-card-content>
            <mat-card-actions>
              <button mat-button (click)="dismiss(alert.alertId)">
                <mat-icon>close</mat-icon>
                Dismiss
              </button>
            </mat-card-actions>
          </mat-card>
        }
      </div>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        padding: 24px;
      }

      .page-header {
        display: flex;
        align-items: center;
        margin-bottom: 16px;
      }

      .page-title {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }

      .spinner-container {
        display: flex;
        justify-content: center;
        align-items: center;
        padding: 48px;
      }

      .state-message {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 32px 16px;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        font-size: 14px;

        mat-icon {
          opacity: 0.5;
        }
      }

      .error-message {
        color: #b91c1c;

        mat-icon {
          opacity: 1;
          color: #b91c1c;
        }
      }

      .alerts-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
        gap: 16px;
      }

      .alert-card {
        .alert-icon {
          display: flex;
          align-items: center;
          justify-content: center;
          width: 40px;
          height: 40px;
          border-radius: 50%;
          font-size: 20px;
        }

        .urgency-expired,
        .urgency-critical {
          background: #fee2e2;
          color: #991b1b;
        }

        .urgency-warning {
          background: #fef3c7;
          color: #92400e;
        }

        .urgency-soon {
          background: #dbeafe;
          color: #1e40af;
        }
      }

      .expiry-info {
        display: flex;
        align-items: center;
        gap: 12px;
        margin-bottom: 16px;
      }

      .expiry-badge {
        display: inline-block;
        padding: 2px 10px;
        border-radius: 12px;
        font-size: 13px;
        font-weight: 500;
      }

      .expiry-red {
        background: #fee2e2;
        color: #991b1b;
      }

      .expiry-amber {
        background: #fef3c7;
        color: #92400e;
      }

      .expiry-blue {
        background: #dbeafe;
        color: #1e40af;
      }

      .expiry-date {
        font-size: 13px;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
      }

      .recipe-suggestions {
        .suggestions-label {
          display: block;
          font-size: 13px;
          font-weight: 500;
          margin-bottom: 8px;
          color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        }
      }
    `,
  ],
})
export class WasteAlertsPageComponent implements OnInit {
  protected readonly alerts = signal<WasteAlertResponse[]>([]);
  protected readonly error = signal(false);
  protected readonly loading = signal(false);

  private readonly wasteAlertService = inject(WasteAlertService);

  dismiss(alertId: string): void {
    this.wasteAlertService.dismissAlert(alertId).subscribe({
      error: () => {},
      next: () => {
        this.alerts.update((current) =>
          current.filter((a) => a.alertId !== alertId)
        );
      },
    });
  }

  getExpiryBadgeClass(daysUntilExpiry: number): string {
    if (daysUntilExpiry <= 0) return 'expiry-badge expiry-red';
    if (daysUntilExpiry <= 1) return 'expiry-badge expiry-amber';
    return 'expiry-badge expiry-blue';
  }

  getUrgencyClass(daysUntilExpiry: number): string {
    if (daysUntilExpiry < 0) return 'urgency-expired';
    if (daysUntilExpiry <= 1) return 'urgency-critical';
    if (daysUntilExpiry <= 2) return 'urgency-warning';
    return 'urgency-soon';
  }

  loadAlerts(): void {
    this.error.set(false);
    this.loading.set(true);

    this.wasteAlertService.getAlerts().subscribe({
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
      next: (alerts) => {
        this.loading.set(false);
        this.alerts.set(alerts);
      },
    });
  }

  ngOnInit(): void {
    this.loadAlerts();
  }
}
