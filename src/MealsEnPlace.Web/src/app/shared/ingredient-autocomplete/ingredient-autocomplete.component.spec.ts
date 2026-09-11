import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, Subject, throwError } from 'rxjs';
import { CanonicalIngredientDto } from '../../core/models/inventory.models';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { IngredientAutocompleteComponent } from './ingredient-autocomplete.component';

describe('IngredientAutocompleteComponent', () => {
  const INGREDIENTS: CanonicalIngredientDto[] = [
    { category: 'Produce', defaultUnitOfMeasureId: 'uom-g', id: 'ing-1', name: 'Chicken Breast' },
    { category: 'Produce', defaultUnitOfMeasureId: 'uom-g', id: 'ing-2', name: 'Chicken Thigh' },
  ];

  let component: IngredientAutocompleteComponent;
  let fixture: ComponentFixture<IngredientAutocompleteComponent>;
  let referenceDataServiceMock: {
    createIngredient: ReturnType<typeof vi.fn>;
    searchIngredients: ReturnType<typeof vi.fn>;
  };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };

  /** Access protected/private internals for testing. */
  interface Internals {
    debouncedQuery: () => string;
    displayText: () => string;
    ingredientQuery: () => string;
    ingredientResults: {
      isLoading: () => boolean;
      value: () => CanonicalIngredientDto[];
    };
    onChange: (v: CanonicalIngredientDto | null) => void;
    selectedIngredient: () => CanonicalIngredientDto | null;
    showCreateNew: () => boolean;
  }

  function internals(): Internals {
    return component as unknown as Internals;
  }

  /**
   * Simulates a user keystroke and flushes all Angular reactive effects so the
   * signal value flows into the toObservable pipeline synchronously.
   *
   * Sequence:
   *  1. onTextInput() updates ingredientQuery signal.
   *  2. TestBed.tick() runs pending root effects (toObservable's watcher), which
   *     emits the new value into the RxJS pipeline and starts debounceTime's timer.
   */
  function type(text: string): void {
    component['onTextInput'](text);
    TestBed.tick();
  }

  /**
   * Advances the fake clock by `ms` milliseconds and flushes Angular reactive
   * effects so signal changes from timer callbacks propagate to rxResource.
   *
   * Requires fake timers to be active (see the outer beforeEach below).
   */
  async function advanceTime(ms: number): Promise<void> {
    await vi.advanceTimersByTimeAsync(ms);
    TestBed.tick();
  }

  // ── Setup ─────────────────────────────────────────────────────────────────
  //
  // IMPORTANT: fake timers are installed BEFORE component creation so that the
  // initial toObservable effect (which fires during fixture.detectChanges and
  // emits '' to debounceTime) schedules its timer via the fake clock, not the
  // real setInterval.  If fake timers were installed after createComponent,
  // that initial timer would be a real timer; debounceTime would see
  // activeTask != null for all subsequent type() calls and never schedule a
  // fake timer, causing vi.advanceTimersByTimeAsync to do nothing useful.
  //
  // The initial '' emission:
  //   • causes debounceTime to schedule a fake timer at t=250ms
  //   • when that timer fires, distinctUntilChanged suppresses it if debouncedQuery
  //     is already '' (the initialValue), so no false search is triggered
  //   • subsequent type() calls update lastValue/lastTime inside debounceTime; when
  //     the timer eventually fires, it emits the most recent typed value

  beforeEach(() => {
    // Fake only the timer APIs that RxJS's asyncScheduler uses (setTimeout,
    // setInterval, Date) and leave queueMicrotask and process.nextTick real.
    // Angular's reactive scheduler (effects, rxResource) queues work via
    // queueMicrotask; if that is also faked, TestBed.tick() after an observable
    // emission cannot propagate signal changes until fake time is advanced again.
    vi.useFakeTimers({
      now: 0,
      toFake: ['Date', 'clearInterval', 'clearTimeout', 'setInterval', 'setTimeout'],
    });

    referenceDataServiceMock = {
      createIngredient: vi.fn(),
      searchIngredients: vi.fn().mockReturnValue(of([])),
    };
    snackBarMock = { open: vi.fn() };

    TestBed.configureTestingModule({
      imports: [IngredientAutocompleteComponent, NoopAnimationsModule, ReactiveFormsModule],
      providers: [
        { provide: ReferenceDataService, useValue: referenceDataServiceMock },
        { provide: MatSnackBar, useValue: snackBarMock },
      ],
    });

    fixture = TestBed.createComponent(IngredientAutocompleteComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ── Debounce behaviour ────────────────────────────────────────────────────
  //
  // Pattern:
  //   type(text)        → sets ingredientQuery signal and flushes toObservable effect
  //   advanceTime(N)    → advances fake clock, fires debounce timer, flushes rxResource

  describe('debounce', () => {
    it('does not search for a single character', async () => {
      type('c');
      await advanceTime(300);

      expect(referenceDataServiceMock.searchIngredients).not.toHaveBeenCalled();
    });

    it('searches after 250 ms for a 2+ character query', async () => {
      referenceDataServiceMock.searchIngredients.mockReturnValue(of(INGREDIENTS));

      type('ch');
      await advanceTime(249);
      expect(referenceDataServiceMock.searchIngredients).not.toHaveBeenCalled();

      await advanceTime(1);

      expect(referenceDataServiceMock.searchIngredients).toHaveBeenCalledWith('ch');
    });

    it('resets the debounce timer when the user types again within the window', async () => {
      referenceDataServiceMock.searchIngredients.mockReturnValue(of(INGREDIENTS));

      type('ch');
      await advanceTime(200);
      type('chi');
      await advanceTime(249);
      expect(referenceDataServiceMock.searchIngredients).not.toHaveBeenCalled();

      await advanceTime(1);

      expect(referenceDataServiceMock.searchIngredients).toHaveBeenCalledTimes(1);
      expect(referenceDataServiceMock.searchIngredients).toHaveBeenCalledWith('chi');
    });

    it('does not fire a second search when the identical debounced term is re-emitted', async () => {
      // Scenario: Angular dialog debounces ingredient search input (distinctUntilChanged clause)
      //
      // type 'ch' → debounce fires → search #1
      // delete to 'c' → retype 'ch' within new debounce window → debounce fires with 'ch'
      //   → distinctUntilChanged suppresses because 'ch' was already the last emitted value
      // Expected: search called exactly once.
      referenceDataServiceMock.searchIngredients.mockReturnValue(of(INGREDIENTS));

      // First debounce cycle: search fires.
      type('ch');
      await advanceTime(250);
      expect(referenceDataServiceMock.searchIngredients).toHaveBeenCalledTimes(1);

      // Delete back to 'c', then retype 'ch' — both within a new 250 ms window.
      type('c');
      await advanceTime(100);
      type('ch');
      await advanceTime(250);

      // distinctUntilChanged sees 'ch' again — suppresses it — still only one call.
      expect(referenceDataServiceMock.searchIngredients).toHaveBeenCalledTimes(1);
    });
  });

  // ── Minimum character threshold ───────────────────────────────────────────

  describe('minimum character threshold', () => {
    it('sends no request for an empty query', async () => {
      type('');
      await advanceTime(300);

      expect(referenceDataServiceMock.searchIngredients).not.toHaveBeenCalled();
    });

    it('sends no request for a 1-character query', async () => {
      type('c');
      await advanceTime(300);

      expect(referenceDataServiceMock.searchIngredients).not.toHaveBeenCalled();
    });

    it('sends a request once the query reaches 2 characters', async () => {
      referenceDataServiceMock.searchIngredients.mockReturnValue(of(INGREDIENTS));

      type('ch');
      await advanceTime(300);

      expect(referenceDataServiceMock.searchIngredients).toHaveBeenCalledWith('ch');
    });
  });

  // ── Stale request cancellation ────────────────────────────────────────────

  describe('stale request cancellation', () => {
    it('only shows results from the most recent query when the first arrives late', async () => {
      const firstSubject = new Subject<CanonicalIngredientDto[]>();
      const secondSubject = new Subject<CanonicalIngredientDto[]>();
      referenceDataServiceMock.searchIngredients
        .mockReturnValueOnce(firstSubject.asObservable())
        .mockReturnValueOnce(secondSubject.asObservable());

      // First query fires after debounce
      type('chi');
      await advanceTime(250);

      // Second query fires before first resolves
      type('chic');
      await advanceTime(250);

      expect(referenceDataServiceMock.searchIngredients).toHaveBeenCalledTimes(2);

      // Second resolves — should be shown.
      // await Promise.resolve() lets rxResource's microtask-scheduled value
      // update run before TestBed.tick() propagates it to computed signals.
      secondSubject.next(INGREDIENTS);
      secondSubject.complete();
      await Promise.resolve();
      TestBed.tick();

      // First resolves late — rxResource must ignore it (already cancelled)
      firstSubject.next([]);
      firstSubject.complete();
      await Promise.resolve();
      TestBed.tick();

      expect(internals().ingredientResults.value()).toEqual(INGREDIENTS);
    });
  });

  // ── Loading indicator ─────────────────────────────────────────────────────

  describe('loading indicator', () => {
    it('shows loading while the search is in flight for a 2+ char query', async () => {
      const subject = new Subject<CanonicalIngredientDto[]>();
      referenceDataServiceMock.searchIngredients.mockReturnValue(subject.asObservable());

      type('chi');
      await advanceTime(250);

      expect(internals().ingredientResults.isLoading()).toBe(true);

      // await Promise.resolve() lets rxResource's microtask-scheduled state
      // update run before TestBed.tick() propagates it to isLoading().
      subject.next(INGREDIENTS);
      subject.complete();
      await Promise.resolve();
      TestBed.tick();

      expect(internals().ingredientResults.isLoading()).toBe(false);
    });
  });

  // ── create-new option ─────────────────────────────────────────────────────

  describe('showCreateNew', () => {
    it('is false for a query shorter than 2 characters', async () => {
      type('c');
      await advanceTime(300);

      expect(internals().showCreateNew()).toBe(false);
    });

    it('is false while loading', async () => {
      const subject = new Subject<CanonicalIngredientDto[]>();
      referenceDataServiceMock.searchIngredients.mockReturnValue(subject.asObservable());

      type('Dragon Fruit');
      await advanceTime(250);

      expect(internals().ingredientResults.isLoading()).toBe(true);
      expect(internals().showCreateNew()).toBe(false);
    });

    it('is true when the debounced query has no exact match in results', async () => {
      referenceDataServiceMock.searchIngredients.mockReturnValue(of(INGREDIENTS));

      type('Dragon Fruit');
      await advanceTime(250);

      expect(internals().showCreateNew()).toBe(true);
    });

    it('is false when the debounced query exactly matches a result (case-insensitive)', async () => {
      referenceDataServiceMock.searchIngredients.mockReturnValue(of(INGREDIENTS));

      type('chicken breast');
      await advanceTime(250);

      expect(internals().showCreateNew()).toBe(false);
    });

    it('reflects the most recent debounced query, not an intermediate keystroke', async () => {
      referenceDataServiceMock.searchIngredients.mockReturnValue(of([]));

      type('Dragon F');
      await advanceTime(100);
      type('Dragon Fruit');
      await advanceTime(250);

      expect(internals().debouncedQuery()).toBe('Dragon Fruit');
    });
  });

  // ── Edit-mode pre-fill ────────────────────────────────────────────────────

  describe('writeValue (edit mode)', () => {
    it('pre-fills the display text with the ingredient name', () => {
      component.writeValue(INGREDIENTS[0]);
      fixture.detectChanges();

      expect(internals().displayText()).toBe('Chicken Breast');
    });

    it('does not fire a search request on writeValue', async () => {
      component.writeValue(INGREDIENTS[0]);
      TestBed.tick();
      await advanceTime(300);

      expect(referenceDataServiceMock.searchIngredients).not.toHaveBeenCalled();
    });

    it('clears the display when writeValue is called with null', () => {
      component.writeValue(INGREDIENTS[0]);
      component.writeValue(null);
      fixture.detectChanges();

      expect(internals().displayText()).toBe('');
    });

    it('stores the full DTO including the ID so the form value is the selected ingredient', () => {
      // Scenario: Edit mode pre-fills ingredient name and selected ID (ID clause).
      // writeValue must retain the full CanonicalIngredientDto, not just the name.
      component.writeValue(INGREDIENTS[0]);

      expect(internals().selectedIngredient()?.id).toBe('ing-1');
      expect(internals().selectedIngredient()?.name).toBe('Chicken Breast');
    });
  });

  // ── Ingredient selection ──────────────────────────────────────────────────

  describe('ingredient selection', () => {
    it('notifies onChange with the selected CanonicalIngredientDto', () => {
      const onChange = vi.fn();
      component.registerOnChange(onChange);

      component['selectIngredient'](INGREDIENTS[0]);

      expect(onChange).toHaveBeenCalledWith(INGREDIENTS[0]);
    });

    it('sets the display text to the selected ingredient name', () => {
      component['selectIngredient'](INGREDIENTS[0]);

      expect(internals().displayText()).toBe('Chicken Breast');
    });

    it('clears the search query after a selection', () => {
      component['onTextInput']('chi');
      component['selectIngredient'](INGREDIENTS[0]);

      expect(internals().ingredientQuery()).toBe('');
    });

    it('notifies onChange with null exactly once when the user edits after a selection', () => {
      const onChange = vi.fn();
      component.registerOnChange(onChange);

      component['selectIngredient'](INGREDIENTS[0]);
      onChange.mockClear();

      component['onTextInput']('chi');

      expect(onChange).toHaveBeenCalledWith(null);
      expect(onChange).toHaveBeenCalledTimes(1);
    });

    it('does not fire a further search after the query is cleared on selection', async () => {
      // Scenario: Stale in-flight requests are cancelled (selection clears the query;
      // the resulting empty debounce must not trigger a searchIngredients call).
      referenceDataServiceMock.searchIngredients.mockReturnValue(of(INGREDIENTS));

      type('chi');
      await advanceTime(250);
      referenceDataServiceMock.searchIngredients.mockClear();

      // Select an ingredient — this sets ingredientQuery to '' internally.
      component['selectIngredient'](INGREDIENTS[0]);
      // Flush the toObservable watcher so '' is pushed into the debounce pipeline.
      TestBed.tick();
      // Advance past the debounce window: '' has length < 2, so rxResource returns
      // of([]) without ever calling searchIngredients.
      await advanceTime(300);

      expect(referenceDataServiceMock.searchIngredients).not.toHaveBeenCalled();
    });
  });

  // ── Blur / touch ─────────────────────────────────────────────────────────

  describe('onBlur', () => {
    it('calls onTouched when the input loses focus', () => {
      const onTouched = vi.fn();
      component.registerOnTouched(onTouched);

      component['onBlur']();

      expect(onTouched).toHaveBeenCalledTimes(1);
    });
  });

  // ── Search error resilience ───────────────────────────────────────────────

  describe('search error resilience', () => {
    it('leaves results empty and the component usable when searchIngredients errors', async () => {
      // The component must not propagate the error to the host — no unhandled rejection.
      // After the error, ingredientResults.value() returns the defaultValue ([]) and
      // showCreateNew() remains callable (component is still usable for new queries).
      referenceDataServiceMock.searchIngredients.mockReturnValue(
        throwError(() => new Error('Network failure')),
      );

      type('ch');
      await advanceTime(250);
      // Let rxResource's microtask-scheduled error-state update propagate.
      await Promise.resolve();
      TestBed.tick();

      expect(internals().ingredientResults.value()).toEqual([]);
      expect(() => internals().showCreateNew()).not.toThrow();
    });
  });

  // ── Create-new flow ───────────────────────────────────────────────────────

  describe('createIngredient', () => {
    it('blocks creation with a snackbar when defaultUnitOfMeasureId is null', () => {
      // defaultUnitOfMeasureId defaults to null — do not set it.
      component['startCreation']('Dragon Fruit');

      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Units of measure are still loading.',
        'Dismiss',
        { duration: 4000 },
      );
      expect(referenceDataServiceMock.createIngredient).not.toHaveBeenCalled();
    });

    it('calls createIngredient with Other category and the typed name', () => {
      fixture.componentRef.setInput('defaultUnitOfMeasureId', 'unit-abc');
      const created: CanonicalIngredientDto = {
        category: 'Other',
        defaultUnitOfMeasureId: 'unit-abc',
        id: 'ing-new',
        name: 'Dragon Fruit',
      };
      referenceDataServiceMock.createIngredient.mockReturnValue(of(created));

      component['startCreation']('Dragon Fruit');

      expect(referenceDataServiceMock.createIngredient).toHaveBeenCalledWith({
        category: 'Other',
        defaultUnitOfMeasureId: 'unit-abc',
        name: 'Dragon Fruit',
      });
    });

    it('posts the bound defaultUnitOfMeasureId when present', () => {
      fixture.componentRef.setInput('defaultUnitOfMeasureId', 'unit-xyz');
      const created: CanonicalIngredientDto = {
        category: 'Other',
        defaultUnitOfMeasureId: 'unit-xyz',
        id: 'ing-new',
        name: 'Papaya',
      };
      referenceDataServiceMock.createIngredient.mockReturnValue(of(created));

      component['startCreation']('Papaya');

      expect(referenceDataServiceMock.createIngredient).toHaveBeenCalledWith({
        category: 'Other',
        defaultUnitOfMeasureId: 'unit-xyz',
        name: 'Papaya',
      });
    });

    it('notifies onChange with the new ingredient after successful creation', () => {
      fixture.componentRef.setInput('defaultUnitOfMeasureId', 'unit-abc');
      const onChange = vi.fn();
      component.registerOnChange(onChange);
      const created: CanonicalIngredientDto = {
        category: 'Other',
        defaultUnitOfMeasureId: 'unit-abc',
        id: 'ing-new',
        name: 'Dragon Fruit',
      };
      referenceDataServiceMock.createIngredient.mockReturnValue(of(created));

      component['startCreation']('Dragon Fruit');

      expect(onChange).toHaveBeenCalledWith(created);
    });

    it('shows a snackbar and restores typed text when creation fails', () => {
      fixture.componentRef.setInput('defaultUnitOfMeasureId', 'unit-abc');
      referenceDataServiceMock.createIngredient.mockReturnValue(
        throwError(() => ({ error: { message: 'Duplicate name.' } })),
      );

      component['startCreation']('Dragon Fruit');

      expect(snackBarMock.open).toHaveBeenCalledWith('Duplicate name.', 'Dismiss', {
        duration: 4000,
      });
      expect(internals().displayText()).toBe('Dragon Fruit');
    });

    it('falls back to a generic message when creation fails without a server message', () => {
      fixture.componentRef.setInput('defaultUnitOfMeasureId', 'unit-abc');
      referenceDataServiceMock.createIngredient.mockReturnValue(throwError(() => ({})));

      component['startCreation']('Dragon Fruit');

      expect(snackBarMock.open).toHaveBeenCalledWith('Failed to create ingredient.', 'Dismiss', {
        duration: 4000,
      });
    });
  });
});
