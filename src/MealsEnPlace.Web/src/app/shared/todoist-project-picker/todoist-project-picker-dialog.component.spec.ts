import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { TodoistProjectHistoryResponse } from '../../core/models/todoist.models';
import { SettingsService } from '../../core/services/settings.service';
import {
  TodoistProjectPickerDialogComponent,
  TodoistProjectPickerDialogData,
} from './todoist-project-picker-dialog.component';

describe('TodoistProjectPickerDialogComponent', () => {
  let fixture: ComponentFixture<TodoistProjectPickerDialogComponent>;
  let component: TodoistProjectPickerDialogComponent;
  let settingsServiceMock: { getProjectHistory: ReturnType<typeof vi.fn> };
  let dialogRefMock: { close: ReturnType<typeof vi.fn> };

  // ── Shared response fixtures ────────────────────────────────────────────────

  const INBOX_ONLY_RESPONSE: TodoistProjectHistoryResponse = {
    lastUsedMealPlanProjectId: null,
    lastUsedShoppingListProjectId: null,
    nameResolutionError: null,
    namesResolved: true,
    projects: [{ displayName: 'Inbox (default)', isInbox: true, projectId: null }],
  };

  /** Two projects plus Inbox; meal-plan remembers '9988776655', shopping-list '2331547980'. */
  const TWO_PROJECT_RESPONSE: TodoistProjectHistoryResponse = {
    lastUsedMealPlanProjectId: '9988776655',
    lastUsedShoppingListProjectId: '2331547980',
    nameResolutionError: null,
    namesResolved: true,
    projects: [
      { displayName: 'Inbox (default)', isInbox: true, projectId: null },
      { displayName: 'Groceries', isInbox: false, projectId: '2331547980' },
      { displayName: 'Meals', isInbox: false, projectId: '9988776655' },
    ],
  };

  /** Names could not be resolved — projectId values shown raw. */
  const UNRESOLVED_NAMES_RESPONSE: TodoistProjectHistoryResponse = {
    lastUsedMealPlanProjectId: null,
    lastUsedShoppingListProjectId: null,
    nameResolutionError: 'Todoist API unreachable',
    namesResolved: false,
    projects: [
      { displayName: 'Inbox (default)', isInbox: true, projectId: null },
      { displayName: null, isInbox: false, projectId: '2331547980' },
    ],
  };

  /** last-used IDs point to a project no longer in the list — stale reference. */
  const STALE_PROJECT_RESPONSE: TodoistProjectHistoryResponse = {
    lastUsedMealPlanProjectId: 'stale-id-no-longer-exists',
    lastUsedShoppingListProjectId: 'stale-id-no-longer-exists',
    nameResolutionError: null,
    namesResolved: true,
    projects: [{ displayName: 'Inbox (default)', isInbox: true, projectId: null }],
  };

  // ── Helpers ─────────────────────────────────────────────────────────────────

  function createComponent(data: TodoistProjectPickerDialogData): void {
    TestBed.configureTestingModule({
      imports: [TodoistProjectPickerDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefMock },
        { provide: SettingsService, useValue: settingsServiceMock },
      ],
    });

    fixture = TestBed.createComponent(TodoistProjectPickerDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  function selectedProjectId(): string | null {
    return (component as unknown as { selectedProjectId: () => string | null }).selectedProjectId();
  }

  beforeEach(() => {
    settingsServiceMock = { getProjectHistory: vi.fn() };
    dialogRefMock = { close: vi.fn() };
  });

  // ── Dismiss performs no push ─────────────────────────────────────────────────

  describe('dismiss — no push', () => {
    it('closes the dialog with undefined when Cancel is clicked', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(INBOX_ONLY_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      const cancelButton = fixture.nativeElement.querySelector(
        'button[mat-button]',
      ) as HTMLButtonElement;
      cancelButton.click();

      expect(dialogRefMock.close).toHaveBeenCalledWith(undefined);
    });

    it('does not call close at all before the user interacts', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(INBOX_ONLY_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      expect(dialogRefMock.close).not.toHaveBeenCalled();
    });
  });

  // ── Degraded namesResolved: false state ─────────────────────────────────────

  describe('degraded names state', () => {
    it('shows the names-hint banner when namesResolved is false', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(UNRESOLVED_NAMES_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      fixture.detectChanges();
      const hint = fixture.nativeElement.querySelector('.names-hint') as HTMLElement | null;
      expect(hint).not.toBeNull();
    });

    it('Push button is enabled — the degraded state is a hint, not a blocker', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(UNRESOLVED_NAMES_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      fixture.detectChanges();
      const pushButton = fixture.nativeElement.querySelector(
        'button[mat-flat-button]',
      ) as HTMLButtonElement;
      expect(pushButton.disabled).toBe(false);
    });

    it('falls back to Inbox-only list and shows names-hint when the history fetch fails entirely', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(throwError(() => new Error('network')));
      createComponent({ resourceType: 'shoppingList' });

      fixture.detectChanges();
      const hint = fixture.nativeElement.querySelector('.names-hint') as HTMLElement | null;
      expect(hint).not.toBeNull();

      const pushButton = fixture.nativeElement.querySelector(
        'button[mat-flat-button]',
      ) as HTMLButtonElement;
      expect(pushButton.disabled).toBe(false);
    });
  });

  // ── Per-resource-type pre-selection ─────────────────────────────────────────

  describe('per-resource-type pre-selection', () => {
    it('pre-selects lastUsedShoppingListProjectId when resourceType is shoppingList', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(TWO_PROJECT_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      fixture.detectChanges();
      // TWO_PROJECT_RESPONSE.lastUsedShoppingListProjectId === '2331547980'
      expect(selectedProjectId()).toBe('2331547980');
    });

    it('pre-selects lastUsedMealPlanProjectId when resourceType is mealPlan', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(TWO_PROJECT_RESPONSE));
      createComponent({ resourceType: 'mealPlan' });

      fixture.detectChanges();
      // TWO_PROJECT_RESPONSE.lastUsedMealPlanProjectId === '9988776655'
      expect(selectedProjectId()).toBe('9988776655');
    });

    it('the two surfaces pre-select independently — shoppingList and mealPlan pick different IDs from the same response', () => {
      // The same history response carries separate per-surface last-used IDs.
      // Verify that the dialog honours the resourceType it was given.
      settingsServiceMock.getProjectHistory.mockReturnValue(of(TWO_PROJECT_RESPONSE));

      // Shopping-list surface
      createComponent({ resourceType: 'shoppingList' });
      fixture.detectChanges();
      const shoppingListSelection = selectedProjectId();

      // Tear down, set up meal-plan surface
      TestBed.resetTestingModule();
      settingsServiceMock = { getProjectHistory: vi.fn() };
      dialogRefMock = { close: vi.fn() };
      settingsServiceMock.getProjectHistory.mockReturnValue(of(TWO_PROJECT_RESPONSE));
      createComponent({ resourceType: 'mealPlan' });
      fixture.detectChanges();
      const mealPlanSelection = selectedProjectId();

      expect(shoppingListSelection).toBe('2331547980');
      expect(mealPlanSelection).toBe('9988776655');
      expect(shoppingListSelection).not.toBe(mealPlanSelection);
    });

    it('defaults to Inbox (null) when lastUsedShoppingListProjectId is null', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(INBOX_ONLY_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      fixture.detectChanges();
      expect(selectedProjectId()).toBeNull();
    });

    it('defaults to Inbox (null) when lastUsedMealPlanProjectId is null', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(INBOX_ONLY_RESPONSE));
      createComponent({ resourceType: 'mealPlan' });

      fixture.detectChanges();
      expect(selectedProjectId()).toBeNull();
    });
  });

  // ── Stale remembered-project fallback ───────────────────────────────────────

  describe('stale project fallback', () => {
    it('falls back to Inbox when lastUsedShoppingListProjectId is not in the returned project list', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(STALE_PROJECT_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      fixture.detectChanges();
      expect(selectedProjectId()).toBeNull();
    });

    it('falls back to Inbox when lastUsedMealPlanProjectId is not in the returned project list', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(STALE_PROJECT_RESPONSE));
      createComponent({ resourceType: 'mealPlan' });

      fixture.detectChanges();
      expect(selectedProjectId()).toBeNull();
    });
  });

  // ── Confirm path ─────────────────────────────────────────────────────────────

  describe('confirm path', () => {
    it('closes with the pre-selected project ID when Push is clicked (shopping-list surface)', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(TWO_PROJECT_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      fixture.detectChanges();
      const pushButton = fixture.nativeElement.querySelector(
        'button[mat-flat-button]',
      ) as HTMLButtonElement;
      pushButton.click();

      expect(dialogRefMock.close).toHaveBeenCalledWith({ projectId: '2331547980' });
    });

    it('closes with projectId: null when Inbox is the selection and Push is clicked', () => {
      settingsServiceMock.getProjectHistory.mockReturnValue(of(INBOX_ONLY_RESPONSE));
      createComponent({ resourceType: 'shoppingList' });

      fixture.detectChanges();
      const pushButton = fixture.nativeElement.querySelector(
        'button[mat-flat-button]',
      ) as HTMLButtonElement;
      pushButton.click();

      expect(dialogRefMock.close).toHaveBeenCalledWith({ projectId: null });
    });
  });
});
