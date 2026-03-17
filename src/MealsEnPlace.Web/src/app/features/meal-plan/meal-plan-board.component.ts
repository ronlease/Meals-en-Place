import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  MealPlanResponse,
  MealPlanSlotResponse,
} from '../../core/models/meal-plan.models';
import { RecipeListItemDto } from '../../core/models/recipe.models';
import { MealPlanService } from '../../core/services/meal-plan.service';
import { RecipeService } from '../../core/services/recipe.service';
import { MealPlanGenerateDialogComponent } from './meal-plan-generate-dialog.component';
import {
  MealPlanSwapDialogComponent,
  SwapDialogData,
} from './meal-plan-swap-dialog.component';

const DAY_ORDER = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];
const SLOT_ORDER = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

@Component({
  selector: 'app-meal-plan-board',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Meal Plan</h1>
      <button mat-flat-button color="primary" (click)="openGenerateDialog()">
        <mat-icon>auto_awesome</mat-icon>
        Generate Plan
      </button>
    </div>

    @if (loading()) {
      <div class="spinner-container">
        <mat-progress-spinner mode="indeterminate" diameter="48" />
      </div>
    } @else if (error()) {
      <div class="state-message error-message">
        <mat-icon>error_outline</mat-icon>
        <span>Failed to load meal plan. Please try again.</span>
        <button mat-button color="primary" (click)="loadActivePlan()">Retry</button>
      </div>
    } @else if (!plan()) {
      <div class="state-message">
        <mat-icon>calendar_today</mat-icon>
        <span>No meal plan yet. Generate one to get started!</span>
      </div>
    } @else {
      <div class="plan-info">
        <span class="plan-name">{{ plan()!.name }}</span>
        <span class="plan-date">Week of {{ plan()!.weekStartDate }}</span>
      </div>
      <div class="board-grid">
        @for (day of days; track day) {
          <div class="day-column">
            <div class="day-header">{{ day }}</div>
            @for (slot of getSlotsForDay(day); track slot.id) {
              <mat-card
                class="slot-card"
                (click)="openSwapDialog(slot)"
                matTooltip="Click to swap recipe"
              >
                <div class="slot-label">{{ slot.mealSlot }}</div>
                <div class="slot-recipe">{{ slot.recipeTitle }}</div>
                <div class="slot-cuisine">{{ slot.cuisineType }}</div>
              </mat-card>
            }
            @if (getSlotsForDay(day).length === 0) {
              <div class="empty-day">No meals</div>
            }
          </div>
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
      }

      .error-message {
        color: #b91c1c;

        mat-icon {
          opacity: 1;
          color: #b91c1c;
        }
      }

      .plan-info {
        display: flex;
        align-items: baseline;
        gap: 12px;
        margin-bottom: 16px;

        .plan-name {
          font-size: 16px;
          font-weight: 500;
        }

        .plan-date {
          font-size: 14px;
          color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        }
      }

      .board-grid {
        display: grid;
        grid-template-columns: repeat(7, 1fr);
        gap: 8px;
        overflow-x: auto;
      }

      .day-column {
        min-width: 140px;
      }

      .day-header {
        text-align: center;
        font-weight: 500;
        font-size: 14px;
        padding: 8px 0;
        border-bottom: 2px solid var(--mat-sys-outline-variant, #e0e0e0);
        margin-bottom: 8px;
      }

      .slot-card {
        cursor: pointer;
        margin-bottom: 8px;
        padding: 12px;
        transition: box-shadow 0.15s ease;

        &:hover {
          box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
        }

        .slot-label {
          font-size: 11px;
          text-transform: uppercase;
          font-weight: 600;
          color: var(--mat-sys-primary, #1976d2);
          margin-bottom: 4px;
        }

        .slot-recipe {
          font-size: 13px;
          font-weight: 500;
          margin-bottom: 2px;
        }

        .slot-cuisine {
          font-size: 12px;
          color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        }
      }

      .empty-day {
        text-align: center;
        padding: 16px;
        font-size: 13px;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
      }
    `,
  ],
})
export class MealPlanBoardComponent implements OnInit {
  readonly days = DAY_ORDER;
  protected readonly error = signal(false);
  protected readonly loading = signal(false);
  protected readonly plan = signal<MealPlanResponse | null>(null);

  private readonly dialog = inject(MatDialog);
  private readonly mealPlanService = inject(MealPlanService);
  private readonly recipeService = inject(RecipeService);
  private recipes: RecipeListItemDto[] = [];

  getSlotsForDay(day: string): MealPlanSlotResponse[] {
    const p = this.plan();
    if (!p) return [];
    return p.slots
      .filter((s) => s.dayOfWeek === day)
      .sort(
        (a, b) =>
          SLOT_ORDER.indexOf(a.mealSlot) - SLOT_ORDER.indexOf(b.mealSlot)
      );
  }

  loadActivePlan(): void {
    this.error.set(false);
    this.loading.set(true);
    this.mealPlanService.getActivePlan().subscribe({
      error: (err) => {
        this.loading.set(false);
        if (err.status === 404) {
          this.plan.set(null);
        } else {
          this.error.set(true);
        }
      },
      next: (plan) => {
        this.loading.set(false);
        this.plan.set(plan);
      },
    });
  }

  ngOnInit(): void {
    this.loadActivePlan();
    this.recipeService.getRecipes().subscribe({
      next: (recipes) => (this.recipes = recipes),
    });
  }

  openGenerateDialog(): void {
    const ref = this.dialog.open(MealPlanGenerateDialogComponent);
    ref.afterClosed().subscribe((request) => {
      if (!request) return;
      this.loading.set(true);
      this.mealPlanService.generatePlan(request).subscribe({
        error: () => {
          this.loading.set(false);
          this.error.set(true);
        },
        next: (plan) => {
          this.loading.set(false);
          this.plan.set(plan);
        },
      });
    });
  }

  openSwapDialog(slot: MealPlanSlotResponse): void {
    const data: SwapDialogData = {
      currentRecipeId: slot.recipeId,
      currentRecipeTitle: slot.recipeTitle,
      recipes: this.recipes,
    };
    const ref = this.dialog.open(MealPlanSwapDialogComponent, { data });
    ref.afterClosed().subscribe((recipeId: string | undefined) => {
      if (!recipeId) return;
      this.mealPlanService
        .swapSlot(slot.id, { recipeId })
        .subscribe({
          next: (updated) => {
            this.plan.update((p) => {
              if (!p) return p;
              return {
                ...p,
                slots: p.slots.map((s) =>
                  s.id === slot.id ? updated : s
                ),
              };
            });
          },
        });
    });
  }
}
