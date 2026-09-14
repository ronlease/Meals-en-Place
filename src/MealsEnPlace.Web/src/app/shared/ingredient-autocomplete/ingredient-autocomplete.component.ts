import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  computed,
  forwardRef,
  inject,
  input,
  ResourceRef,
  Signal,
  signal,
} from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import {
  MatAutocompleteModule,
  MatAutocompleteSelectedEvent,
} from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, debounceTime, distinctUntilChanged, of } from 'rxjs';
import { CanonicalIngredientDto } from '../../core/models/inventory.models';
import { ReferenceDataService } from '../../core/services/reference-data.service';

/** Sentinel value placed on the create-new mat-option so optionSelected can detect it. */
const CREATE_SENTINEL = '__NEW__';

/**
 * Debounced ingredient search autocomplete that implements ControlValueAccessor.
 * The form value is the selected CanonicalIngredientDto or null.
 *
 * Inputs:
 *   allowCreate           — when true, shows a "Create <name>" option for unmatched queries
 *                           (default false). Requires defaultUnitOfMeasureId to be bound.
 *   defaultUnitOfMeasureId — Guid of the unit to assign when creating a new ingredient.
 *                            Bind to units()[0]?.id from the parent form. Required when
 *                            allowCreate is true; creation is blocked with a snackbar if null.
 *   label                 — mat-label text (default 'Ingredient').
 *   placeholder           — input placeholder (default 'e.g. Diced Tomatoes').
 */
@Component({
  imports: [
    MatAutocompleteModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  providers: [
    {
      multi: true,
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => IngredientAutocompleteComponent),
    },
  ],
  selector: 'app-ingredient-autocomplete',
  standalone: true,
  styles: [
    `
      .full-width {
        width: 100%;
      }

      .category-hint {
        color: var(--mat-sys-on-surface-variant);
        font-size: 11px;
        margin-left: 8px;
      }

      .spinner-row {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 4px 0;
      }
    `,
  ],
  template: `
    <mat-form-field appearance="outline" class="full-width">
      <mat-label>{{ label() }}</mat-label>
      <input
        matInput
        [value]="displayText()"
        [matAutocomplete]="auto"
        [placeholder]="placeholder()"
        (blur)="onBlur()"
        (input)="onTextInput($any($event.target).value)"
      />
      <mat-autocomplete
        #auto="matAutocomplete"
        [displayWith]="displayIngredient"
        (optionSelected)="onOptionSelected($event)"
      >
        @if (debouncedQuery().trim().length >= 2 && ingredientResults.isLoading()) {
          <mat-option disabled>
            <div class="spinner-row">
              <mat-progress-spinner diameter="20" mode="indeterminate" />
              Searching...
            </div>
          </mat-option>
        }
        @for (ingredient of ingredientResults.value(); track ingredient.id) {
          <mat-option [value]="ingredient">
            {{ ingredient.name }}
            <span class="category-hint">{{ ingredient.category }}</span>
          </mat-option>
        }
        @if (allowCreate() && showCreateNew()) {
          <mat-option [value]="createSentinel">
            <mat-icon>add</mat-icon>
            Create "{{ debouncedQuery().trim() }}"
          </mat-option>
        }
      </mat-autocomplete>
    </mat-form-field>
  `,
})
export class IngredientAutocompleteComponent implements ControlValueAccessor {
  // ── Inputs ────────────────────────────────────────────────────────────────
  readonly allowCreate = input(false);
  readonly defaultUnitOfMeasureId = input<string | null>(null);
  readonly label = input('Ingredient');
  readonly placeholder = input('e.g. Diced Tomatoes');

  // ── Protected state ───────────────────────────────────────────────────────
  protected readonly createSentinel = CREATE_SENTINEL;
  protected readonly debouncedQuery: Signal<string>;
  protected readonly displayText = signal('');
  protected readonly ingredientQuery = signal('');
  protected readonly ingredientResults: ResourceRef<CanonicalIngredientDto[]>;
  protected readonly showCreateNew: Signal<boolean>;

