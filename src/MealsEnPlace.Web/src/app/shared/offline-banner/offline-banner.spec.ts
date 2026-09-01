import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NetworkStatusService } from '../../core/services/network-status.service';
import { OfflineBannerComponent } from './offline-banner';

describe('OfflineBannerComponent', () => {
  let fixture: ComponentFixture<OfflineBannerComponent>;
  let isOnline: ReturnType<typeof signal<boolean>>;

  function createComponent(online: boolean): void {
    isOnline = signal(online);

    TestBed.configureTestingModule({
      imports: [OfflineBannerComponent],
      providers: [{ provide: NetworkStatusService, useValue: { isOnline } }],
    });

    fixture = TestBed.createComponent(OfflineBannerComponent);
    fixture.detectChanges();
  }

  function banner(): HTMLElement | null {
    return fixture.nativeElement.querySelector('.offline-banner');
  }

  it('renders nothing while the app is online', () => {
    createComponent(true);

    expect(banner()).toBeNull();
  });

  it('renders the banner while the app is offline', () => {
    createComponent(false);

    expect(banner()).not.toBeNull();
  });

  it('explains that cached data is being shown', () => {
    createComponent(false);

    expect(banner()?.textContent).toContain('You are offline');
    expect(banner()?.textContent).toContain('cached data');
  });

  // ── The banner tracks the signal, not just its value at creation ─────────────

  it('appears when connectivity is lost after the first render', () => {
    createComponent(true);

    isOnline.set(false);
    fixture.detectChanges();

    expect(banner()).not.toBeNull();
  });

  it('disappears when connectivity is restored', () => {
    createComponent(false);

    isOnline.set(true);
    fixture.detectChanges();

    expect(banner()).toBeNull();
  });
});
