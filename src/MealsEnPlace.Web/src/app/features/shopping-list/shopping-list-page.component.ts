import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { MealPlanResponse } from '../../core/models/meal-plan.models';
import { ShoppingListItemResponse } from '../../core/models/shopping-list.models';
import { MealPlanService } from '../../core/services/meal-plan.service';
import { ShoppingListService } from '../../core/services/shopping-list.service';

@Component({
  selector: 'app-shopping-list-page',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    RouterLink,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Shopping List</h1>
      @if (activePlan()) {
        <button mat-flat-button color="primary" (click)="regenerate()">
          <mat-icon>refresh</mat-icon>
          Regenerate
        </button>
      }
    </div>

    @if (loading()) {
      <div class="spinner-container">
        <mat-progress-spinner mode="indeterminate" diameter="48" />
      </div>
    } @else if (!activePlan()) {
      <div class="state-message">
        <mat-icon>shopping_cart</mat-icon>
        <span>No active meal plan. <a routerLink="/meal-plan">Generate one first.</a></span>
      </div>
    } @else if (items().length === 0) {
      <div class="state-message">
        <mat-icon>check_circle_outline</mat-icon>
        <span>Your inventory covers everything in this meal plan!</span>
      </div>
    } @else {
      <p class="plan-context">For: {{ activePlan()!.name }}</p>
      <mat-table [dataSource]="items()" class="shopping-table">
        <ng-container matColumnDef="category">
          <mat-header-cell *matHeaderCellDef>Category</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.category }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="canonicalIngredientName">
          <mat-header-cell *matHeaderCellDef>Ingredient</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.canonicalIngredientName }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="quantity">
          <mat-header-cell *matHeaderCellDef>Quantity</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.quantity }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="unitOfMeasureAbbreviation">
          <mat-header-cell *matHeaderCellDef>Unit</mat-header-cell>
          <mat-cell *matCellDef="let item">{{ item.unitOfMeasureAbbreviation }}</mat-cell>
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
        justify-content: space-between;
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

        a {
          color: var(--mat-sys-primary, #1976d2);
        }
      }

      .plan-context {
        font-size: 14px;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        margin-bottom: 16px;
      }

      .shopping-table {
        width: 100%;
      }
    `,
  ],
})
export class ShoppingListPageComponent implements OnInit {
  protected readonly activePlan = signal<MealPlanResponse | null>(null);
  protected readonly displayedColumns = [
    'category',
    'canonicalIngredientName',
    'quantity',
    'unitOfMeasureAbbreviation',
  ];
  protected readonly items = signal<ShoppingListItemResponse[]>([]);
  protected readonly loading = signal(false);

  private readonly mealPlanService = inject(MealPlanService);
  private readonly shoppingListService = inject(ShoppingListService);

  ngOnInit(): void {
    this.loading.set(true);
    this.mealPlanService.getActivePlan().subscribe({
      error: () => {
        this.loading.set(false);
        this.activePlan.set(null);
      },
      next: (plan) => {
        this.activePlan.set(plan);
        this.loadList(plan.id);
      },
    });
  }

  regenerate(): void {
    const plan = this.activePlan();
    if (!plan) return;
    this.loading.set(true);
    this.shoppingListService.generateList(plan.id).subscribe({
      error: () => this.loading.set(false),
      next: (items) => {
        this.loading.set(false);
        this.items.set(items);
      },
    });
  }

  private loadList(planId: string): void {
    this.shoppingListService.getList(planId).subscribe({
      error: () => this.loading.set(false),
      next: (items) => {
        this.loading.set(false);
        this.items.set(items);
      },
    });
  }
}
