import { TestBed } from '@angular/core/testing';
import { InstallPromptService } from './install-prompt.service';

describe('InstallPromptService', () => {
  /**
   * Builds a stand-in for the non-standard `beforeinstallprompt` event. jsdom
   * has no such event type, so the extra members are attached to a plain Event.
   */
  function makeBeforeInstallPromptEvent(outcome: 'accepted' | 'dismissed'): Event & {
    prompt: ReturnType<typeof vi.fn>;
  } {
    const event = new Event('beforeinstallprompt') as Event & {
      prompt: ReturnType<typeof vi.fn>;
      userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
    };
    event.prompt = vi.fn().mockResolvedValue(undefined);
    event.userChoice = Promise.resolve({ outcome });
    return event;
  }

  function createService(): InstallPromptService {
    TestBed.configureTestingModule({});
    return TestBed.inject(InstallPromptService);
  }

  it('cannot install until the browser fires beforeinstallprompt', () => {
    expect(createService().canInstall()).toBe(false);
  });

  // ── beforeinstallprompt capture ─────────────────────────────────────────────

  describe('beforeinstallprompt', () => {
    it('becomes installable once the event is captured', () => {
      const service = createService();

      window.dispatchEvent(makeBeforeInstallPromptEvent('accepted'));

      expect(service.canInstall()).toBe(true);
    });

    it('suppresses the browser default so the app can present its own affordance', () => {
      const service = createService();
      const event = makeBeforeInstallPromptEvent('accepted');
      const preventDefault = vi.spyOn(event, 'preventDefault');

      window.dispatchEvent(event);

      expect(preventDefault).toHaveBeenCalled();
      expect(service.canInstall()).toBe(true);
    });
  });

  // ── appinstalled ────────────────────────────────────────────────────────────

  it('clears the installable state when the app reports itself installed', () => {
    const service = createService();
    window.dispatchEvent(makeBeforeInstallPromptEvent('accepted'));

    window.dispatchEvent(new Event('appinstalled'));

    expect(service.canInstall()).toBe(false);
  });

  // ── promptInstall ───────────────────────────────────────────────────────────

  describe('promptInstall', () => {
    it('does nothing when no deferred prompt has been captured', async () => {
      const service = createService();

      await expect(service.promptInstall()).resolves.toBeUndefined();
      expect(service.canInstall()).toBe(false);
    });

    it('shows the prompt and clears the affordance when the user accepts', async () => {
      const service = createService();
      const event = makeBeforeInstallPromptEvent('accepted');
      window.dispatchEvent(event);

      await service.promptInstall();

      expect(event.prompt).toHaveBeenCalled();
      expect(service.canInstall()).toBe(false);
    });

    it('leaves canInstall true when the user dismisses, but discards the used prompt', async () => {
      // A dismissed prompt cannot be replayed — the second call must be a no-op
      // rather than calling prompt() on a spent event.
      const service = createService();
      const event = makeBeforeInstallPromptEvent('dismissed');
      window.dispatchEvent(event);

      await service.promptInstall();
      expect(service.canInstall()).toBe(true);

      await service.promptInstall();
      expect(event.prompt).toHaveBeenCalledTimes(1);
    });
  });
});
