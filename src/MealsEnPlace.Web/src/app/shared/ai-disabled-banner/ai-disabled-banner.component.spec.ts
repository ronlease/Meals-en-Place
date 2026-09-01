import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AiAvailabilityService } from '../../core/services/ai-availability.service';
import { AiDisabledBannerComponent } from './ai-disabled-banner.component';

describe('AiDisabledBannerComponent', () => {
  let configured: ReturnType<typeof signal<boolean>>;
  let dismissBanner: ReturnType<typeof vi.fn>;
  let dismissed: ReturnType<typeof signal<boolean>>;
  let fixture: ComponentFixture<AiDisabledBannerComponent>;

  function createComponent(isConfigured: boolean, isDismissed: boolean): void {
    configured = signal(isConfigured);
    dismissed = signal(isDismissed);
    dismissBanner = vi.fn(() => dismissed.set(true));

    TestBed.configureTestingModule({
      imports: [AiDisabledBannerComponent],
      providers: [
        provideRouter([]),
        {
          provide: AiAvailabilityService,
          useValue: { configured, dismissBanner, dismissed },
        },
      ],
    });

    fixture = TestBed.createComponent(AiDisabledBannerComponent);
    fixture.detectChanges();
  }

  function banner(): HTMLElement | null {
    return fixture.nativeElement.querySelector('.banner');
  }

  // ── Visibility is the product of two signals ────────────────────────────────

  describe('visibility', () => {
    it('shows when no key is configured and the banner has not been dismissed', () => {
      createComponent(false, false);

      expect(banner()).not.toBeNull();
    });

    it('hides when a key is configured', () => {
      createComponent(true, false);

      expect(banner()).toBeNull();
    });

    it('hides when the banner was dismissed for this session', () => {
      createComponent(false, true);

      expect(banner()).toBeNull();
    });

    it('hides when a key is configured even if nothing was dismissed', () => {
      createComponent(true, true);

      expect(banner()).toBeNull();
    });

    it('disappears as soon as a key becomes configured', () => {
      createComponent(false, false);

      configured.set(true);
      fixture.detectChanges();

      expect(banner()).toBeNull();
    });
  });

  // ── Content and affordances ─────────────────────────────────────────────────

  describe('content', () => {
    it('states that AI features are disabled', () => {
      createComponent(false, false);

      expect(banner()?.textContent).toContain('AI features are disabled');
    });

    it('links to the settings page so the user can paste a key', () => {
      createComponent(false, false);

      const link = fixture.nativeElement.querySelector('a') as HTMLAnchorElement;
      expect(link.getAttribute('href')).toBe('/settings');
    });

    it('announces itself to assistive technology as a polite alert', () => {
      createComponent(false, false);

      expect(banner()?.getAttribute('role')).toBe('alert');
      expect(banner()?.getAttribute('aria-live')).toBe('polite');
    });
  });

  // ── Dismiss ─────────────────────────────────────────────────────────────────

  describe('dismiss', () => {
    it('calls dismissBanner when the close button is clicked', () => {
      createComponent(false, false);

      const closeButton = fixture.nativeElement.querySelector(
        'button[aria-label="Dismiss banner for this session"]',
      ) as HTMLButtonElement;
      closeButton.click();

      expect(dismissBanner).toHaveBeenCalled();
    });

    it('hides the banner once dismissed', () => {
      createComponent(false, false);

      const closeButton = fixture.nativeElement.querySelector(
        'button[aria-label="Dismiss banner for this session"]',
      ) as HTMLButtonElement;
      closeButton.click();
      fixture.detectChanges();

      expect(banner()).toBeNull();
    });
  });
});
