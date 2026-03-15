import { Component, signal, viewChildren } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { InventoryLocation } from '../../core/models/inventory.models';
import { InventoryTableComponent } from './inventory-table.component';

@Component({
  selector: 'app-inventory-page',
  standalone: true,
  imports: [
    InventoryTableComponent,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Inventory</h1>
      <button mat-flat-button color="primary" (click)="addItem()">
        <mat-icon>add</mat-icon>
        Add Item
      </button>
    </div>

    <mat-tab-group
      animationDuration="200ms"
      (selectedIndexChange)="onTabChange($event)"
    >
      <mat-tab label="Pantry">
        <ng-template matTabContent>
          <app-inventory-table location="Pantry" />
        </ng-template>
      </mat-tab>

      <mat-tab label="Fridge">
        <ng-template matTabContent>
          <app-inventory-table location="Fridge" />
        </ng-template>
      </mat-tab>

      <mat-tab label="Freezer">
        <ng-template matTabContent>
          <app-inventory-table location="Freezer" />
        </ng-template>
      </mat-tab>
    </mat-tab-group>
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
        justify-content: space-between;
        margin-bottom: 16px;
      }

      .page-title {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }
    `,
  ],
})
export class InventoryPageComponent {
  protected readonly activeLocation = signal<InventoryLocation>('Pantry');
  private readonly tables = viewChildren(InventoryTableComponent);

  addItem(): void {
    const activeTable = this.tables().find(
      (t) => t.location() === this.activeLocation()
    );
    activeTable?.openAddDialog();
  }

  onTabChange(index: number): void {
    const locations: InventoryLocation[] = ['Pantry', 'Fridge', 'Freezer'];
    this.activeLocation.set(locations[index]);
  }
}
