import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';

describe('ConfirmDialogComponent', () => {
  const DATA: ConfirmDialogData = {
    confirmLabel: 'Clear token',
    message: 'This removes the saved Claude API key.',
    title: 'Clear the Claude API key?',
  };

  let dialogRefMock: { close: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<ConfirmDialogComponent>;

  function createComponent(data: ConfirmDialogData = DATA): void {
    dialogRefMock = { close: vi.fn() };

    TestBed.configureTestingModule({
      imports: [ConfirmDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefMock },
      ],
    });

    fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.detectChanges();
  }

  function buttonWithText(text: string): HTMLButtonElement {
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    const match = buttons.find((button) => button.textContent?.trim() === text);
    if (!match) {
      throw new Error(`No button labelled "${text}"`);
    }
    return match;
  }

  // ── Rendering ───────────────────────────────────────────────────────────────

  describe('rendering', () => {
    it('renders the supplied title', () => {
      createComponent();

      expect(fixture.nativeElement.textContent).toContain('Clear the Claude API key?');
    });

    it('renders the supplied message', () => {
      createComponent();

      expect(fixture.nativeElement.textContent).toContain('This removes the saved Claude API key.');
    });

    it('labels the confirm button with the caller-supplied label', () => {
      // The dialog is reused across surfaces, so the label is never hard-coded.
      createComponent({
        confirmLabel: 'Delete item',
        message: 'This cannot be undone.',
        title: 'Delete this inventory item?',
      });

      expect(buttonWithText('Delete item')).toBeTruthy();
    });
  });

  // ── Result contract ─────────────────────────────────────────────────────────

  describe('result', () => {
    it('closes with true when the confirm button is clicked', () => {
      createComponent();

      buttonWithText('Clear token').click();

      expect(dialogRefMock.close).toHaveBeenCalledWith(true);
    });

    it('closes with false when Cancel is clicked', () => {
      createComponent();

      buttonWithText('Cancel').click();

      expect(dialogRefMock.close).toHaveBeenCalledWith(false);
    });

    it('does not close before the user chooses', () => {
      createComponent();

      expect(dialogRefMock.close).not.toHaveBeenCalled();
    });
  });
});
