import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { GenerateMealPlanRequest } from '../../core/models/meal-plan.models';

@Component({
  selector: 'app-meal-plan-generate-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Generate Meal Plan</h2>
    <mat-dialog-content>
      <mat-form-field class="full-width">
        <mat-label>Plan Name</mat-label>
        <input matInput [(ngModel)]="planName" placeholder="e.g., Week of March 16" />
      </mat-form-field>
      <mat-checkbox [(ngModel)]="seasonalOnly">
        Prefer seasonal ingredients
      </mat-checkbox>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" (click)="generate()">Generate</button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .full-width {
        width: 100%;
        margin-bottom: 8px;
      }

      mat-dialog-content {
        min-width: 320px;
      }
    `,
  ],
})
export class MealPlanGenerateDialogComponent {
  planName = '';
  seasonalOnly = false;

  private readonly dialogRef = inject(MatDialogRef<MealPlanGenerateDialogComponent>);

  generate(): void {
    const request: GenerateMealPlanRequest = {};
    if (this.planName.trim()) {
      request.name = this.planName.trim();
    }
    if (this.seasonalOnly) {
      request.seasonalOnly = true;
    }
    this.dialogRef.close(request);
  }
}
