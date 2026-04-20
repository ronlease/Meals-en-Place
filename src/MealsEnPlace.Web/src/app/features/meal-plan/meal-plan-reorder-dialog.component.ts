import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ReorderPreviewResponse } from '../../core/models/meal-plan.models';

export interface ReorderDialogData {
  preview: ReorderPreviewResponse;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
  selector: 'app-meal-plan-reorder-dialog',
  styles: [
    `
      .dialog-body {
        min-width: 420px;
      }

      .no-changes {
        align-items: center;
        display: flex;
        gap: 8px;
      }

      .changes-table {
        border-collapse: collapse;
        margin-top: 8px;
        width: 100%;
      }

      .changes-table th,
      .changes-table td {
        border-bottom: 1px solid var(--mat-sys-outline-variant, #e0e0e0);
        padding: 8px 6px;
        text-align: left;
      }

      .changes-table th {
        font-size: 12px;
        font-weight: 600;
        text-transform: uppercase;
      }

      .arrow {
        color: var(--mat-sys-primary, #1976d2);
      }

      .legend {
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        font-size: 12px;
        margin-top: 12px;
      }
    `,
  ],
  template: `
    <h2 mat-dialog-title>Reorder meal plan by expiry</h2>
    <mat-dialog-content class="dialog-body">
      @if (!data.preview.hasChanges) {
        <div class="no-changes">
          <mat-icon fontIcon="check_circle" inline></mat-icon>
          <span>{{ data.preview.reason }}</span>
        </div>
      } @else {
        <p>
          These slots would change day assignments. Meal occasions (Breakfast,
          Lunch, Dinner, Snack) stay on their current meal; only the day within
          that meal shuffles.
        </p>
        <table class="changes-table">
          <thead>
            <tr>
              <th>Meal</th>
              <th>Recipe</th>
              <th>From</th>
              <th>To</th>
              <th>Urgency</th>
            </tr>
          </thead>
          <tbody>
            @for (change of data.preview.changes; track change.id) {
              <tr>
                <td>{{ change.mealSlot }}</td>
                <td>{{ change.recipeTitle }}</td>
                <td>{{ change.originalDay }}</td>
                <td>
                  <span class="arrow" aria-label="changes to">→</span>
                  {{ change.proposedDay }}
                </td>
                <td>{{ change.urgencyScore.toFixed(2) }}</td>
              </tr>
            }
          </tbody>
        </table>
        <div class="legend">
          Urgency window: next {{ data.preview.urgencyWindowDays }} day(s).
          Higher urgency = more ingredients expiring soon.
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (data.preview.hasChanges) {
        <button mat-button [mat-dialog-close]="false">Cancel</button>
        <button mat-flat-button color="primary" [mat-dialog-close]="true">
          Apply reorder
        </button>
      } @else {
        <button mat-flat-button [mat-dialog-close]="false">Close</button>
      }
    </mat-dialog-actions>
  `,
})
export class MealPlanReorderDialogComponent {
  protected readonly data = inject<ReorderDialogData>(MAT_DIALOG_DATA);
  protected readonly dialogRef = inject(MatDialogRef<MealPlanReorderDialogComponent, boolean>);
}
