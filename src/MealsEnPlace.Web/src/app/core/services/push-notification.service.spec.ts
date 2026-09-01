import { TestBed } from '@angular/core/testing';
import { SwPush } from '@angular/service-worker';
import { Subject } from 'rxjs';
import { PushNotificationService } from './push-notification.service';

describe('PushNotificationService', () => {
  let messages$: Subject<object>;
  let notificationClicks$: Subject<{ action: string; notification: NotificationOptions }>;

  function createService(): PushNotificationService {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: SwPush,
          useValue: { messages: messages$, notificationClicks: notificationClicks$ },
        },
      ],
    });
    return TestBed.inject(PushNotificationService);
  }

  /** Installs a Notification stub, since jsdom does not implement the API. */
  function stubNotification(
    permission: NotificationPermission,
    requested: NotificationPermission = permission,
  ): void {
    vi.stubGlobal('Notification', {
      permission,
      requestPermission: vi.fn().mockResolvedValue(requested),
    });
  }

  beforeEach(() => {
    messages$ = new Subject();
    notificationClicks$ = new Subject();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  // ── Permission state seeding ────────────────────────────────────────────────

  describe('initial permission state', () => {
    it('seeds from Notification.permission when the API exists', () => {
      stubNotification('granted');

      expect(createService().permissionState()).toBe('granted');
    });

    it('falls back to "default" when the Notification API is unavailable', () => {
      vi.stubGlobal('Notification', undefined);

      expect(createService().permissionState()).toBe('default');
    });
  });

  // ── Service-worker subscriptions ────────────────────────────────────────────

  describe('service-worker streams', () => {
    it('subscribes to push messages without throwing when one arrives', () => {
      stubNotification('default');
      createService();

      expect(() => messages$.next({ notification: { title: 'Spinach expires soon' } })).not.toThrow();
      expect(console.log).toHaveBeenCalledWith('[Push] Received message:', {
        notification: { title: 'Spinach expires soon' },
      });
    });

    it('subscribes to notification clicks without throwing when one arrives', () => {
      stubNotification('default');
      createService();

      const click = { action: 'open', notification: { body: 'Use it up' } };
      expect(() => notificationClicks$.next(click)).not.toThrow();
      expect(console.log).toHaveBeenCalledWith('[Push] Notification clicked:', click);
    });
  });

  // ── requestPermission ───────────────────────────────────────────────────────

  describe('requestPermission', () => {
    it('returns "denied" without prompting when the Notification API is unavailable', async () => {
      vi.stubGlobal('Notification', undefined);
      const service = createService();

      await expect(service.requestPermission()).resolves.toBe('denied');
      // The signal keeps its seeded value; only a real prompt updates it.
      expect(service.permissionState()).toBe('default');
    });

    it('stores and returns the granted result', async () => {
      stubNotification('default', 'granted');
      const service = createService();

      await expect(service.requestPermission()).resolves.toBe('granted');
      expect(service.permissionState()).toBe('granted');
    });

    it('stores and returns a denied result', async () => {
      stubNotification('default', 'denied');
      const service = createService();

      await expect(service.requestPermission()).resolves.toBe('denied');
      expect(service.permissionState()).toBe('denied');
    });
  });

  // ── subscribeToPush ─────────────────────────────────────────────────────────

  it('subscribeToPush is a stub that resolves without contacting a push server', async () => {
    stubNotification('granted');
    const service = createService();

    await expect(service.subscribeToPush()).resolves.toBeUndefined();
    expect(console.log).toHaveBeenCalledWith(
      '[Push] subscribeToPush() stub — no VAPID key configured yet',
    );
  });
});
