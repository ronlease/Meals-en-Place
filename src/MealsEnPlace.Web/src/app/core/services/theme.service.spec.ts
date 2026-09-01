import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

const DARK_CLASS = 'dark-theme';
const STORAGE_KEY = 'mep-theme';

describe('ThemeService', () => {
  /** Installs a matchMedia stub reporting the given OS-level dark-mode preference. */
  function stubPrefersDark(prefersDark: boolean): void {
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockReturnValue({
        addEventListener: vi.fn(),
        matches: prefersDark,
        removeEventListener: vi.fn(),
      }),
    );
  }

  /** Creates the service and flushes the constructor effect that syncs the DOM. */
  function createService(): ThemeService {
    TestBed.configureTestingModule({});
    const service = TestBed.inject(ThemeService);
    TestBed.tick();
    return service;
  }

  beforeEach(() => {
    localStorage.clear();
    document.body.classList.remove(DARK_CLASS);
    stubPrefersDark(false);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
    document.body.classList.remove(DARK_CLASS);
  });

  // ── Initial resolution: stored value wins over the OS preference ─────────────

  describe('initial theme resolution', () => {
    it('restores dark mode from localStorage', () => {
      localStorage.setItem(STORAGE_KEY, 'dark');

      const service = createService();

      expect(service.isDarkMode()).toBe(true);
    });

    it('restores light mode from localStorage even when the OS prefers dark', () => {
      // An explicit stored choice must beat the OS hint.
      localStorage.setItem(STORAGE_KEY, 'light');
      stubPrefersDark(true);

      const service = createService();

      expect(service.isDarkMode()).toBe(false);
    });

    it('falls back to the OS preference when nothing is stored', () => {
      stubPrefersDark(true);

      const service = createService();

      expect(service.isDarkMode()).toBe(true);
    });

    it('defaults to light when nothing is stored and the OS prefers light', () => {
      const service = createService();

      expect(service.isDarkMode()).toBe(false);
    });
  });

  // ── The effect keeps the body class and localStorage in sync ────────────────

  describe('DOM and storage synchronization', () => {
    it('adds the dark-theme body class and persists "dark" when dark mode is active', () => {
      localStorage.setItem(STORAGE_KEY, 'dark');

      createService();

      expect(document.body.classList.contains(DARK_CLASS)).toBe(true);
      expect(localStorage.getItem(STORAGE_KEY)).toBe('dark');
    });

    it('removes the dark-theme body class and persists "light" when light mode is active', () => {
      document.body.classList.add(DARK_CLASS);

      createService();

      expect(document.body.classList.contains(DARK_CLASS)).toBe(false);
      expect(localStorage.getItem(STORAGE_KEY)).toBe('light');
    });
  });

  // ── toggleTheme ─────────────────────────────────────────────────────────────

  describe('toggleTheme', () => {
    it('flips light to dark and updates the body class and storage', () => {
      const service = createService();

      service.toggleTheme();
      TestBed.tick();

      expect(service.isDarkMode()).toBe(true);
      expect(document.body.classList.contains(DARK_CLASS)).toBe(true);
      expect(localStorage.getItem(STORAGE_KEY)).toBe('dark');
    });

    it('flips dark back to light', () => {
      localStorage.setItem(STORAGE_KEY, 'dark');
      const service = createService();

      service.toggleTheme();
      TestBed.tick();

      expect(service.isDarkMode()).toBe(false);
      expect(document.body.classList.contains(DARK_CLASS)).toBe(false);
      expect(localStorage.getItem(STORAGE_KEY)).toBe('light');
    });
  });
});
