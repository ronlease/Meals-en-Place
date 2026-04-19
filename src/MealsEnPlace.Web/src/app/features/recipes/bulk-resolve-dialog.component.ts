import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { UnitOfMeasureDto } from '../../core/models/inventory.models';
import { RecipeService } from '../../core/services/recipe.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';

export interface BulkResolveDialogData {
  canonicalIngredientId: string;
  canonicalIngredientName: string;
  notes: string;
  occurrenceCount: number;
}

export interface BulkResolveDialogResult {
  affectedCount: number;
}

interface BulkResolveForm {
  quantity: FormControl<number | null>;
  unitOfMeasureId: FormControl<string>;
}

@Component({
  selector: 'app-bulk-resolve-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>Resolve {{ data.canonicalIngredientName }}</h2>

    <mat-dialog-content>
      <p class="phrase-label">
        Source phrase: <code>{{ data.notes }}</code>
      </p>

      <div class="impact-banner">
        <mat-icon>info</mat-icon>
        <span>
          This will update
          <strong>{{ data.occurrenceCount }}</strong>
          ingredient{{ data.occurrenceCount === 1 ? '' : 's' }} across every
          recipe that uses the phrase above.
        </span>
      </div>

      @if (unitsLoading()) {
        <div class="spinner-overlay">
          <mat-progress-spinner mode="indeterminate" diameter="32" />
        </div>
      } @else {
        <form [formGroup]="form" class="dialog-form" (ngSubmit)="submit()">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Declared net quantity</mat-label>
            <input
              matInput
              type="number"
              step="0.01"
              min="0.01"
              formControlName="quantity"
              autocomplete="off"
            />
            @if (form.controls.quantity.hasError('required') && form.controls.quantity.touched) {
              <mat-error>Quantity is required</mat-error>
            }
            @if (form.controls.quantity.hasError('min') && form.controls.quantity.touched) {
              <mat-error>Quantity must be greater than zero</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Unit of measure</mat-label>
            <mat-select formControlName="unitOfMeasureId">
              @for (unit of units(); track unit.id) {
                <mat-option [value]="unit.id">
                  {{ unit.name }} ({{ unit.abbreviation }})
                </mat-option>
              }
            </mat-select>
            @if (form.controls.unitOfMeasureId.hasError('required') && form.controls.unitOfMeasureId.touched) {
              <mat-error>Select a unit</mat-error>
            }
          </mat-form-field>

          @if (errorMessage()) {
            <div class="error-banner">
              <mat-icon>error_outline</mat-icon>
              <span>{{ errorMessage() }}</span>
            </div>
          }
        </form>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="cancel()" [disabled]="submitting()">
        Cancel
      </button>
      <button
        mat-flat-button
        color="primary"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid || submitting() || unitsLoading()"
      >
        @if (submitting()) {
          <mat-progress-spinner mode="indeterminate" diameter="20" />
        } @else {
          Apply to {{ data.occurrenceCount }} ingredient{{ data.occurrenceCount === 1 ? '' : 's' }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .phrase-label {
        margin: 0 0 1rem 0;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
      }

      .phrase-label code {
        font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
        padding: 0.125rem 0.5rem;
        background: var(--mat-sys-surface-container, #f5f5f5);
        border-radius: 4px;
      }

      .impact-banner {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.75rem 1rem;
        margin-bottom: 1.25rem;
        background: var(--mat-sys-primary-container, #d0e4ff);
        color: var(--mat-sys-on-primary-container, #001e3c);
        border-radius: 8px;
      }

      .dialog-form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }

      .full-width {
        width: 100%;
      }

      .spinner-overlay {
        display: flex;
        justify-content: center;
        padding: 2rem 0;
      }

      .error-banner {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.75rem 1rem;
        background: var(--mat-sys-error-container, #ffdad6);
        color: var(--mat-sys-on-error-container, #410002);
        border-radius: 8px;
      }
    `,
  ],
})
export class BulkResolveDialogComponent implements OnInit {
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly form: FormGroup<BulkResolveForm>;
  protected readonly submitting = signal(false);
  protected readonly units = signal<UnitOfMeasureDto[]>([]);
  protected readonly unitsLoading = signal(true);

  protected readonly data = inject<BulkResolveDialogData>(MAT_DIALOG_DATA);

  private readonly dialogRef = inject(
    MatDialogRef<BulkResolveDialogComponent, BulkResolveDialogResult | undefined>
  );
  private readonly recipeService = inject(RecipeService);
  private readonly referenceDataService = inject(ReferenceDataService);

  constructor() {
    this.form = new FormGroup<BulkResolveForm>({
      quantity: new FormControl<number | null>(null, {
        validators: [Validators.required, Validators.min(0.01)],
      }),
      unitOfMeasureId: new FormControl<string>('', {
        nonNullable: true,
        validators: [Validators.required],
      }),
    });
  }

  cancel(): void {
    this.dialogRef.close(undefined);
  }

  ngOnInit(): void {
    this.referenceDataService.getUnits().subscribe({
      error: () => {
        this.unitsLoading.set(false);
        this.errorMessage.set('Failed to load units of measure. Close and retry.');
      },
      next: (units) => {
        this.units.set(units);
        this.unitsLoading.set(false);
      },
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const quantity = this.form.controls.quantity.value;
    const unitOfMeasureId = this.form.controls.unitOfMeasureId.value;
    if (quantity === null || !unitOfMeasureId) {
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.recipeService
      .bulkResolveGroup({
        canonicalIngredientId: this.data.canonicalIngredientId,
        notes: this.data.notes,
        quantity,
        unitOfMeasureId,
      })
      .subscribe({
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(
            err.error?.detail ?? 'Failed to apply the resolution. Please try again.'
          );
        },
        next: (response) => {
          this.submitting.set(false);
          this.dialogRef.close({ affectedCount: response.affectedCount });
        },
      });
  }
}