  // ── Private ───────────────────────────────────────────────────────────────
  private onChange: (value: CanonicalIngredientDto | null) => void = () => undefined;
  private onTouched: () => void = () => undefined;
  private readonly referenceDataService = inject(ReferenceDataService);
  private readonly selectedIngredient = signal<CanonicalIngredientDto | null>(null);
  private readonly snackBar = inject(MatSnackBar);

  constructor() {
    this.debouncedQuery = toSignal(
      toObservable(this.ingredientQuery).pipe(debounceTime(250), distinctUntilChanged()),
      { initialValue: '' },
    );

    this.ingredientResults = rxResource({
      defaultValue: [] as CanonicalIngredientDto[],
      params: () => this.debouncedQuery().trim(),
      // catchError keeps the resource out of its error state: reading value()
      // on an errored resource throws inside the template and breaks change
      // detection, so a failed request degrades to an empty result set instead.
      stream: ({ params }) =>
        params.length < 2
          ? of([])
          : this.referenceDataService
              .searchIngredients(params)
              .pipe(catchError(() => of([] as CanonicalIngredientDto[]))),
    });

    this.showCreateNew = computed(() => {
      const query = this.debouncedQuery().trim();
      if (query.length < 2) return false;
      if (this.ingredientResults.isLoading()) return false;
      const results = this.ingredientResults.value() ?? [];
      return !results.some((i) => i.name.toLowerCase() === query.toLowerCase());
    });
  }

  // ── ControlValueAccessor ──────────────────────────────────────────────────

  registerOnChange(fn: (value: CanonicalIngredientDto | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  writeValue(value: CanonicalIngredientDto | null): void {
    this.selectedIngredient.set(value);
    this.displayText.set(value?.name ?? '');
    // Do NOT update ingredientQuery — writeValue is programmatic; no search should fire.
  }

  // ── Template event handlers ───────────────────────────────────────────────

  protected displayIngredient(value: CanonicalIngredientDto | string | null): string {
    if (!value || value === CREATE_SENTINEL) return '';
    if (typeof value === 'string') return value;
    return value.name;
  }

  protected onBlur(): void {
    this.onTouched();
  }

  protected onOptionSelected(event: MatAutocompleteSelectedEvent): void {
    const value = event.option.value;
    if (value === CREATE_SENTINEL) {
      this.startCreation(this.debouncedQuery().trim());
      return;
    }
    this.selectIngredient(value as CanonicalIngredientDto);
  }

  protected onTextInput(text: string): void {
    this.ingredientQuery.set(text);
    this.displayText.set(text);
    if (this.selectedIngredient()?.name !== text) {
      this.selectedIngredient.set(null);
      this.onChange(null);
    }
  }

  // ── Private helpers ───────────────────────────────────────────────────────

  private selectIngredient(ingredient: CanonicalIngredientDto): void {
    this.selectedIngredient.set(ingredient);
    this.displayText.set(ingredient.name);
    this.ingredientQuery.set('');
    this.onChange(ingredient);
  }

  private startCreation(name: string): void {
    if (!name) return;
    const defaultUnitOfMeasureId = this.defaultUnitOfMeasureId();
    if (!defaultUnitOfMeasureId) {
      this.snackBar.open('Units of measure are still loading.', 'Dismiss', { duration: 4000 });
      return;
    }
    this.referenceDataService
      .createIngredient({ category: 'Other', defaultUnitOfMeasureId, name })
      .subscribe({
        error: (err: HttpErrorResponse) => {
          this.snackBar.open(err.error?.message ?? 'Failed to create ingredient.', 'Dismiss', {
            duration: 4000,
          });
          // Restore the typed text so the user can try again.
          this.displayText.set(name);
        },
        next: (created) => {
          this.snackBar.open(`Created ingredient "${created.name}".`, undefined, {
            duration: 2500,
          });
          this.selectIngredient(created);
        },
      });
  }
}
