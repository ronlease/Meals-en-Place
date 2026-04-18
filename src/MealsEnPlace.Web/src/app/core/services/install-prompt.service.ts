import { inject, Injectable, NgZone, signal } from '@angular/core';

interface BeforeInstallPromptEvent extends Event {
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
  prompt(): Promise<void>;
}

@Injectable({ providedIn: 'root' })
export class InstallPromptService {
  readonly canInstall = signal(false);

  private deferredPrompt: BeforeInstallPromptEvent | null = null;
  private readonly ngZone = inject(NgZone);

  constructor() {
    window.addEventListener('beforeinstallprompt', (e: Event) => {
      e.preventDefault();
      this.deferredPrompt = e as BeforeInstallPromptEvent;
      this.ngZone.run(() => this.canInstall.set(true));
    });

    window.addEventListener('appinstalled', () => {
      this.ngZone.run(() => {
        this.canInstall.set(false);
        this.deferredPrompt = null;
      });
    });
  }

  async promptInstall(): Promise<void> {
    if (!this.deferredPrompt) return;
    this.deferredPrompt.prompt();
    const result = await this.deferredPrompt.userChoice;
    if (result.outcome === 'accepted') {
      this.canInstall.set(false);
    }
    this.deferredPrompt = null;
  }
}
