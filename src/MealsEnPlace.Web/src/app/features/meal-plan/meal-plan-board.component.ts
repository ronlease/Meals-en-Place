import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ConsumeMealResponse,
  MealPlanResponse,
  MealPlanSlotResponse,
} from '../../core/models/meal-plan.models';
import { RecipeListItemDto } from '../../core/models/recipe.models';
import { MealPlanService } from '../../core/services/meal-plan.service';
import { RecipeService } from '../../core/services/recipe.service';
import { MealPlanGenerateDialogComponent } from './meal-plan-generate-dialog.component';
import {
  MealPlanReorderDialogComponent,
  ReorderDialogData,
} from './meal-plan-reorder-dialog.component';
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
      <div class="header-actions">
        @if (plan()) {
          <button
            mat-stroked-button
            (click)="openReorderDialog()"
            [disabled]="reordering()"
            matTooltip="Move recipes with expiring ingredients to earlier days"
          >
            <mat-icon>schedule</mat-icon>
            Reorder by expiry
          </button>
        }
        <button mat-flat-button color="primary" (click)="openGenerateDialog()">
          <mat-icon>auto_awesome</mat-icon>
          Generate Plan
        </button>
      </div>
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
                [class.consumed]="slot.consumedAt"
                (click)="openSwapDialog(slot)"
                matTooltip="Click to swap recipe"
              >
                <div class="slot-label-row">
                  <span class="slot-label">{{ slot.mealSlot }}</span>
                  @if (slot.consumedAt) {
                    <mat-icon class="consumed-icon" aria-label="Eaten" matTooltip="Marked as eaten">check_circle</mat-icon>
                  }
                </div>
                <div class="slot-recipe">{{ slot.recipeTitle }}</div>
                <div class="slot-cuisine">{{ slot.cuisineType }}</div>
                <div class="slot-actions">
                  @if (slot.consumedAt) {
                    <button
                      mat-button
                      class="slot-action-button"
                      (click)="unconsumeSlot(slot); $event.stopPropagation()"
                      [disabled]="consumingSlotId() === slot.id"
                    >
                      <mat-icon>undo</mat-icon>
                      Unmark
                    </button>
                  } @else {
                    <button
                      mat-button
                      class="slot-action-button"
                      (click)="consumeSlot(slot); $event.stopPropagation()"
                      [disabled]="consumingSlotId() === slot.id"
                    >
                      <mat-icon>restaurant</mat-icon>
                      Mark eaten
                    </button>
                  }
                </div>
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

      .header-actions {
        display: flex;
        gap: 8px;
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

        &.consumed {
          opacity: 0.7;

          .slot-recipe { text-decoration: line-through; }
        }

        .slot-label-row {
          align-items: center;
          display: flex;
          gap: 6px;
          margin-bottom: 4px;
        }

        .slot-label {
          font-size: 11px;
          text-transform: uppercase;
          font-weight: 600;
          color: var(--mat-sys-primary, #1976d2);
        }

        .consumed-icon {
          color: #0a6;
          font-size: 16px;
          height: 16px;
          width: 16px;
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

        .slot-actions {
          display: flex;
          justify-content: flex-end;
          margin-top: 6px;
        }

        .slot-action-button {
          font-size: 11px;
          line-height: 1.2;
          min-width: 0;
          padding: 0 8px;
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
  protected readonly consumingSlotId = signal<string | null>(null);
  protected readonly reordering = signal(false);
  protected readonly error = signal(false);
  protected readonly loading = signal(false);
  protected readonly plan = signal<MealPlanResponse | null>(null);

  private readonly dialog = inject(MatDialog);
  private readonly mealPlanService = inject(MealPlanService);
  private readonly recipeService = inject(RecipeService);
  private readonly snackBar = inject(MatSnackBar);
  private recipes: RecipeListItemDto[] = [];

  consumeSlot(slot: MealPlanSlotResponse): void {
    this.consumingSlotId.set(slot.id);
    this.mealPlanService.consumeSlot(slot.id).subscribe({
      error: () => {
        this.consumingSlotId.set(null);
        this.snackBar.open('Could not mark the meal as eaten.', 'Dismiss', { duration: 5000 });
      },
      next: (result) => {
        this.consumingSlotId.set(null);
        this.applyConsumedAt(slot.id, result.consumedAt);
        this.showConsumeResultSnackbar(result);
      },
    });
  }

  unconsumeSlot(slot: MealPlanSlotResponse): void {
    this.consumingSlotId.set(slot.id);
    this.mealPlanService.unconsumeSlot(slot.id).subscribe({
      error: () => {
        this.consumingSlotId.set(null);
        this.snackBar.open('Could not unmark the meal.', 'Dismiss', { duration: 5000 });
      },
      next: () => {
        this.consumingSlotId.set(null);
        this.applyConsumedAt(slot.id, null);
      },
    });
  }

  private applyConsumedAt(slotId: string, consumedAt: string | null): void {
    this.plan.update((p) => {
      if (!p) return p;
      return {
        ...p,
        slots: p.slots.map((s) =>
          s.id === slotId ? { ...s, consumedAt } : s
        ),
      };
    });
  }

  private showConsumeResultSnackbar(result: ConsumeMealResponse): void {
    if (result.shortIngredients.length > 0) {
      const names = result.shortIngredients
        .map((s) => `${s.ingredientName} (short by ${s.shortBy} ${s.unitOfMeasureAbbreviation})`.trim())
        .join(', ');
      this.snackBar.open(`Marked eaten. Inventory was short on: ${names}`, 'Dismiss', { duration: 8000 });
    } else if (result.autoDepleteApplied) {
      this.snackBar.open('Marked eaten. Inventory updated.', 'Dismiss', { duration: 3000 });
    } else {
      this.snackBar.open('Marked eaten.', 'Dismiss', { duration: 3000 });
    }
  }

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

  openReorderDialog(): void {
    const currentPlan = this.plan();
    if (!currentPlan) return;

    this.reordering.set(true);
    this.mealPlanService.previewReorderByExpiry(currentPlan.id).subscribe({
      error: () => {
        this.reordering.set(false);
        this.snackBar.open('Could not compute reorder preview.', 'Dismiss', { duration: 5000 });
      },
      next: (preview) => {
        this.reordering.set(false);
        const data: ReorderDialogData = { preview };
        const ref = this.dialog.open(MealPlanReorderDialogComponent, { data });
        ref.afterClosed().subscribe((confirmed: boolean | undefined) => {
          if (!confirmed) return;
          this.reordering.set(true);
          this.mealPlanService
            .applyReorderByExpiry(currentPlan.id, preview.urgencyWindowDays)
            .subscribe({
              error: () => {
                this.reordering.set(false);
                this.snackBar.open('Could not apply reorder.', 'Dismiss', { duration: 5000 });
              },
              next: (refreshed) => {
                this.reordering.set(false);
                this.plan.set(refreshed);
                this.snackBar.open('Meal plan reordered.', 'Dismiss', { duration: 3000 });
              },
            });
        });
      },
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
