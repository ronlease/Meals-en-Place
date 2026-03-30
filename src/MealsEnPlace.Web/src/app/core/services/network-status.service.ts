import { inject, Injectable, NgZone, OnDestroy, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class NetworkStatusService implements OnDestroy {
  readonly isOnline = signal(navigator.onLine);

  private readonly ngZone = inject(NgZone);
  private readonly offlineHandler = () =>
    this.ngZone.run(() => this.isOnline.set(false));
  private readonly onlineHandler = () =>
    this.ngZone.run(() => this.isOnline.set(true));

  constructor() {
    window.addEventListener('offline', this.offlineHandler);
    window.addEventListener('online', this.onlineHandler);
  }

  ngOnDestroy(): void {
    window.removeEventListener('offline', this.offlineHandler);
    window.removeEventListener('online', this.onlineHandler);
  }
}
