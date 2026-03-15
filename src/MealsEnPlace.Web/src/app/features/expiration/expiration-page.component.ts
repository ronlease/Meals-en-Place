import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { forkJoin } from 'rxjs';
import { InventoryItemResponse, InventoryLocation } from '../../core/models/inventory.models';
import { InventoryService } from '../../core/services/inventory.service';

type ExpiryFilter = 'all' | '7days' | '3days';

interface ExpiringItem extends InventoryItemResponse {
  daysRemaining: number;
}

@Component({
  selector: 'app-expiration-page',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Expiring Soon</h1>
    </div>

    <div class="filter-bar">
      <mat-button-toggle-group
        [value]="activeFilter()"
        (change)="activeFilter.set($event.value)"
        aria-label="Expiry filter"
      >
        <mat-button-toggle value="all">Show All</mat-button-toggle>
        <mat-button-toggle value="7days">Expiring Within 7 Days</mat-button-toggle>
        <mat-button-toggle value="3days">Expiring Within 3 Days</mat-button-toggle>
      </mat-button-toggle-group>
    </div>

    @if (loading()) {
      <div class="spinner-container">
        <mat-progress-spinner mode="indeterminate" diameter="48" />
      </div>
    } @else if (error()) {
      <div class="state-message error-message">
        <mat-icon>error_outline</mat-icon>
        <span>Failed to load inventory. Please try again.</span>
        <button mat-button color="primary" (click)="loadItems()">Retry</button>
      </div>
    } @else if (filteredItems().length === 0) {
      <div class="state-message">
        <mat-icon>check_circle_outline</mat-icon>
        <span>
          @if (activeFilter() === 'all') {
            No items with expiry dates found.
          } @else if (activeFilter() === '7days') {
            No items expiring within 7 days.
          } @else {
            No items expiring within 3 days.
          }
        </span>
      </div>
    } @else {
      <mat-table [dataSource]="filteredItems()" class="expiration-table">
        <ng-container matColumnDef="canonicalIngredientName">
          <mat-header-cell *matHeaderCellDef>Ingredient</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.canonicalIngredientName }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="location">
          <mat-header-cell *matHeaderCellDef>Location</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.location }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="quantity">
          <mat-header-cell *matHeaderCellDef>Quantity</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.quantity }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="uomAbbreviation">
          <mat-header-cell *matHeaderCellDef>UOM</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.uomAbbreviation }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="expiryDate">
          <mat-header-cell *matHeaderCellDef>Expiry Date</mat-header-cell>
          <mat-cell *matCellDef="let item">
            <span [class]="getExpiryBadgeClass(item.daysRemaining)">
              {{ item.expiryDate | date: 'mediumDate' }}
            </span>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="daysRemaining">
          <mat-header-cell *matHeaderCellDef>Days Remaining</mat-header-cell>
          <mat-cell *matCellDef="let item">
            @if (item.daysRemaining < 0) {
              <span class="expired-label">Expired</span>
            } @else if (item.daysRemaining === 0) {
              <span class="expiry-badge expiry-red">Expires today</span>
            } @else if (item.daysRemaining === 1) {
              <span class="expiry-badge expiry-red">1 day</span>
            } @else {
              <span [class]="getDaysRemainingClass(item.daysRemaining)">
                {{ item.daysRemaining }} days
              </span>
            }
          </mat-cell>
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
        margin-bottom: 16px;
      }

      .page-title {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }

      .filter-bar {
        margin-bottom: 24px;
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
        color: rgba(0, 0, 0, 0.54);
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

      .expiration-table {
        width: 100%;
      }

      .expiry-badge {
        display: inline-block;
        padding: 2px 10px;
        border-radius: 12px;
        font-size: 13px;
        font-weight: 500;
      }

      .expiry-ok {
        background: #dcfce7;
        color: #166534;
      }

      .expiry-amber {
        background: #fef3c7;
        color: #92400e;
      }

      .expiry-red {
        background: #fee2e2;
        color: #991b1b;
      }

      .expired-label {
        font-weight: 700;
        color: #991b1b;
      }
    `,
  ],
})
export class ExpirationPageComponent implements OnInit {
  protected readonly activeFilter = signal<ExpiryFilter>('all');
  protected readonly allItems = signal<ExpiringItem[]>([]);
  protected readonly displayedColumns = [
    'canonicalIngredientName',
    'location',
    'quantity',
    'uomAbbreviation',
    'expiryDate',
    'daysRemaining',
  ];
  protected readonly error = signal(false);
  protected readonly filteredItems = computed(() => {
    const filter = this.activeFilter();
    const items = this.allItems();
    if (filter === '3days') {
      return items.filter((i) => i.daysRemaining <= 3);
    }
    if (filter === '7days') {
      return items.filter((i) => i.daysRemaining <= 7);
    }
    return items;
  });
  protected readonly loading = signal(false);

  private readonly inventoryService = inject(InventoryService);

  getDaysRemainingClass(daysRemaining: number): string {
    if (daysRemaining < 3) return 'expiry-badge expiry-red';
    if (daysRemaining <= 7) return 'expiry-badge expiry-amber';
    return 'expiry-badge expiry-ok';
  }

  getExpiryBadgeClass(daysRemaining: number): string {
    if (daysRemaining < 3) return 'expiry-badge expiry-red';
    if (daysRemaining <= 7) return 'expiry-badge expiry-amber';
    return 'expiry-badge expiry-ok';
  }

  loadItems(): void {
    this.error.set(false);
    this.loading.set(true);

    const locations: InventoryLocation[] = ['Pantry', 'Fridge', 'Freezer'];
    forkJoin(locations.map((loc) => this.inventoryService.getItems(loc))).subscribe({
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
      next: ([pantry, fridge, freezer]) => {
        const today = new Date();
        today.setHours(0, 0, 0, 0);

        const withExpiry = [...pantry, ...fridge, ...freezer]
          .filter((item) => item.expiryDate !== null && item.expiryDate !== '')
          .map((item): ExpiringItem => {
            const expiry = new Date(item.expiryDate!);
            expiry.setHours(0, 0, 0, 0);
            const daysRemaining = Math.ceil(
              (expiry.getTime() - today.getTime()) / (1000 * 60 * 60 * 24)
            );
            return { ...item, daysRemaining };
          })
          .sort((a, b) => a.daysRemaining - b.daysRemaining);

        this.loading.set(false);
        this.allItems.set(withExpiry);
      },
    });
  }

  ngOnInit(): void {
    this.loadItems();
  }
}
