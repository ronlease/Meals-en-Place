import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { By } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { Observable, of, throwError } from 'rxjs';
import { DisplaySystem } from '../../core/models/preferences.models';
import { AiAvailabilityService } from '../../core/services/ai-availability.service';
import { PreferencesService } from '../../core/services/preferences.service';
import { SettingsService } from '../../core/services/settings.service';
import { TodoistAvailabilityService } from '../../core/services/todoist-availability.service';
import { SettingsPageComponent } from './settings-page.component';

describe('SettingsPageComponent', () => {
  let aiConfigured: ReturnType<typeof signal<boolean>>;
  let aiRefresh: ReturnType<typeof vi.fn>;
  let aiSetConfigured: ReturnType<typeof vi.fn>;
  let autoDepleteOnConsume: ReturnType<typeof signal<boolean>>;
  let component: SettingsPageComponent;
  let dialogMock: { open: ReturnType<typeof vi.fn> };
  let displaySystem: ReturnType<typeof signal<DisplaySystem>>;
  let fixture: ComponentFixture<SettingsPageComponent>;
  let setAutoDepleteOnConsume: ReturnType<typeof vi.fn>;
  let settingsServiceMock: {
    clearToken: ReturnType<typeof vi.fn>;
    clearTodoistToken: ReturnType<typeof vi.fn>;
    saveToken: ReturnType<typeof vi.fn>;
    saveTodoistToken: ReturnType<typeof vi.fn>;
    testToken: ReturnType<typeof vi.fn>;
    testTodoistToken: ReturnType<typeof vi.fn>;
  };
  let snackBarMock: { open: ReturnType<typeof vi.fn> };
  let todoistConfigured: ReturnType<typeof signal<boolean>>;
  let todoistRefresh: ReturnType<typeof vi.fn>;
  let todoistSetConfigured: ReturnType<typeof vi.fn>;
  let toggleDisplaySystem: ReturnType<typeof vi.fn>;

  function dialogReturning(result: unknown): { afterClosed: () => Observable<unknown> } {
    return { afterClosed: () => of(result) };
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [SettingsPageComponent, NoopAnimationsModule],
      providers: [
        { provide: SettingsService, useValue: settingsServiceMock },
        {
          provide: AiAvailabilityService,
          useValue: {
            configured: aiConfigured,
            dismissBanner: vi.fn(),
            dismissed: signal(false),
            refresh: aiRefresh,
            setConfigured: aiSetConfigured,
          },
        },
        {
          provide: PreferencesService,
          useValue: {
            autoDepleteOnConsume,
            displaySystem,
            setAutoDepleteOnConsume,
            toggleDisplaySystem,
          },
        },
        {
          provide: TodoistAvailabilityService,
          useValue: {
            configured: todoistConfigured,
            refresh: todoistRefresh,
            setConfigured: todoistSetConfigured,
          },
        },
      ],
    });

    TestBed.overrideProvider(MatDialog, { useValue: dialogMock });
    TestBed.overrideProvider(MatSnackBar, { useValue: snackBarMock });

    fixture = TestBed.createComponent(SettingsPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  type TestResult = { message: string; success: boolean } | null;

  type Internals = {
    saving: () => boolean;
    testResult: () => TestResult;
    testing: () => boolean;
    todoistSaving: () => boolean;
    todoistTestResult: () => TestResult;
    todoistTesting: () => boolean;
    todoistTokenInput: { (): string; set: (value: string) => void };
    tokenInput: { (): string; set: (value: string) => void };
  };

  function internals(): Internals {
    return component as unknown as Internals;
  }

  function toggles(): MatSlideToggle[] {
    return fixture.debugElement
      .queryAll(By.directive(MatSlideToggle))
      .map((node) => node.componentInstance as MatSlideToggle);
  }

  /**
   * Clicks a slide toggle's underlying switch. Calling toggle() on the component
   * flips the state without emitting (change), which is the binding under test.
   */
  function clickToggle(index: number): void {
    const switches = fixture.nativeElement.querySelectorAll(
      'mat-slide-toggle button[role="switch"]',
    ) as NodeListOf<HTMLButtonElement>;
    switches[index].click();
    fixture.detectChanges();
  }

  beforeEach(() => {
    aiConfigured = signal(false);
    autoDepleteOnConsume = signal(false);
    displaySystem = signal<DisplaySystem>('Imperial');
    todoistConfigured = signal(false);

    aiRefresh = vi.fn();
    aiSetConfigured = vi.fn((value: boolean) => aiConfigured.set(value));
    setAutoDepleteOnConsume = vi.fn();
    todoistRefresh = vi.fn();
    todoistSetConfigured = vi.fn((value: boolean) => todoistConfigured.set(value));
    toggleDisplaySystem = vi.fn();

    dialogMock = { open: vi.fn().mockReturnValue(dialogReturning(false)) };
    snackBarMock = { open: vi.fn() };
    settingsServiceMock = {
      clearToken: vi.fn(),
      clearTodoistToken: vi.fn(),
      saveToken: vi.fn(),
      saveTodoistToken: vi.fn(),
      testToken: vi.fn(),
      testTodoistToken: vi.fn(),
    };
  });

  // ── Startup ─────────────────────────────────────────────────────────────────

  it('refreshes both availability signals on construction', () => {
    createComponent();

    expect(aiRefresh).toHaveBeenCalled();
    expect(todoistRefresh).toHaveBeenCalled();
  });

  // ── Preference toggles ──────────────────────────────────────────────────────

  describe('preference toggles', () => {
    it('shows the metric toggle off while the display system is Imperial', () => {
      createComponent();

      expect(toggles()[0].checked).toBe(false);
    });

    it('shows the metric toggle on once the display system is Metric', () => {
      displaySystem.set('Metric');

      createComponent();

      expect(toggles()[0].checked).toBe(true);
    });

    it('toggles the display system when that switch is changed', () => {
      createComponent();

      clickToggle(0);

      expect(toggleDisplaySystem).toHaveBeenCalled();
    });

    it('reflects the auto-deplete preference', () => {
      autoDepleteOnConsume.set(true);

      createComponent();

      expect(toggles()[1].checked).toBe(true);
    });

    it('passes the new auto-deplete value through when that switch is changed', () => {
      createComponent();

      clickToggle(1);

      expect(setAutoDepleteOnConsume).toHaveBeenCalledWith(true);
    });
  });

  // ── Claude key: save ────────────────────────────────────────────────────────

  describe('save', () => {
    it('does nothing for an empty input', () => {
      createComponent();

      component.save();

      expect(settingsServiceMock.saveToken).not.toHaveBeenCalled();
    });

    it('does nothing for a whitespace-only input', () => {
      createComponent();
      internals().tokenInput.set('   ');

      component.save();

      expect(settingsServiceMock.saveToken).not.toHaveBeenCalled();
    });

    it('trims the token before saving', () => {
      createComponent();
      internals().tokenInput.set('  sk-ant-secret  ');
      settingsServiceMock.saveToken.mockReturnValue(of({ configured: true }));

      component.save();

      expect(settingsServiceMock.saveToken).toHaveBeenCalledWith('sk-ant-secret');
    });

    it('marks AI available, clears the input, and confirms on success', () => {
      // Clearing the input matters: the key is write-only and must not linger
      // in the DOM after it is stored.
      createComponent();
      internals().tokenInput.set('sk-ant-secret');
      settingsServiceMock.saveToken.mockReturnValue(of({ configured: true }));

      component.save();

      expect(aiSetConfigured).toHaveBeenCalledWith(true);
      expect(internals().tokenInput()).toBe('');
      expect(internals().testResult()).toBeNull();
      expect(internals().saving()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith('API key saved.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('keeps the typed token and reports the failure', () => {
      createComponent();
      internals().tokenInput.set('sk-ant-secret');
      settingsServiceMock.saveToken.mockReturnValue(throwError(() => new Error('boom')));

      component.save();

      expect(internals().tokenInput()).toBe('sk-ant-secret');
      expect(internals().saving()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Could not save the token. See console for details.',
        'Dismiss',
        { duration: 5000 },
      );
    });
  });

  // ── Claude key: test ────────────────────────────────────────────────────────

  describe('test', () => {
    it('tests the stored key when nothing is typed', () => {
      createComponent();
      settingsServiceMock.testToken.mockReturnValue(of({ success: true }));

      component.test();

      expect(settingsServiceMock.testToken).toHaveBeenCalledWith(undefined);
    });

    it('tests the typed candidate rather than the stored key', () => {
      createComponent();
      internals().tokenInput.set('  sk-ant-candidate ');
      settingsServiceMock.testToken.mockReturnValue(of({ success: true }));

      component.test();

      expect(settingsServiceMock.testToken).toHaveBeenCalledWith('sk-ant-candidate');
    });

    it('reports acceptance on success', () => {
      createComponent();
      settingsServiceMock.testToken.mockReturnValue(of({ success: true }));

      component.test();
      fixture.detectChanges();

      expect(internals().testResult()?.success).toBe(true);
      expect(internals().testing()).toBe(false);
      expect(fixture.nativeElement.querySelector('.test-result.success').textContent).toContain(
        'Anthropic accepted the key.',
      );
    });

    it('shows the server error message on rejection', () => {
      createComponent();
      settingsServiceMock.testToken.mockReturnValue(
        of({ errorMessage: 'Invalid API key.', success: false }),
      );

      component.test();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.test-result.failure').textContent).toContain(
        'Invalid API key.',
      );
    });

    it('falls back to a generic message when a rejection carries none', () => {
      createComponent();
      settingsServiceMock.testToken.mockReturnValue(of({ success: false }));

      component.test();

      expect(internals().testResult()).toEqual({
        message: 'Unknown error.',
        success: false,
      });
    });

    it('reports a network failure as an unsuccessful test', () => {
      createComponent();
      settingsServiceMock.testToken.mockReturnValue(throwError(() => new Error('offline')));

      component.test();

      expect(internals().testResult()).toEqual({
        message: 'Network error contacting the server.',
        success: false,
      });
      expect(internals().testing()).toBe(false);
    });

    it('clears the previous result before retesting', () => {
      createComponent();
      settingsServiceMock.testToken.mockReturnValue(of({ success: true }));
      component.test();

      let capturedDuringCall: TestResult = { message: 'unset', success: false };
      settingsServiceMock.testToken.mockImplementation(() => {
        capturedDuringCall = internals().testResult();
        return of({ success: true });
      });
      component.test();

      expect(capturedDuringCall).toBeNull();
    });
  });

  // ── Claude key: remove ──────────────────────────────────────────────────────

  describe('remove', () => {
    it('asks for confirmation before deleting', () => {
      createComponent();

      component.remove();

      expect(dialogMock.open).toHaveBeenCalledWith(expect.anything(), {
        data: {
          confirmLabel: 'Remove key',
          message:
            'The stored Anthropic API key will be deleted. AI-backed features will disable until a new key is saved.',
          title: 'Remove Claude API key?',
        },
      });
    });

    it('deletes nothing when the confirmation is declined', () => {
      createComponent();

      component.remove();

      expect(settingsServiceMock.clearToken).not.toHaveBeenCalled();
    });

    it('clears the key and marks AI unavailable when confirmed', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning(true));
      settingsServiceMock.clearToken.mockReturnValue(of({ configured: false }));

      component.remove();

      expect(settingsServiceMock.clearToken).toHaveBeenCalled();
      expect(aiSetConfigured).toHaveBeenCalledWith(false);
      expect(internals().testResult()).toBeNull();
      expect(snackBarMock.open).toHaveBeenCalledWith('API key removed.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('offers the Remove key button only once a key is configured', () => {
      createComponent();
      const removeButton = (
        Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
      ).find((button) => button.textContent?.includes('Remove key'));
      expect(removeButton).toBeUndefined();

      aiConfigured.set(true);
      fixture.detectChanges();

      expect(
        (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]).find(
          (button) => button.textContent?.includes('Remove key'),
        ),
      ).toBeDefined();
    });
  });

  // ── Todoist token ───────────────────────────────────────────────────────────

  describe('Todoist token', () => {
    it('does nothing when saving an empty token', () => {
      createComponent();

      component.saveTodoist();

      expect(settingsServiceMock.saveTodoistToken).not.toHaveBeenCalled();
    });

    it('trims and saves the token, then clears the input', () => {
      createComponent();
      internals().todoistTokenInput.set('  todoist-secret  ');
      settingsServiceMock.saveTodoistToken.mockReturnValue(of({ configured: true }));

      component.saveTodoist();

      expect(settingsServiceMock.saveTodoistToken).toHaveBeenCalledWith('todoist-secret');
      expect(todoistSetConfigured).toHaveBeenCalledWith(true);
      expect(internals().todoistTokenInput()).toBe('');
      expect(internals().todoistSaving()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith('Todoist token saved.', 'Dismiss', {
        duration: 4000,
      });
    });

    it('reports a failed save without clearing the typed token', () => {
      createComponent();
      internals().todoistTokenInput.set('todoist-secret');
      settingsServiceMock.saveTodoistToken.mockReturnValue(
        throwError(() => new Error('boom')),
      );

      component.saveTodoist();

      expect(internals().todoistTokenInput()).toBe('todoist-secret');
      expect(internals().todoistSaving()).toBe(false);
      expect(snackBarMock.open).toHaveBeenCalledWith(
        'Could not save the Todoist token. See console for details.',
        'Dismiss',
        { duration: 5000 },
      );
    });

    it('tests the stored token when nothing is typed', () => {
      createComponent();
      settingsServiceMock.testTodoistToken.mockReturnValue(of({ success: true }));

      component.testTodoist();

      expect(settingsServiceMock.testTodoistToken).toHaveBeenCalledWith(undefined);
      expect(internals().todoistTestResult()?.success).toBe(true);
      expect(internals().todoistTesting()).toBe(false);
    });

    it('tests the typed candidate', () => {
      createComponent();
      internals().todoistTokenInput.set(' candidate ');
      settingsServiceMock.testTodoistToken.mockReturnValue(of({ success: true }));

      component.testTodoist();

      expect(settingsServiceMock.testTodoistToken).toHaveBeenCalledWith('candidate');
    });

    it('surfaces a rejection message', () => {
      createComponent();
      settingsServiceMock.testTodoistToken.mockReturnValue(
        of({ errorMessage: 'Todoist rejected the token.', success: false }),
      );

      component.testTodoist();

      expect(internals().todoistTestResult()).toEqual({
        message: 'Todoist rejected the token.',
        success: false,
      });
    });

    it('reports a network failure as an unsuccessful test', () => {
      createComponent();
      settingsServiceMock.testTodoistToken.mockReturnValue(
        throwError(() => new Error('offline')),
      );

      component.testTodoist();

      expect(internals().todoistTestResult()).toEqual({
        message: 'Network error contacting the server.',
        success: false,
      });
      expect(internals().todoistTesting()).toBe(false);
    });

    it('asks for confirmation before removing the token', () => {
      createComponent();

      component.removeTodoist();

      expect(dialogMock.open).toHaveBeenCalledWith(expect.anything(), {
        data: {
          confirmLabel: 'Remove token',
          message:
            'The stored Todoist API token will be deleted. Push actions will disable until a new token is saved.',
          title: 'Remove Todoist API token?',
        },
      });
      expect(settingsServiceMock.clearTodoistToken).not.toHaveBeenCalled();
    });

    it('adopts the configured flag the clear endpoint reports', () => {
      createComponent();
      dialogMock.open.mockReturnValue(dialogReturning(true));
      settingsServiceMock.clearTodoistToken.mockReturnValue(of({ configured: false }));

      component.removeTodoist();

      expect(todoistSetConfigured).toHaveBeenCalledWith(false);
      expect(internals().todoistTokenInput()).toBe('');
      expect(internals().todoistTestResult()).toBeNull();
      expect(snackBarMock.open).toHaveBeenCalledWith('Todoist token removed.', 'Dismiss', {
        duration: 4000,
      });
    });
  });

  // ── Status pills ────────────────────────────────────────────────────────────

  describe('status pills', () => {
    it('shows both integrations as not configured by default', () => {
      createComponent();

      expect(fixture.nativeElement.querySelectorAll('.status-pill.not-configured').length).toBe(
        2,
      );
      expect(fixture.nativeElement.querySelectorAll('.status-pill.configured').length).toBe(0);
    });

    it('flips a pill to configured when that integration becomes available', () => {
      createComponent();

      aiConfigured.set(true);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelectorAll('.status-pill.configured').length).toBe(1);
    });
  });
});
