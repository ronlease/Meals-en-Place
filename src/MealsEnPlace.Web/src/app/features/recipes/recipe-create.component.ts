import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { CanonicalIngredientDto, UnitOfMeasureDto } from '../../core/models/inventory.models';
import { CreateRecipeRequest } from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';

@Component({
  selector: 'app-recipe-create',
  standalone: true,
  imports: [
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Create Recipe</h1>
    </div>

    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="recipe-form">
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Title</mat-label>
        <input matInput formControlName="title" required />
      </mat-form-field>

      <div class="row">
        <mat-form-field appearance="outline">
          <mat-label>Cuisine</mat-label>
          <input matInput formControlName="cuisineType" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Servings</mat-label>
          <input matInput type="number" formControlName="servingCount" min="1" />
        </mat-form-field>
      </div>

      <h3>Ingredients</h3>

      @for (ig of ingredientControls.controls; track $index; let i = $index) {
        <div class="ingredient-row" [formGroup]="asFormGroup(ig)">
          <mat-form-field appearance="outline" class="ingredient-field">
            <mat-label>Ingredient</mat-label>
            <mat-select formControlName="canonicalIngredientId" required>
              @for (ing of ingredients(); track ing.id) {
                <mat-option [value]="ing.id">{{ ing.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="qty-field">
            <mat-label>Qty</mat-label>
            <input matInput type="number" formControlName="quantity" min="0" step="0.1" required />
          </mat-form-field>

          <mat-form-field appearance="outline" class="uom-field">
            <mat-label>Unit</mat-label>
            <mat-select formControlName="uomId">
              <mat-option [value]="null">None</mat-option>
              @for (u of units(); track u.id) {
                <mat-option [value]="u.id">{{ u.abbreviation }} ({{ u.name }})</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="notes-field">
            <mat-label>Notes</mat-label>
            <input matInput formControlName="notes" placeholder="e.g., 1 can" />
          </mat-form-field>

          <button mat-icon-button type="button" (click)="removeIngredient(i)" color="warn">
            <mat-icon>delete</mat-icon>
          </button>
        </div>
      }

      <button mat-stroked-button type="button" (click)="addIngredient()" class="add-btn">
        <mat-icon>add</mat-icon>
        Add Ingredient
      </button>

      <mat-form-field appearance="outline" class="full-width instructions-field">
        <mat-label>Instructions</mat-label>
        <textarea matInput formControlName="instructions" rows="6" required></textarea>
      </mat-form-field>

      <div class="form-actions">
        <button mat-button type="button" (click)="cancel()">Cancel</button>
        <button
          mat-flat-button
          color="primary"
          type="submit"
          [disabled]="saving() || form.invalid || ingredientControls.length === 0"
        >
          @if (saving()) {
            <mat-progress-spinner mode="indeterminate" diameter="18" />
          }
          Save Recipe
        </button>
      </div>
    </form>
  `,
  styles: [
    `
      :host {
        display: block;
        padding: 24px;
      }

      .page-header {
        margin-bottom: 16px;
      }

      .page-title {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }

      .recipe-form {
        max-width: 800px;
      }

      .full-width {
        width: 100%;
      }

      .row {
        display: flex;
        gap: 16px;

        mat-form-field {
          flex: 1;
        }
      }

      h3 {
        margin: 16px 0 8px;
        font-size: 16px;
        font-weight: 500;
      }

      .ingredient-row {
        display: flex;
        gap: 8px;
        align-items: flex-start;
        margin-bottom: 4px;
      }

      .ingredient-field {
        flex: 3;
      }

      .qty-field {
        flex: 1;
        min-width: 80px;
      }

      .uom-field {
        flex: 2;
      }

      .notes-field {
        flex: 2;
      }

      .add-btn {
        margin: 8px 0 16px;
      }

      .instructions-field {
        margin-top: 16px;
      }

      .form-actions {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
        margin-top: 16px;
      }
    `,
  ],
})
export class RecipeCreateComponent implements OnInit {
  protected readonly ingredients = signal<CanonicalIngredientDto[]>([]);
  protected readonly saving = signal(false);
  protected readonly units = signal<UnitOfMeasureDto[]>([]);

  private readonly fb = inject(FormBuilder);
  private readonly recipeService = inject(RecipeService);
  private readonly referenceDataService = inject(ReferenceDataService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  form = this.fb.group({
    cuisineType: [''],
    ingredients: this.fb.array([]),
    instructions: ['', Validators.required],
    servingCount: [4, [Validators.required, Validators.min(1)]],
    title: ['', Validators.required],
  });

  get ingredientControls(): FormArray {
    return this.form.get('ingredients') as FormArray;
  }

  addIngredient(): void {
    this.ingredientControls.push(
      this.fb.group({
        canonicalIngredientId: ['', Validators.required],
        notes: [''],
        quantity: [1, [Validators.required, Validators.min(0)]],
        uomId: [null as string | null],
      })
    );
  }

  asFormGroup(control: any): FormGroup {
    return control as FormGroup;
  }

  cancel(): void {
    this.router.navigate(['/recipes']);
  }

  ngOnInit(): void {
    this.referenceDataService.getIngredients().subscribe({
      next: (data) => this.ingredients.set(data),
    });
    this.referenceDataService.getUnits().subscribe({
      next: (data) => this.units.set(data),
    });
    this.addIngredient();
  }

  onSubmit(): void {
    if (this.form.invalid || this.ingredientControls.length === 0) return;

    this.saving.set(true);
    const val = this.form.value;
    const request: CreateRecipeRequest = {
      cuisineType: val.cuisineType || '',
      ingredients: (val.ingredients || []).map((i: any) => ({
        canonicalIngredientId: i.canonicalIngredientId,
        notes: i.notes || null,
        quantity: i.quantity,
        uomId: i.uomId || null,
      })),
      instructions: val.instructions || '',
      servingCount: val.servingCount || 4,
      title: val.title || '',
    };

    this.recipeService.createRecipe(request).subscribe({
      error: () => {
        this.saving.set(false);
        this.snackBar.open('Failed to create recipe.', 'Dismiss', { duration: 4000 });
      },
      next: (recipe) => {
        this.saving.set(false);
        this.snackBar.open(`Recipe "${recipe.title}" created.`, 'OK', { duration: 3000 });
        this.router.navigate(['/recipes']);
      },
    });
  }

  removeIngredient(index: number): void {
    this.ingredientControls.removeAt(index);
  }
}
