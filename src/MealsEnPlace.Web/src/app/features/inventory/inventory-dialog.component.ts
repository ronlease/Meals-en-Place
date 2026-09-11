import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  AddInventoryItemRequest,
  CanonicalIngredientDto,
  ContainerReferenceDetectedResponse,
  InventoryItemResponse,
  InventoryLocation,
  UnitOfMeasureDto,
} from '../../core/models/inventory.models';
import { InventoryService } from '../../core/services/inventory.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { IngredientAutocompleteComponent } from '../../shared/ingredient-autocomplete/ingredient-autocomplete.component';

export interface InventoryDialogData {
  item?: InventoryItemResponse;
  location: InventoryLocation;
  mode: 'add' | 'edit';
}

interface ContainerForm {
  declaredQuantity: FormControl<number | null>;
  declaredUnitOfMeasureId: FormControl<string>;
}

interface InventoryForm {
  canonicalIngredient: FormControl<CanonicalIngredientDto | null>;
  expiryDate: FormControl<Date | null>;
  location: FormControl<InventoryLocation>;
  notes: FormControl<string>;
  quantity: FormControl<number | null>;
  unitOfMeasureId: FormControl<string>;
}

@Component({
  selector: 'app-inventory-dialog',
  standalone: true,
  imports: [
    IngredientAutocompleteComponent,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.mode === 'add' ? 'Add Item' : 'Edit Item' }}</h2>

    <mat-dialog-content>
      @if (referenceDataLoading()) {
        <div class="spinner-overlay">
          <mat-progress-spinner mode="indeterminate" diameter="40" />
        </div>
      } @else {
        <form [formGroup]="inventoryForm" class="dialog-form">
          <app-ingredient-autocomplete
            formControlName="canonicalIngredient"
            [allowCreate]="true"
            [defaultUnitOfMeasureId]="units().length ? units()[0].id : null"
          />
          @if (
            inventoryForm.controls.canonicalIngredient.touched &&
            inventoryForm.controls.canonicalIngredient.value === null &&
            !inventoryForm.controls.canonicalIngredient.dirty
          ) {
            <mat-error class="field-error">Ingredient is required</mat-error>
          }
          @if (ingredientNotResolved() && inventoryForm.controls.canonicalIngredient.touched) {
            <mat-error class="field-error"
              >Select an ingredient from the list or create a new one</mat-error
            >
          }

          <div class="row-fields">
            <mat-form-field appearance="outline" class="quantity-field">
              <mat-label>Quantity</mat-label>
              <input matInput type="number" formControlName="quantity" min="0" step="any" />
              @if (inventoryForm.controls.quantity.hasError('required')) {
                <mat-error>Quantity is required</mat-error>
              }
              @if (inventoryForm.controls.quantity.hasError('min')) {
                <mat-error>Must be greater than 0</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="unit-of-measure-field">
              <mat-label>Unit of Measure</mat-label>
              <mat-select formControlName="unitOfMeasureId">
                @for (unit of units(); track unit.id) {
                  <mat-option [value]="unit.id">{{ unit.abbreviation }}</mat-option>
                }
              </mat-select>
              @if (inventoryForm.controls.unitOfMeasureId.hasError('required')) {
                <mat-error>Unit of measure is required</mat-error>
              }
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Location</mat-label>
            <mat-select formControlName="location">
              <mat-option value="Pantry">Pantry</mat-option>
              <mat-option value="Fridge">Fridge</mat-option>
              <mat-option value="Freezer">Freezer</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Expiry Date (optional)</mat-label>
            <input matInput [matDatepicker]="expiryPicker" formControlName="expiryDate" />
            <mat-datepicker-toggle matIconSuffix [for]="expiryPicker" />
            <mat-datepicker #expiryPicker />
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Notes (optional)</mat-label>
            <input matInput formControlName="notes" placeholder="e.g. 1 can of diced tomatoes" />
          </mat-form-field>
        </form>

        @if (containerReferenceDetected()) {
          <div class="container-reference-prompt">
            <mat-icon>info</mat-icon>
            <p>{{ containerReference()!.message }}</p>

            <form [formGroup]="containerForm" class="dialog-form">
              <div class="row-fields">
                <mat-form-field appearance="outline" class="quantity-field">
                  <mat-label>Net Quantity</mat-label>
                  <input
                    matInput
                    type="number"
                    formControlName="declaredQuantity"
                    min="0"
                    step="any"
                  />
                  @if (containerForm.controls.declaredQuantity.hasError('required')) {
                    <mat-error>Quantity is required</mat-error>
                  }
                </mat-form-field>

                <mat-form-field appearance="outline" class="unit-of-measure-field">
                  <mat-label>Unit of Measure</mat-label>
                  <mat-select formControlName="declaredUnitOfMeasureId">
                    @for (unit of units(); track unit.id) {
                      <mat-option [value]="unit.id">{{ unit.abbreviation }}</mat-option>
                    }
                  </mat-select>
                  @if (containerForm.controls.declaredUnitOfMeasureId.hasError('required')) {
                    <mat-error>Unit of measure is required</mat-error>
                  }
                </mat-form-field>
              </div>
            </form>
          </div>
        }

        @if (loading()) {
          <div class="spinner-overlay">
            <mat-progress-spinner mode="indeterminate" diameter="40" />
          </div>
        }
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="null" [disabled]="loading()">Cancel</button>
      <button
        mat-flat-button
        color="primary"
        (click)="onSubmit()"
        [disabled]="loading() || referenceDataLoading()"
      >
        {{ containerReferenceDetected() ? 'Declare & Save' : data.mode === 'add' ? 'Add' : 'Save' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .dialog-form {
        display: flex;
        flex-direction: column;
        gap: 4px;
        min-width: 420px;
      }

      .full-width {
        width: 100%;
      }

      .row-fields {
        display: flex;
        gap: 12px;
      }

      .quantity-field {
        flex: 1;
      }

      .unit-of-measure-field {
        flex: 1;
      }

      .field-error {
        font-size: 12px;
        color: var(--mat-sys-error);
        margin-top: -8px;
        margin-bottom: 4px;
      }

      .container-reference-prompt {
        border: 1px solid #f59e0b;
        border-radius: 8px;
        background: #fffbeb;
        padding: 12px 16px;
        margin-top: 8px;
        display: flex;
        flex-direction: column;
        gap: 8px;

        mat-icon {
          color: #f59e0b;
        }

        p {
          margin: 0;
          color: #92400e;
          font-size: 14px;
        }
      }

      .spinner-overlay {
        display: flex;
        justify-content: center;
        padding: 8px 0;
      }
    `,
  ],
})
export class InventoryDialogComponent implements OnInit {
  protected readonly containerForm: FormGroup<ContainerForm>;
  protected readonly containerReference = signal<ContainerReferenceDetectedResponse | null>(null);
  protected readonly containerReferenceDetected = signal(false);
  protected readonly data: InventoryDialogData = inject(MAT_DIALOG_DATA);
  protected readonly inventoryForm: FormGroup<InventoryForm>;
  protected readonly loading = signal(false);
  protected readonly referenceDataLoading = signal(true);
  protected readonly units = signal<UnitOfMeasureDto[]>([]);

  private readonly dialogRef = inject(MatDialogRef<InventoryDialogComponent>);
  private readonly inventoryService = inject(InventoryService);
  private readonly referenceDataService = inject(ReferenceDataService);
  private readonly snackBar = inject(MatSnackBar);

  constructor() {
    this.containerForm = new FormGroup<ContainerForm>({
      declaredQuantity: new FormControl<number | null>(null, [
        Validators.required,
        Validators.min(0.001),
      ]),
      declaredUnitOfMeasureId: new FormControl<string>('', {
        nonNullable: true,
        validators: [Validators.required],
      }),
    });

    this.inventoryForm = new FormGroup<InventoryForm>({
      canonicalIngredient: new FormControl<CanonicalIngredientDto | null>(null, {
        validators: [Validators.required],
      }),
      expiryDate: new FormControl<Date | null>(null),
      location: new FormControl<InventoryLocation>('Pantry', {
        nonNullable: true,
        validators: [Validators.required],
      }),
      notes: new FormControl<string>('', { nonNullable: true }),
      quantity: new FormControl<number | null>(null, [Validators.required, Validators.min(0.001)]),
      unitOfMeasureId: new FormControl<string>('', {
        nonNullable: true,
        validators: [Validators.required],
      }),
    });
  }

  // Returns true when the user typed text but did not pick an option from the list.
  // Used to surface "Select an ingredient…" and is mutually exclusive with "required":
  // required only fires when the control is untouched (never typed into).
  protected ingredientNotResolved(): boolean {
    const ctrl = this.inventoryForm.controls.canonicalIngredient;
    return ctrl.value === null && ctrl.dirty;
  }

  ngOnInit(): void {
    const { item, location, mode } = this.data;
    this.referenceDataService.getUnits().subscribe({
      error: () => {
        this.referenceDataLoading.set(false);
        this.snackBar.open('Failed to load units.', 'Dismiss', { duration: 4000 });
      },
      next: (units) => {
        this.units.set(units);
        this.referenceDataLoading.set(false);
        this.applyEditValues(mode, item, location);
      },
    });
  }

  onSubmit(): void {
    if (this.containerReferenceDetected()) {
      this.containerForm.markAllAsTouched();
      if (this.containerForm.invalid) return;
      this.submitWithContainerDeclaration();
      return;
    }

    this.inventoryForm.markAllAsTouched();
    if (this.inventoryForm.invalid) return;

    if (this.data.mode === 'add') {
      if (!this.inventoryForm.controls.canonicalIngredient.value) return;
      this.submitAdd();
    } else {
      this.submitEdit();
    }
  }

  private applyEditValues(
    mode: 'add' | 'edit',
    item: InventoryItemResponse | undefined,
    location: InventoryLocation,
  ): void {
    if (mode === 'edit' && item) {
      const expiry = item.expiryDate ? new Date(item.expiryDate) : null;
      // Construct a minimal CanonicalIngredientDto for writeValue so that the
      // autocomplete displays the ingredient name without firing a search.
      const ingredientDto: CanonicalIngredientDto = {
        category: '',
        defaultUnitOfMeasureId: item.unitOfMeasureId,
        id: item.canonicalIngredientId,
        name: item.canonicalIngredientName,
      };
      this.inventoryForm.setValue({
        canonicalIngredient: ingredientDto,
        expiryDate: expiry,
        location: item.location,
        notes: item.notes ?? '',
        quantity: item.quantity,
        unitOfMeasureId: item.unitOfMeasureId,
      });
    } else {
      this.inventoryForm.controls.location.setValue(location);
    }
  }

  private buildAddRequest(
    declaredQuantity?: number | null,
    declaredUnitOfMeasureId?: string | null,
  ): AddInventoryItemRequest {
    const v = this.inventoryForm.getRawValue();
    return {
      canonicalIngredientId: v.canonicalIngredient!.id,
      declaredQuantity: declaredQuantity ?? null,
      declaredUnitOfMeasureId: declaredUnitOfMeasureId ?? null,
      expiryDate: v.expiryDate ? v.expiryDate.toISOString().substring(0, 10) : null,
      location: v.location,
      notes: v.notes,
      quantity: v.quantity!,
      unitOfMeasureId: v.unitOfMeasureId,
    };
  }

  private submitAdd(): void {
    this.loading.set(true);
    const request = this.buildAddRequest();
    this.inventoryService.addItem(request).subscribe({
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to add item.', 'Dismiss', {
          duration: 4000,
        });
      },
      next: (response) => {
        this.loading.set(false);
        if ('detectedKeyword' in response) {
          this.containerReference.set(response as ContainerReferenceDetectedResponse);
          this.containerReferenceDetected.set(true);
        } else {
          this.snackBar.open('Item added.', undefined, { duration: 2500 });
          this.dialogRef.close(response as InventoryItemResponse);
        }
      },
    });
  }

  private submitEdit(): void {
    this.loading.set(true);
    const v = this.inventoryForm.getRawValue();
    this.inventoryService
      .updateItem(this.data.item!.id, {
        expiryDate: v.expiryDate ? v.expiryDate.toISOString().substring(0, 10) : null,
        location: v.location,
        notes: v.notes,
        quantity: v.quantity!,
        unitOfMeasureId: v.unitOfMeasureId,
      })
      .subscribe({
        error: (err: HttpErrorResponse) => {
          this.loading.set(false);
          this.snackBar.open(err.error?.message ?? 'Failed to update item.', 'Dismiss', {
            duration: 4000,
          });
        },
        next: (response) => {
          this.loading.set(false);
          this.snackBar.open('Item updated.', undefined, { duration: 2500 });
          this.dialogRef.close(response);
        },
      });
  }

  private submitWithContainerDeclaration(): void {
    this.loading.set(true);
    const cv = this.containerForm.getRawValue();
    const request = this.buildAddRequest(cv.declaredQuantity, cv.declaredUnitOfMeasureId);
    this.inventoryService.addItem(request).subscribe({
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to save item.', 'Dismiss', {
          duration: 4000,
        });
      },
      next: (response) => {
        this.loading.set(false);
        if ('detectedKeyword' in response) {
          this.snackBar.open('Container reference still detected. Please try again.', 'Dismiss', {
            duration: 4000,
          });
        } else {
          this.snackBar.open('Item added.', undefined, { duration: 2500 });
          this.dialogRef.close(response as InventoryItemResponse);
        }
      },
    });
  }
}
