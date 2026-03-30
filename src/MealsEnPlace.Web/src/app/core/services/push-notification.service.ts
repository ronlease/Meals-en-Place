import { inject, Injectable, signal } from '@angular/core';
import { SwPush } from '@angular/service-worker';

@Injectable({ providedIn: 'root' })
export class PushNotificationService {
  readonly permissionState = signal<NotificationPermission>(
    typeof Notification !== 'undefined' ? Notification.permission : 'default'
  );

  private readonly swPush = inject(SwPush);

  constructor() {
    this.swPush.messages.subscribe((msg) => {
      console.log('[Push] Received message:', msg);
    });

    this.swPush.notificationClicks.subscribe((click) => {
      console.log('[Push] Notification clicked:', click);
    });
  }

  async requestPermission(): Promise<NotificationPermission> {
    if (typeof Notification === 'undefined') return 'denied';
    const result = await Notification.requestPermission();
    this.permissionState.set(result);
    return result;
  }

  async subscribeToPush(): Promise<void> {
    // TODO: Replace with actual VAPID public key when push server is ready
    // const sub = await this.swPush.requestSubscription({
    //   serverPublicKey: 'VAPID_PUBLIC_KEY_HERE',
    // });
    // Send sub to server: POST /api/v1/push-subscriptions
    console.log(
      '[Push] subscribeToPush() stub — no VAPID key configured yet'
    );
  }
}
