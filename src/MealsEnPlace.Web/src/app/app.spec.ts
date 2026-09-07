import { BreakpointObserver, BreakpointState } from '@angular/cdk/layout';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { App } from './app';
import { AiAvailabilityService } from './core/services/ai-availability.service';
import { InstallPromptService } from './core/services/install-prompt.service';
import { NetworkStatusService } from './core/services/network-status.service';
import { PreferencesService } from './core/services/preferences.service';
import { ThemeService } from './core/services/theme.service';
import { TodoistAvailabilityService } from './core/services/todoist-availability.service';

describe('App', () => {
  let aiRefresh: ReturnType<typeof vi.fn>;
  let breakpoint$: BehaviorSubject<BreakpointState>;
  let canInstall: ReturnType<typeof signal<boolean>>;
  let component: App;
  let displaySystem: ReturnType<typeof signal<'Imperial' | 'Metric'>>;
  let fixture: ComponentFixture<App>;
  let isDarkMode: ReturnType<typeof signal<boolean>>;
  let loadPreferences: ReturnType<typeof vi.fn>;
  let promptInstall: ReturnType<typeof vi.fn>;
  let todoistRefresh: ReturnType<typeof vi.fn>;
  let toggleDisplaySystem: ReturnType<typeof vi.fn>;
  let toggleTheme: ReturnType<typeof vi.fn>;

  function matches(isMobile: boolean): BreakpointState {
    return { breakpoints: {}, matches: isMobile };
  }

  function createComponent(isMobile = false): void {
    breakpoint$ = new BehaviorSubject<BreakpointState>(matches(isMobile));
    canInstall = signal(false);
    displaySystem = signal<'Imperial' | 'Metric'>('Imperial');
    isDarkMode = signal(false);

    aiRefresh = vi.fn();
    loadPreferences = vi.fn();
    promptInstall = vi.fn();
    todoistRefresh = vi.fn();
    toggleDisplaySystem = vi.fn();
    toggleTheme = vi.fn();

    TestBed.configureTestingModule({
      imports: [App, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: BreakpointObserver, useValue: { observe: () => breakpoint$ } },
        {
          provide: AiAvailabilityService,
          useValue: {
            configured: signal(true),
            dismissBanner: vi.fn(),
            dismissed: signal(false),
            refresh: aiRefresh,
          },
        },
        { provide: InstallPromptService, useValue: { canInstall, promptInstall } },
        { provide: NetworkStatusService, useValue: { isOnline: signal(true) } },
        {
          provide: PreferencesService,
          useValue: {
            autoDepleteOnConsume: signal(false),
            displaySystem,
            loadPreferences,
            toggleDisplaySystem,
          },
        },
        { provide: ThemeService, useValue: { isDarkMode, toggleTheme } },
        {
          provide: TodoistAvailabilityService,
          useValue: { configured: signal(true), refresh: todoistRefresh },
        },
      ],
    });

    fixture = TestBed.createComponent(App);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  function isMobileSignal(): boolean {
    return (fixture.componentInstance as unknown as { isMobile: () => boolean }).isMobile();
  }

  function queryButton(ariaLabel: string): HTMLButtonElement | null {
    return fixture.nativeElement.querySelector(`button[aria-label="${ariaLabel}"]`);
  }

  // ── Startup work ────────────────────────────────────────────────────────────

  describe('ngOnInit', () => {
    it('loads user preferences on startup', () => {
      createComponent();

      expect(loadPreferences).toHaveBeenCalled();
    });

    it('refreshes both availability services so the banners and pills are accurate', () => {
      createComponent();

      expect(aiRefresh).toHaveBeenCalled();
      expect(todoistRefresh).toHaveBeenCalled();
    });
  });

  // ── Responsive layout ───────────────────────────────────────────────────────

  describe('responsive layout', () => {
    it('is not mobile when the breakpoint does not match', () => {
      createComponent(false);

      expect(isMobileSignal()).toBe(false);
    });

    it('is mobile when the breakpoint matches', () => {
      createComponent(true);

      expect(isMobileSignal()).toBe(true);
    });

    it('tracks later breakpoint changes rather than only the first emission', () => {
      createComponent(false);

      breakpoint$.next(matches(true));
      fixture.detectChanges();

      expect(isMobileSignal()).toBe(true);
    });

    it('renders the hamburger toggle only on mobile', () => {
      createComponent(true);

      expect(queryButton('Toggle navigation')).not.toBeNull();
    });

    it('omits the hamburger toggle on desktop, where the sidenav is always open', () => {
      createComponent(false);

      expect(queryButton('Toggle navigation')).toBeNull();
    });
  });

  // ── onNavClick closes the drawer only when it overlays the content ──────────

  describe('onNavClick', () => {
    it('closes the sidenav on mobile, where the drawer covers the page', () => {
      createComponent(true);
      const close = vi.spyOn(component.sidenav, 'close');

      component.onNavClick();

      expect(close).toHaveBeenCalled();
    });

    it('leaves the sidenav open on desktop', () => {
      createComponent(false);
      const close = vi.spyOn(component.sidenav, 'close');

      component.onNavClick();

      expect(close).not.toHaveBeenCalled();
    });
  });

  // ── Toolbar affordances ─────────────────────────────────────────────────────

  describe('toolbar', () => {
    it('hides the install button until the browser offers an install prompt', () => {
      createComponent();

      expect(queryButton('Install app')).toBeNull();
    });

    it('shows the install button once an install prompt is available', () => {
      createComponent();

      canInstall.set(true);
      fixture.detectChanges();

      expect(queryButton('Install app')).not.toBeNull();
    });

    it('triggers the install prompt when the install button is clicked', () => {
      createComponent();
      canInstall.set(true);
      fixture.detectChanges();

      queryButton('Install app')?.click();

      expect(promptInstall).toHaveBeenCalled();
    });

    it('offers Metric while the display system is Imperial', () => {
      createComponent();

      expect(queryButton('Switch to Metric')).not.toBeNull();
    });

    it('offers Imperial once the display system is Metric', () => {
      createComponent();

      displaySystem.set('Metric');
      fixture.detectChanges();

      expect(queryButton('Switch to Imperial')).not.toBeNull();
    });

    it('toggles the display system when that button is clicked', () => {
      createComponent();

      queryButton('Switch to Metric')?.click();

      expect(toggleDisplaySystem).toHaveBeenCalled();
    });

    it('offers dark mode while the theme is light', () => {
      createComponent();

      expect(queryButton('Switch to dark mode')).not.toBeNull();
    });

    it('offers light mode once the theme is dark', () => {
      createComponent();

      isDarkMode.set(true);
      fixture.detectChanges();

      expect(queryButton('Switch to light mode')).not.toBeNull();
    });

    it('toggles the theme when that button is clicked', () => {
      createComponent();

      queryButton('Switch to dark mode')?.click();

      expect(toggleTheme).toHaveBeenCalled();
    });
  });

  // ── Navigation ──────────────────────────────────────────────────────────────

  it('renders a nav link for every top-level feature', () => {
    createComponent();

    const hrefs = Array.from(
      fixture.nativeElement.querySelectorAll('mat-nav-list a'),
    ).map((anchor) => (anchor as HTMLAnchorElement).getAttribute('href'));

    expect(hrefs).toEqual([
      '/inventory',
      '/expiration',
      '/meal-plan',
      '/recipes',
      '/seasonal-produce',
      '/shopping-list',
      '/waste-alerts',
      '/settings',
    ]);
  });
});
