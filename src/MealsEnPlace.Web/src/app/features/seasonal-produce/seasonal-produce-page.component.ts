import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { SeasonalProduceResponse } from '../../core/models/seasonal-produce.models';
import { SeasonalProduceService } from '../../core/services/seasonal-produce.service';

type ViewMode = 'in-season' | 'all';

@Component({
  selector: 'app-seasonal-produce-page',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Seasonal Produce</h1>
      <span class="zone-badge">USDA Zone 7a</span>
    </div>

    <div class="filter-bar">
      <mat-button-toggle-group
        [value]="viewMode()"
        (change)="viewMode.set($event.value)"
        aria-label="View mode"
      >
        <mat-button-toggle value="in-season">In Season Now</mat-button-toggle>
        <mat-button-toggle value="all">Full Calendar</mat-button-toggle>
      </mat-button-toggle-group>
    </div>

    @if (loading()) {
      <div class="spinner-container">
        <mat-progress-spinner mode="indeterminate" diameter="48" />
      </div>
    } @else if (error()) {
      <div class="state-message error-message">
        <mat-icon>error_outline</mat-icon>
        <span>Failed to load seasonal produce data.</span>
      </div>
    } @else if (displayData().length === 0) {
      <div class="state-message">
        <mat-icon>eco</mat-icon>
        <span>No produce is currently in season for Zone 7a.</span>
      </div>
    } @else {
      <mat-table [dataSource]="displayData()" class="produce-table">
        <ng-container matColumnDef="name">
          <mat-header-cell *matHeaderCellDef>Produce</mat-header-cell>
          <mat-cell *matCellDef="let item">
            <mat-icon class="produce-icon">eco</mat-icon>
            {{ item.name }}
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="peakSeasonStart">
          <mat-header-cell *matHeaderCellDef>Season Start</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.peakSeasonStart }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="peakSeasonEnd">
          <mat-header-cell *matHeaderCellDef>Season End</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.peakSeasonEnd }}</mat-cell>
        </ng-container>

        <mat-header-row *matHeaderRowDef="displayedColumns" />
        <mat-row *matRowDef="let row; columns: displayedColumns;" />
      </mat-table>
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
        gap: 12px;
        margin-bottom: 16px;
      }

      .page-title {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }

      .zone-badge {
        display: inline-block;
        padding: 2px 10px;
        border-radius: 12px;
        font-size: 12px;
        font-weight: 500;
        background: #dcfce7;
        color: #166534;
      }

      .filter-bar {
        margin-bottom: 24px;
      }

      .spinner-container {
        display: flex;
        justify-content: center;
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

      .produce-table {
        width: 100%;
      }

      .produce-icon {
        color: #16a34a;
        margin-right: 8px;
        font-size: 18px;
        width: 18px;
        height: 18px;
      }
    `,
  ],
})
export class SeasonalProducePageComponent implements OnInit {
  protected readonly allWindows = signal<SeasonalProduceResponse[]>([]);
  protected readonly displayData = computed(() => {
    return this.viewMode() === 'in-season'
      ? this.inSeason()
      : this.allWindows();
  });
  protected readonly displayedColumns = ['name', 'peakSeasonStart', 'peakSeasonEnd'];
  protected readonly error = signal(false);
  protected readonly inSeason = signal<SeasonalProduceResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly viewMode = signal<ViewMode>('in-season');

  private readonly seasonalProduceService = inject(SeasonalProduceService);

  ngOnInit(): void {
    this.loading.set(true);
    this.seasonalProduceService.getInSeason().subscribe({
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
      next: (data) => {
        this.inSeason.set(data);
        this.seasonalProduceService.getAllWindows().subscribe({
          error: () => {
            this.loading.set(false);
          },
          next: (all) => {
            this.loading.set(false);
            this.allWindows.set(all);
          },
        });
      },
    });
  }
}
