import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, input, OnInit, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  InventoryItemResponse,
  InventoryLocation,
} from '../../core/models/inventory.models';
import { InventoryService } from '../../core/services/inventory.service';
import {
  InventoryDialogComponent,
  InventoryDialogData,
} from './inventory-dialog.component';

@Component({
  selector: 'app-inventory-table',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
  ],
  template: `
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
    } @else if (items().length === 0) {
      <div class="state-message">
        <mat-icon>kitchen</mat-icon>
        <span>No items in {{ location() }}. Add one to get started.</span>
      </div>
    } @else {
      <mat-table [dataSource]="items()" class="inventory-table">
        <ng-container matColumnDef="canonicalIngredientName">
          <mat-header-cell *matHeaderCellDef>Ingredient</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.canonicalIngredientName }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="quantity">
          <mat-header-cell *matHeaderCellDef>Quantity</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.quantity }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="unitOfMeasureAbbreviation">
          <mat-header-cell *matHeaderCellDef>Unit of Measure</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.unitOfMeasureAbbreviation }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="expiryDate">
          <mat-header-cell *matHeaderCellDef>Expiry Date</mat-header-cell>
          <mat-cell *matCellDef="let item">
            @if (item.expiryDate) {
              <span [class]="getExpiryClass(item.expiryDate)" class="expiry-badge">
                {{ item.expiryDate | date: 'mediumDate' }}
              </span>
            } @else {
              <span class="no-expiry">—</span>
            }
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="actions">
          <mat-header-cell *matHeaderCellDef></mat-header-cell>
          <mat-cell *matCellDef="let item" class="actions-cell">
            <button
              mat-icon-button
              matTooltip="Edit"
              (click)="openEditDialog(item)"
              [disabled]="deletingId() === item.id"
            >
              <mat-icon>edit</mat-icon>
            </button>
            <button
              mat-icon-button
              matTooltip="Delete"
              color="warn"
              (click)="deleteItem(item)"
              [disabled]="deletingId() === item.id"
            >
              @if (deletingId() === item.id) {
                <mat-progress-spinner mode="indeterminate" diameter="20" />
              } @else {
                <mat-icon>delete</mat-icon>
              }
            </button>
          </mat-cell>
        </ng-container>

        <mat-header-row *matHeaderRowDef="displayedColumns" />
        <mat-row *matRowDef="let row; columns: displayedColumns;" />
      </mat-table>
    }
  `,
  styles: [
    `
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

      .inventory-table {
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

      .no-expiry {
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.38));
      }

      .actions-cell {
        display: flex;
        gap: 4px;
      }
    `,
  ],
})
export class InventoryTableComponent implements OnInit {
  protected readonly deletingId = signal<string | null>(null);
  protected readonly displayedColumns = [
    'canonicalIngredientName',
    'quantity',
    'unitOfMeasureAbbreviation',
    'expiryDate',
    'actions',
  ];
  protected readonly error = signal(false);
  protected readonly items = signal<InventoryItemResponse[]>([]);
  protected readonly loading = signal(false);
  readonly itemDeleted = output<string>();
  readonly location = input.required<InventoryLocation>();

  private readonly dialog = inject(MatDialog);
  private readonly inventoryService = inject(InventoryService);
  private readonly snackBar = inject(MatSnackBar);

  deleteItem(item: InventoryItemResponse): void {
    this.deletingId.set(item.id);
    this.inventoryService.deleteItem(item.id).subscribe({
      error: () => {
        this.deletingId.set(null);
        this.snackBar.open('Failed to delete item.', 'Dismiss', {
          duration: 4000,
        });
      },
      next: () => {
        this.deletingId.set(null);
        this.items.update((list) => list.filter((i) => i.id !== item.id));
        this.itemDeleted.emit(item.id);
        this.snackBar.open('Item deleted.', undefined, { duration: 2500 });
      },
    });
  }

  getExpiryClass(expiryDate: string): string {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const expiry = new Date(expiryDate);
    expiry.setHours(0, 0, 0, 0);
    const daysRemaining = Math.ceil(
      (expiry.getTime() - today.getTime()) / (1000 * 60 * 60 * 24)
    );
    if (daysRemaining < 3) return 'expiry-badge expiry-red';
    if (daysRemaining <= 7) return 'expiry-badge expiry-amber';
    return 'expiry-badge expiry-ok';
  }

  loadItems(): void {
    this.error.set(false);
    this.loading.set(true);
    this.inventoryService.getItems(this.location()).subscribe({
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
      next: (items) => {
        this.loading.set(false);
        this.items.set(items);
      },
    });
  }

  ngOnInit(): void {
    this.loadItems();
  }

  openAddDialog(): void {
    const dialogData: InventoryDialogData = {
      location: this.location(),
      mode: 'add',
    };
    this.dialog
      .open(InventoryDialogComponent, { data: dialogData, width: '500px' })
      .afterClosed()
      .subscribe((result: InventoryItemResponse | null) => {
        if (result) {
          this.items.update((list) => [...list, result]);
        }
      });
  }

  openEditDialog(item: InventoryItemResponse): void {
    const dialogData: InventoryDialogData = {
      item,
      location: this.location(),
      mode: 'edit',
    };
    this.dialog
      .open(InventoryDialogComponent, { data: dialogData, width: '500px' })
      .afterClosed()
      .subscribe((result: InventoryItemResponse | null) => {
        if (result) {
          this.items.update((list) =>
            list.map((i) => (i.id === result.id ? result : i))
          );
        }
      });
  }
}
